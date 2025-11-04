using Newtonsoft.Json;
using SteamLibrary.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace SteamLibrary.Services
{
    public class SteamAppListService : IDisposable
    {
        private readonly string apiKey;
        private readonly string appListFilePath;
        private readonly HttpClient httpClient = new HttpClient { Timeout = new TimeSpan(0, 1, 0) };

        public SteamAppListService(string apiKey, string pluginDataFolderPath)
        {
            this.apiKey = apiKey;
            appListFilePath = Path.Combine(pluginDataFolderPath, "applist.json");
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
            if (!File.Exists(appListFilePath))
                return new AppListStorageModel();

            var contents = File.ReadAllText(appListFilePath);
            return JsonConvert.DeserializeObject<AppListStorageModel>(contents);
        }

        private void SaveAppList(AppListStorageModel appList)
        {
            var contents = JsonConvert.SerializeObject(appList);
            File.WriteAllText(appListFilePath, contents);
        }

        private AppListResponseRoot GetOnline(uint lastModifiedSince, uint? lastAppId)
        {
            var url = $"https://api.steampowered.com/IStoreService/GetAppList/v1/?key={apiKey}&include_games=true&include_software=true&include_videos=true&if_modified_since={lastModifiedSince}";
            if (lastAppId != null)
                url += $"&last_appid={lastAppId}";

            var strResult = httpClient.GetStringAsync(url).GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<AppListResponseRoot>(strResult);
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }

    public class AppListStorageModel
    {
        public uint LastModified { get; set; }
        public Dictionary<uint, string> Apps { get; set; } = new Dictionary<uint, string>();
    }
}
