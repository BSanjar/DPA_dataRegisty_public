using System.Globalization;
using data_registry_public.Integrations;
using data_registry_public.Models;
using data_registry_public.Models.local_models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using data_registry_public.Infrastructure.Hosting;
using data_registry_public.Infrastructure.Logging;

var builder = WebApplication.CreateBuilder(args);

// === Логирование (Serilog, как в Abilling) ===
// /logs/registry_public/auth        — вход через ЕСИ, регистрация, привязка организации, выход; действия пользователей (инциденты)
// /logs/registry_public/integration — запросы и ответы Минюста, ЕСИ (токен, userinfo — без секретов и персональных данных)
// /logs/registry_public/system      — старт/остановка, миграции при запуске, проблемные HTTP-запросы (5xx, > 5 с), прочее
// /logs/registry_public/errors      — копия ЛЮБОЙ записи уровня Error/Fatal из всех категорий
// Корень — Logging:Directory (локально {ContentRoot}/logs/registry_public), файл на день, хранение — 90 дней.
// Пользователь в логах — внутренний id (public_users.id), а не ФИО/ПИН: минимум персональных данных.
const string Ns = "data_registry_public.";
AppLogging.ResolveUser = principal => principal.FindFirst("id")?.Value;
var logDirectory = builder.AddAppLogging("registry_public", new Dictionary<string, string>
{
    [Ns + "Controllers.esiController"] = LogCategories.Auth,
    [Ns + "Integrations."] = LogCategories.Integration,
    [Ns + "Controllers.IncidentsController"] = LogCategories.Auth,
    [Ns + "Controllers.UserPageController"] = LogCategories.Auth,
});

// Локализация. ResourcesPath не задаём: MSBuild встраивает resx как manifest-ресурсы
// корневого пространства имён (data_registry_public.SharedResource.resources).
builder.Services.AddLocalization();

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

var supportedCultures = new[]
{
    new CultureInfo("ru"),
    new CultureInfo("ky"),
};
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("ru");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    // Порядок определения языка: cookie → query ?culture= → Accept-Language
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});


// Контекст БД: строка подключения — из appsettings.{режим}.json (без неё приложение не стартует).
// Логи EF Core идут в общий Serilog (Warning+: ошибки SQL попадут в errors).
var connectionString = builder.Configuration.GetRequiredConnectionString(builder.Environment);
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = new Microsoft.AspNetCore.Http.PathString("/esi/Login");
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        // Тестовый вход выключен (Test/Production) — ранее выданные тестовые сессии больше не действуют.
        options.Events.OnValidatePrincipal = async context =>
        {
            var features = context.HttpContext.RequestServices.GetRequiredService<AppFeatures>();
            var principal = context.Principal;
            var isTestSession = principal?.HasClaim(data_registry_public.Controllers.esiController.TestLoginClaim, "true") == true
                                || principal?.FindFirst("pin")?.Value == data_registry_public.Controllers.esiController.TestUserPin;
            if (isTestSession && !features.EsiTestLogin)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.AddSingleton<AppFeatures>();

// ЕСИ (Тундук): адреса, клиент и redirect_uri — секция Esi в appsettings.{режим}.json.
builder.Services.Configure<EsiOptions>(builder.Configuration.GetSection("Esi"));
builder.Services.AddHttpClient(EsiClient.HttpClientName);
builder.Services.AddScoped<EsiClient>();

builder.Services.AddHttpClient();

// Интеграция с Минюстом (данные организаций по ИНН). Режим — MinJust:Mode:
// Real на сервере (appsettings.json), Emulator при локальном запуске (appsettings.Development.json).
builder.Services.Configure<MinJustOptions>(builder.Configuration.GetSection("MinJust"));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient(MinJustService.HttpClientName);
builder.Services.AddScoped<IMinJustService, MinJustService>();


var app = builder.Build();

// Лёгкая миграция при запуске: недостающие колонки добавляются, если их ещё нет.
// Повторный запуск безопасен (IF NOT EXISTS / ON CONFLICT DO NOTHING).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE incidents ADD COLUMN IF NOT EXISTS organization varchar");

        // Ответственное лицо по инциденту (sql/2026-10-21_incidents_responsible.sql в админке).
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE incidents ADD COLUMN IF NOT EXISTS responsible_name     varchar;
            ALTER TABLE incidents ADD COLUMN IF NOT EXISTS responsible_position varchar;
            ALTER TABLE incidents ADD COLUMN IF NOT EXISTS responsible_phone    varchar;
            ALTER TABLE incidents ADD COLUMN IF NOT EXISTS responsible_email    varchar;");

        // Реквизиты организаций, заполняемые из Минюста.
        await db.Database.ExecuteSqlRawAsync(@"
            ALTER TABLE organizations ADD COLUMN IF NOT EXISTS short_name           varchar;
            ALTER TABLE organizations ADD COLUMN IF NOT EXISTS legal_form           varchar;
            ALTER TABLE organizations ADD COLUMN IF NOT EXISTS registration_number  varchar;
            ALTER TABLE organizations ADD COLUMN IF NOT EXISTS registration_date    varchar;
            ALTER TABLE organizations ADD COLUMN IF NOT EXISTS address              varchar;
            ALTER TABLE organizations ADD COLUMN IF NOT EXISTS director_name        varchar;
            ALTER TABLE organizations ADD COLUMN IF NOT EXISTS director_position    varchar;
            ALTER TABLE organizations ADD COLUMN IF NOT EXISTS status               varchar;
            ALTER TABLE organizations ADD COLUMN IF NOT EXISTS last_synced_at       timestamp without time zone;");

        // Справочник секторов деятельности организаций.
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO organization_buisnes_sectors (id, sector_en, sector_ru, sector_kg) VALUES
                ('IT',    'Information Technology',  'Информационные технологии',  'Маалымат технологиялары'),
                ('FIN',   'Finance',                 'Финансы',                    'Каржы'),
                ('EDU',   'Education',               'Образование',                'Билим берүү'),
                ('HLTH',  'Healthcare',              'Здравоохранение',            'Саламаттык сактоо'),
                ('CONS',  'Construction',            'Строительство',              'Курулуш'),
                ('TRD',   'Trade',                   'Торговля',                   'Соода'),
                ('LOG',   'Logistics',               'Логистика',                  'Логистика'),
                ('AGRO',  'Agriculture',             'Сельское хозяйство',         'Айыл чарба'),
                ('MFG',   'Manufacturing',           'Производство',               'Өндүрүш'),
                ('TEL',   'Telecommunications',      'Телекоммуникации',           'Телекоммуникация'),
                ('ENER',  'Energy',                  'Энергетика',                 'Энергетика'),
                ('TOUR',  'Tourism',                 'Туризм',                     'Туризм'),
                ('REAL',  'Real Estate',             'Недвижимость',               'Кыймылсыз мүлк'),
                ('SERV',  'Services',                'Услуги',                     'Кызмат көрсөтүү'),
                ('GOV',   'Government',              'Государственный сектор',     'Мамлекеттик сектор'),
                ('MEDIA', 'Media & Entertainment',   'Медиа и развлечения',        'Медиа жана көңүл ачуу'),
                ('INS',   'Insurance',               'Страхование',                'Камсыздандыруу'),
                ('BANK',  'Banking',                 'Банковская деятельность',    'Банк иши'),
                ('AUTO',  'Automotive',              'Автомобильная отрасль',      'Авто тармак'),
                ('FOOD',  'Food & Beverage',         'Пищевая промышленность',     'Тамак-аш өнөр жайы'),
                ('OTHER', 'Other / Not specified',   'Другое / Не указано',        'Башка / Көрсөтүлгөн эмес')
            ON CONFLICT (id) DO NOTHING;");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Не удалось выполнить миграцию/заполнение справочников при запуске");
    }
}

// Журнал запросов — самым первым: видит итоговый код ответа (в т.ч. 500 после обработчика ошибок).
app.UseAppRequestLogging();
// Прокси-заголовки, обработчик ошибок (не в Development), https — по настройкам Hosting:*.
app.UseAppHosting();

// Старый адрес раздела (/Incedents — опечатка) → /Incidents, чтобы не ломались закладки и ссылки.
app.Use(async (context, next) =>
{
    const string legacy = "/Incedents";
    var path = context.Request.Path;
    if (path.StartsWithSegments(legacy, StringComparison.OrdinalIgnoreCase, out var rest))
    {
        context.Response.Redirect("/Incidents" + rest + context.Request.QueryString, permanent: true);
        return;
    }
    await next();
});
app.UseStaticFiles();

app.UseRouting();

// RequestLocalization — после UseRouting и до UseAuthorization,
// чтобы культура была выставлена до выполнения контроллеров.
var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(locOptions);

app.UseAuthentication();
app.UseLogUserContext();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.RunWithLogging(logDirectory);
