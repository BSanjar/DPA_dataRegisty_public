using Serilog;
using Serilog.Configuration;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using static data_registry_public.Infrastructure.Logging.LogCategories;

namespace data_registry_public.Infrastructure.Logging;

/// <summary>
/// Категории логов = папки на диске: /logs/registry_public/{auth,integration,system,errors}/{category}_yyyyMMdd.log.
/// Схема та же, что в Abilling: читаемый текст, асинхронная запись, файл на день,
/// любая ошибка дополнительно зеркалится в logs/errors.
/// </summary>
public static class LogCategories
{
    // Служебные свойства названы так, чтобы не совпасть с параметрами сообщений ({User}, {Category} и т. п.).
    public const string CategoryProperty = "LogCategory";
    public const string UserProperty = "LogUser";
    public const string IpProperty = "LogIp";

    /// <summary>Вход через ЕСИ, регистрация пользователей, привязка организации, выход; действия пользователей (инциденты).</summary>
    public const string Auth = "auth";
    /// <summary>Внешние системы: запросы и ответы Минюста, ЕСИ (токен, userinfo).</summary>
    public const string Integration = "integration";
    /// <summary>Старт/остановка, миграции при запуске, проблемные HTTP-запросы (5xx, медленные), прочее.</summary>
    public const string System = "system";

    public static readonly string[] All = { Auth, Integration, System };

    /// <summary>Явно задать категорию для блока кода: <c>using (LogCategories.Push(LogCategories.Integration)) { ... }</c>.</summary>
    public static IDisposable Push(string category) => LogContext.PushProperty(CategoryProperty, category);
}

/// <summary>
/// Проставляет Category по классу-источнику (SourceContext), если она не задана явно,
/// и значения по умолчанию для User/RequestId, чтобы шаблон строки не «плыл» вне HTTP-запроса.
/// </summary>
public sealed class LogCategoryEnricher : ILogEventEnricher
{
    private readonly IReadOnlyDictionary<string, string> _sourceToCategory;

    public LogCategoryEnricher(IReadOnlyDictionary<string, string> sourceToCategory) => _sourceToCategory = sourceToCategory;

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        if (!logEvent.Properties.ContainsKey(CategoryProperty))
        {
            var source = logEvent.Properties.TryGetValue("SourceContext", out var v) && v is ScalarValue { Value: string s } ? s : null;
            logEvent.AddPropertyIfAbsent(factory.CreateProperty(CategoryProperty, Resolve(source)));
        }
        logEvent.AddPropertyIfAbsent(factory.CreateProperty(UserProperty, "-"));
        logEvent.AddPropertyIfAbsent(factory.CreateProperty("RequestId", "-"));
        logEvent.AddPropertyIfAbsent(factory.CreateProperty(IpProperty, "-"));
    }

    private string Resolve(string? source)
    {
        if (source is null) return LogCategories.System;
        if (_sourceToCategory.TryGetValue(source, out var exact)) return exact;
        // Совпадение по префиксу (пространство имён): самое длинное выигрывает.
        var best = _sourceToCategory
            .Where(p => p.Key.EndsWith('.') && source.StartsWith(p.Key, StringComparison.Ordinal))
            .OrderByDescending(p => p.Key.Length)
            .Select(p => p.Value)
            .FirstOrDefault();
        return best ?? LogCategories.System;
    }
}

public static class AppLogging
{
    /// <summary>Как подписывать пользователя в логах (по умолчанию — имя входа).</summary>
    public static Func<System.Security.Claims.ClaimsPrincipal, string?> ResolveUser { get; set; } = p => p.Identity?.Name;

    /// <summary>Скрыть идентификатор, оставив последние 4 символа: ПИН 22010199000123 → ***0123.</summary>
    public static string Mask(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Length <= 4 ? "***" : "***" + value[^4..];

    /// <summary>Тело запроса/ответа для лога: в одну строку и не длиннее max символов.</summary>
    public static string Body(string? value, int max = 4000)
    {
        if (string.IsNullOrEmpty(value)) return "(пусто)";
        var s = value.Replace('\r', ' ').Replace('\n', ' ');
        return s.Length > max ? s[..max] + $"… (+{s.Length - max} симв.)" : s;
    }

    private static string UserOf(HttpContext ctx) =>
        ctx.User.Identity?.IsAuthenticated == true ? ResolveUser(ctx.User) ?? "?" : "-";

    private const string OutputTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{LogUser} {LogIp}] [{RequestId}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Подключает Serilog вместо встроенного логгера. Настройки (appsettings, все необязательные):
    /// Logging:Directory (по умолчанию {ContentRoot}/logs/{appFolder}; в контейнере — /logs/{appFolder}, смонтировать томом),
    /// Logging:RetainedFileCountLimit (дней хранения, по умолчанию 90),
    /// Logging:MinimumLevel (Information), Logging:Console (true).
    /// </summary>
    public static string AddAppLogging(this WebApplicationBuilder builder, string appFolder, IReadOnlyDictionary<string, string> sourceToCategory)
    {
        var config = builder.Configuration;
        var directory = config["Logging:Directory"];
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(builder.Environment.ContentRootPath, "logs", appFolder);
        }
        string? fallbackReason = null;
        try
        {
            Directory.CreateDirectory(directory);
            // Проверяем право записи сразу: иначе Serilog молча ничего не запишет.
            var probe = Path.Combine(directory, ".write-test");
            System.IO.File.WriteAllText(probe, "");
            System.IO.File.Delete(probe);
        }
        catch (Exception ex)
        {
            // Не роняем приложение из-за логов: пишем во временную папку и громко сообщаем об этом.
            fallbackReason = $"{directory}: {ex.Message}";
            directory = Path.Combine(Path.GetTempPath(), "logs", appFolder);
            Directory.CreateDirectory(directory);
            Console.Error.WriteLine($"[logging] Нет доступа на запись в {fallbackReason}. Логи пишутся в {directory}");
        }

        var retained = config.GetValue<int?>("Logging:RetainedFileCountLimit") ?? 90;
        var minimum = Enum.TryParse<LogEventLevel>(config["Logging:MinimumLevel"], true, out var lvl) ? lvl : LogEventLevel.Information;
        var console = config.GetValue("Logging:Console", true);

        // Один файл на день; при >50 МБ за день — продолжение в _001, _002…
        void File(LoggerSinkConfiguration to, string path) => to.Async(a => a.File(
            path,
            outputTemplate: OutputTemplate,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: retained,
            fileSizeLimitBytes: 50L * 1024 * 1024,
            rollOnFileSizeLimit: true,
            shared: false,
            flushToDiskInterval: TimeSpan.FromSeconds(2)),
            // При отставании диска теряем часть записей, но не тормозим запросы.
            blockWhenFull: false);

        var logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimum)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With(new LogCategoryEnricher(sourceToCategory))
            .Enrich.WithProperty("App", builder.Environment.ApplicationName);

        foreach (var category in LogCategories.All)
        {
            var c = category;
            logger.WriteTo.Logger(branch =>
            {
                branch.Filter.ByIncludingOnly(e =>
                    e.Properties.TryGetValue(CategoryProperty, out var v) && v is ScalarValue { Value: string s } && s == c);
                File(branch.WriteTo, Path.Combine(directory, c, $"{c}_.log"));
            });
        }

        logger.WriteTo.Logger(branch =>
        {
            branch.Filter.ByIncludingOnly(e => e.Level >= LogEventLevel.Error);
            File(branch.WriteTo, Path.Combine(directory, "errors", "errors_.log"));
        });

        if (console)
        {
            // Для `docker logs`: то же содержимое, что и в файлах.
            logger.WriteTo.Async(a => a.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{LogCategory}] [{LogUser}] {Message:lj}{NewLine}{Exception}"));
        }

        Log.Logger = logger.CreateLogger();
        if (fallbackReason is not null)
        {
            Log.ForContext(CategoryProperty, LogCategories.System)
                .Error("Каталог логов недоступен ({Reason}) — используется {Directory}. Проверьте том и права (chown 1654).",
                    fallbackReason, directory);
        }
        // Полностью заменяет встроенный ILoggerFactory: уровни управляются только здесь.
        builder.Host.UseSerilog();
        return directory;
    }

    /// <summary>
    /// Журнал запросов (одна строка на запрос). Вызывать первым в конвейере, до UseExceptionHandler:
    /// тогда строка запроса получает итоговый код 500, а стек исключения пишет только обработчик ошибок
    /// (без дублей в logs/errors). Статика (запросы без endpoint с успешным ответом) не логируется.
    /// Логируется только путь, без query string — в нём могут быть коды и токены.
    /// </summary>
    public static IApplicationBuilder UseAppRequestLogging(this IApplicationBuilder app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "{RequestMethod} {RequestPath} → {StatusCode} за {Elapsed:0} мс";
            options.GetLevel = (ctx, elapsed, ex) =>
                ex is not null || ctx.Response.StatusCode >= 500 ? LogEventLevel.Error
                // HEAD — проверки доступности (балансировщик, мониторинг): не засоряем журнал.
                : HttpMethods.IsHead(ctx.Request.Method) ? LogEventLevel.Verbose
                // Статика и редиректы до маршрутизации (endpoint не выбран) при успешном ответе.
                : ctx.GetEndpoint() is null && ctx.Response.StatusCode < 400 ? LogEventLevel.Verbose
                : elapsed > 5000 ? LogEventLevel.Warning
                // Обычные запросы — Debug: в файлы не идут, включаются через Logging:MinimumLevel=Debug.
                : LogEventLevel.Debug;
            options.EnrichDiagnosticContext = (diag, ctx) =>
            {
                diag.Set(CategoryProperty, LogCategories.System);
                diag.Set(UserProperty, UserOf(ctx));
                diag.Set(IpProperty, ClientIp(ctx));
            };
        });
        return app;
    }

    /// <summary>Пользователь в каждой записи, сделанной во время запроса. Вызывать после UseAuthentication.</summary>
    public static IApplicationBuilder UseLogUserContext(this IApplicationBuilder app) =>
        app.Use(async (ctx, next) =>
        {
            using (LogContext.PushProperty(UserProperty, UserOf(ctx)))
            using (LogContext.PushProperty(IpProperty, ClientIp(ctx)))
            {
                await next();
            }
        });

    /// <summary>IP клиента; за обратным прокси — первый адрес из X-Forwarded-For.</summary>
    public static string ClientIp(HttpContext ctx)
    {
        var forwarded = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded.Split(',')[0].Trim();
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "-";
    }

    /// <summary>Запуск приложения с гарантированной записью фатальной ошибки и сбросом буферов.</summary>
    public static void RunWithLogging(this WebApplication app, string logDirectory)
    {
        var log = Log.ForContext(CategoryProperty, LogCategories.System).ForContext(Constants.SourceContextPropertyName, "App");
        var lifetime = app.Lifetime;
        lifetime.ApplicationStarted.Register(() =>
            log.Information(
                "Приложение запущено: окружение {Environment}, версия {Version}, логи в {LogDirectory}",
                app.Environment.EnvironmentName,
                typeof(AppLogging).Assembly.GetName().Version?.ToString() ?? "?",
                logDirectory));
        lifetime.ApplicationStopping.Register(() =>
            log.Information("Приложение останавливается"));

        try
        {
            app.Run();
        }
        catch (Exception ex) when (ex is not HostAbortedException)
        {
            log.Fatal(ex, "Приложение аварийно остановлено");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
