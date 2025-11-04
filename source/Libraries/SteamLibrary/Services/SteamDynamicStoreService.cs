using Newtonsoft.Json;
using SteamLibrary.Models;

namespace SteamLibrary.Services
{
    public class SteamDynamicStoreService
    {
        private readonly IWebViewDownloader downloader;

        public SteamDynamicStoreService(IWebViewDownloader downloader)
        {
            this.downloader = downloader;
        }

        public SteamUserDataRoot GetUserData() => GetJson<SteamUserDataRoot>("https://store.steampowered.com/dynamicstore/userdata/");

        private TModel GetJson<TModel>(string url)
        {
            var str = downloader.DownloadPageSource(url);
            var model = JsonConvert.DeserializeObject<TModel>(str);
            return model;
        }
    }
}
