using AngleSharp.Parser.Html;
using Newtonsoft.Json;
using Playnite.SDK;
using SteamLibrary.Models;
using SteamLibrary.Services.Base;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace SteamLibrary.Services
{
    public class SteamStoreService : SteamAuthServiceBase
    {
        private const string recommendationQueueUrl = "https://store.steampowered.com/explore/";
        private IWebViewDownloader downloader;
        protected override string TargetUrl => recommendationQueueUrl;

        public SteamStoreService(IPlayniteAPI playniteApi) : base(playniteApi)
        {
            downloader = new WebViewDownloader(playniteApi.WebViews);
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

        protected override SteamUserToken? GetSteamUserTokenFromWebView(IWebView webView)
        {
            var url = webView.GetCurrentAddress();
            if (url.Contains("/login"))
                return null;

            var source = webView.GetPageSource();
            var userIdMatch = Regex.Match(source, "&quot;steamid&quot;:&quot;(?<id>[0-9]+)&quot;");
            var tokenMatch = Regex.Match(source, "&quot;webapi_token&quot;:&quot;(?<token>[^&]+)&quot;");

            if (!userIdMatch.Success || !tokenMatch.Success)
            {
                logger.Warn("Could not find Steam user ID or token");
                return null;
            }

            return new SteamUserToken
            {
                UserId = ulong.Parse(userIdMatch.Groups["id"].Value),
                AccessToken = tokenMatch.Groups["token"].Value,
            };
        }
    }
}
