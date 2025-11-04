using System.Collections.Generic;

namespace SteamLibrary.Models
{
    public class SteamUserDataRoot
    {
        public uint[] rgWishlist { get; set; }
        public uint[] rgOwnedPackages { get; set; }
        public uint[] rgOwnedApps { get; set; }
        public uint[] rgFollowedApps { get; set; }
        public object[] rgMasterSubApps { get; set; }
        public object[] rgPackagesInCart { get; set; }
        public object[] rgAppsInCart { get; set; }
        public RgRecommendedTags[] rgRecommendedTags { get; set; }
        public Dictionary<uint, uint> rgIgnoredApps { get; set; }
        public object[] rgIgnoredPackages { get; set; }
        public string[] rgHardwareUsed { get; set; }
        public Dictionary<uint, Curator> rgCurators { get; set; }
        public uint[] rgCuratorsIgnored { get; set; }
        public Dictionary<uint, Dictionary<uint, uint>> rgCurations { get; set; }
        public bool bShowFilteredUserReviewScores { get; set; }
        public uint[] rgCreatorsFollowed { get; set; }
        public uint[] rgCreatorsIgnored { get; set; }
        public RgExcludedTags[] rgExcludedTags { get; set; }
        public object[] rgExcludedContentDescriptorIDs { get; set; }
        public object[] rgAutoGrantApps { get; set; }
        public uint[] rgRecommendedApps { get; set; }
        public string[] rgPreferredPlatforms { get; set; }
        public uint rgPrimaryLanguage { get; set; }
        public uint[] rgSecondaryLanguages { get; set; }
        public bool bAllowAppImpressions { get; set; }
        public uint nCartLineItemCount { get; set; }
        public uint nRemainingCartDiscount { get; set; }
        public uint nTotalCartDiscount { get; set; }
    }

    public class RgRecommendedTags
    {
        public uint tagid { get; set; }
        public string name { get; set; }
    }

    public class Curator
    {
        public uint clanid { get; set; }
        public string avatar { get; set; }
        public string name { get; set; }
    }

    public class RgExcludedTags
    {
        public uint tagid { get; set; }
        public string name { get; set; }
        public uint timestamp_added { get; set; }
    }
}
