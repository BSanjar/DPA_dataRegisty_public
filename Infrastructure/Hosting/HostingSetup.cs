using Microsoft.AspNetCore.HttpOverrides;

namespace data_registry_public.Infrastructure.Hosting;

/// <summary>
/// Общая настройка хостинга для трёх режимов (Development / Test / Production).
/// Всё, что отличается между режимами, берётся из appsettings.{режим}.json, а не из кода.
/// </summary>
public static class HostingSetup
{
    /// <summary>
    /// Строка подключения из ConnectionStrings:DefaultConnection. Если её нет — падаем сразу при старте
    /// с понятным сообщением, а не на первом запросе к БД.
    /// </summary>
    public static string GetRequiredConnectionString(this IConfiguration configuration, IHostEnvironment env)
    {
        var connection = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                $"Не задана ConnectionStrings:DefaultConnection для режима «{env.EnvironmentName}». " +
                "Проверьте appsettings.{режим}.json или переменную ConnectionStrings__DefaultConnection.");
        }
        return connection;
    }

    /// <summary>
    /// Прокси и HTTPS. Hosting:ForwardedHeaders — доверять X-Forwarded-For/Proto от nginx (IP клиента, схема для
    /// redirect-ссылок и cookie). Hosting:HttpsRedirection — редирект на https (локально; в контейнере TLS у прокси).
    /// Вызывать первым в конвейере.
    /// </summary>
    public static WebApplication UseAppHosting(this WebApplication app)
    {
        if (app.Configuration.GetValue("Hosting:ForwardedHeaders", false))
        {
            var options = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
            };
            // Прокси в соседнем контейнере/хосте — адрес заранее неизвестен.
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
            app.UseForwardedHeaders(options);
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        if (app.Configuration.GetValue("Hosting:HttpsRedirection", false))
        {
            app.UseHttpsRedirection();
        }
        return app;
    }
}
