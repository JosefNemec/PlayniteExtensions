using System.Collections.Generic;

namespace SteamLibrary.Services
{
    public class SteamStoreLibraryService
    {
        private readonly SteamDynamicStoreService dataService;
        private readonly SteamAppListService appListService;

        public SteamStoreLibraryService(SteamDynamicStoreService dataService, SteamAppListService appListService)
        {
            this.dataService = dataService;
            this.appListService = appListService;
        }

        public IDictionary<uint, string> GetGames()
        {
            var userData = dataService.GetUserData();
            var apps = appListService.GetAppList();
            
            var output = new Dictionary<uint, string>();
            foreach (uint appId in userData.rgOwnedApps)
            {
                if (apps.TryGetValue(appId, out var appName))
                    output[appId] = appName;
            }

            return output;
        }
    }
}
