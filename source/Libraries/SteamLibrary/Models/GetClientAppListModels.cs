namespace SteamLibrary.Models
{
    public class GetClientAppListResponse
    {
        public string bytes_available { get; set; }
        public SteamClientApp[] apps { get; set; }
    }

    public class SteamClientApp
    {
        public ulong appid { get; set; }
        public string app { get; set; }
        public string app_type { get; set; }
        public bool available_on_platform { get; set; }
        public string bytes_required { get; set; }
        public bool running { get; set; }
        public bool installed { get; set; }
    }
}
