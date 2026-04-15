namespace data_registry_public.Models.local_models
{
    public class AppSettings
    {
        public string DBconnectionString { get; set; }
        public string clientId { get; set; }
        public string secretKey { get; set; }
        public string redirectUrl { get; set; }
        public string authorizeUrl { get; set; }
        public string tokenUrl { get; set; }
        public string userInfoUrl { get; set; }
        public string minJustApi { get; set; }
    }
}
