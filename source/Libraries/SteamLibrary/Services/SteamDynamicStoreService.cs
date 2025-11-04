using AngleSharp.Parser.Html;
using Newtonsoft.Json;
using SteamLibrary.Models;
using System;
using System.Linq;

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
            
            if (str.Trim().StartsWith("<html", StringComparison.InvariantCultureIgnoreCase)
                && str.Contains("<body", StringComparison.InvariantCultureIgnoreCase))
            {
                var doc = new HtmlParser().Parse(str);
                str = doc.GetElementsByTagName("body").FirstOrDefault()?.TextContent;
            }

            var model = JsonConvert.DeserializeObject<TModel>(str);
            return model;
        }
    }
}
