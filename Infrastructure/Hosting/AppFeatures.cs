namespace data_registry_public.Infrastructure.Hosting;

/// <summary>
/// Флаги возможностей из секции Features (appsettings.{режим}.json). Считаются один раз при старте.
/// </summary>
public sealed class AppFeatures
{
    public AppFeatures(IConfiguration configuration, IHostEnvironment env)
    {
        // Тестовый вход без ЕСИ: включается флагом, но на проде запрещён в любом случае.
        EsiTestLogin = configuration.GetValue("Features:EsiTestLogin", false) && !env.IsProduction();
    }

    public bool EsiTestLogin { get; }
}
