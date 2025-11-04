// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SteamLibrary.Models
{
    public class AppListResponseRoot
    {
        public AppListResponse response { get; set; }
    }

    public class AppListResponse
    {
        public SteamAppInfo[] apps { get; set; }
        public bool have_more_results { get; set; }
        public uint last_appid { get; set; }
    }

    public class SteamAppInfo
    {
        public uint appid { get; set; }
        public string name { get; set; }
        public uint last_modified { get; set; }
        public uint price_change_number { get; set; }
    }
}
