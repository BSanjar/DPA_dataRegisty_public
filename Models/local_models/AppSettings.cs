namespace data_registry_public.Models.local_models
{
    /// <summary>
    /// Секция AppSettings. ЕСИ вынесена в свою секцию Esi (см. Integrations/EsiClient.cs, EsiOptions).
    /// </summary>
    public class AppSettings
    {
        /// <summary>Адрес сервиса Минюста (данные организации по ИНН).</summary>
        public string? minJustApi { get; set; }
    }
}
