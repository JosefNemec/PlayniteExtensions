// ReSharper disable InconsistentNaming
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SteamLibrary.Models
{
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
}
