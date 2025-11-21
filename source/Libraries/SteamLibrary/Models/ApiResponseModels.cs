using System.Collections.Generic;

namespace SteamLibrary.Models
{
    public class SteamApiResponseRoot<TResponse>
    {
        public TResponse response { get; set; }
    }

    public class GetFamilyGroupForUserResponse
    {
        public string family_groupid { get; set; }
        public bool is_not_member_of_any_group { get; set; }
    }

    public class GetSharedLibraryAppsResponse
    {
        public FamilySharedApp[] apps { get; set; }
        public string owner_steamid { get; set; }
    }

    public class FamilySharedApp
    {
        public int appid { get; set; }
        public string[] owner_steamids { get; set; }
        public string name { get; set; }
        public string capsule_filename { get; set; }
        public string img_icon_hash { get; set; }
        public uint exclude_reason { get; set; }
        public uint rt_time_acquired { get; set; }
        public uint rt_last_played { get; set; }
        /// <summary>
        /// Playtime for the current user in minutes
        /// </summary>
        public uint rt_playtime { get; set; }
        public uint app_type { get; set; }
        public uint[] content_descriptors { get; set; }
        public string sort_as { get; set; }
    }

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

    public class GetOwnedGamesResponse
    {
        public int game_count { get; set; }
        public List<OwnedGame> games { get; set; }
    }

    public class OwnedGame
    {
        public int appid { get; set; }
        public string name { get; set; }
        public uint playtime_forever { get; set; }
        public uint rtime_last_played { get; set; }
    }
}
