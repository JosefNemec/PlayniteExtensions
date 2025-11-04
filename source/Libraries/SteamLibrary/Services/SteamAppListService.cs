using Newtonsoft.Json;
using Playnite.Services;
using SteamLibrary.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace SteamLibrary.Services
{
    public class SteamAppListService : BaseServicesClient
    {
        private readonly string apiKey;
        private readonly string appListFile;

        public SteamAppListService(string apiKey, string pluginDataFolderPath, Version playniteVersion)
            : base("https://api.steampowered.com/IStoreService/", playniteVersion)
        {
            this.apiKey = apiKey;
            appListFile = Path.Combine(pluginDataFolderPath, "applist.json");
        }

        public Dictionary<uint, string> GetAppList()
        {
            var appList = GetStoredAppList();
            var lastModified = appList.LastModified;
            AppListResponse response;
            uint? lastAppId = null;

            do
            {
                response = GetOnline(lastModified, lastAppId).response;
                foreach (var app in response.apps)
                {
                    appList.Apps[app.appid] = app.name;

                    if (app.last_modified > appList.LastModified)
                        appList.LastModified = app.last_modified;
                }

                lastAppId = response.last_appid;
            } while (response.have_more_results);

            SaveAppList(appList);
            
            return appList.Apps;
        }

        private AppListStorageModel GetStoredAppList()
        {
            if (!File.Exists(appListFile))
                return new AppListStorageModel();

            var contents = File.ReadAllText(appListFile);
            return JsonConvert.DeserializeObject<AppListStorageModel>(contents);
        }

        private void SaveAppList(AppListStorageModel appList)
        {
            var contents = JsonConvert.SerializeObject(appList);
            File.WriteAllText(appListFile, contents);
        }

        private AppListResponseRoot GetOnline(uint lastModifiedSince, uint? lastAppId)
        {
            var path = $"GetAppList/v1/?key={apiKey}&include_games=true&include_software=true&include_videos=true&if_modified_since={lastModifiedSince}";
            if (lastAppId != null)
                path += $"&last_appid={lastAppId}";

            return ExecuteGetRequest<AppListResponseRoot>(path);
        }
    }

    public class AppListStorageModel
    {
        public uint LastModified { get; set; }
        public Dictionary<uint, string> Apps { get; set; } = new Dictionary<uint, string>();
    }
}
