namespace SteamLibrary.Models
{
    public class GetClientAppListResponse
    {
        public string bytes_available { get; set; }
        public SteamClientApp[] apps { get; set; }
        /*public int refetch_interval_sec_full { get; set; }
        public int refetch_interval_sec_changing { get; set; }
        public int refetch_interval_sec_updating { get; set; }*/
    }

    public class SteamClientApp
    {
        public ulong appid { get; set; }
        public string app { get; set; }
        public string app_type { get; set; }
        // public bool changing { get; set; }
        // public bool available_on_platform { get; set; }
        public string bytes_required { get; set; }
        // public int queue_position { get; set; }
        public bool running { get; set; }
        // public bool auto_update { get; set; }
        public bool installed { get; set; }
        // public string bytes_downloaded { get; set; }
        // public string bytes_to_download { get; set; }
        // public string bytes_staged { get; set; }
        // public string bytes_to_stage { get; set; }
        // public int source_buildid { get; set; }
        // public int target_buildid { get; set; }
        // public int update_percentage { get; set; }
        // public int rt_time_scheduled { get; set; }
    }
}
