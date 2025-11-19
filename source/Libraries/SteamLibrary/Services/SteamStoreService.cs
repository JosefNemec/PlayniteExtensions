using AngleSharp.Parser.Html;
using Newtonsoft.Json;
using Playnite.SDK;
using SteamLibrary.Models;
using SteamLibrary.Services.Base;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SteamLibrary.Services
{
    public class SteamStoreService : SteamAuthServiceBase
    {
        private const string recommendationQueueUrl = "https://store.steampowered.com/explore/";
        protected override string TargetUrl => recommendationQueueUrl;

        public SteamStoreService(IPlayniteAPI playniteApi) : base(playniteApi)
        {
        }

        public async Task<SteamUserDataRoot> GetUserDataAsync()
        {
            var str = await DownloadPageSourceAsync("https://store.steampowered.com/dynamicstore/userdata/");

            if (str.Trim().StartsWith("<html", StringComparison.InvariantCultureIgnoreCase)
                && str.Contains("<body", StringComparison.InvariantCultureIgnoreCase))
            {
                var doc = await new HtmlParser().ParseAsync(str);
                str = doc.GetElementsByTagName("body").FirstOrDefault()?.TextContent;
            }

            var model = JsonConvert.DeserializeObject<SteamUserDataRoot>(str);
            return model;
        }

        private async Task<string> DownloadPageSourceAsync(string url)
        {
            using var webView = PlayniteApi.WebViews.CreateOffscreenView();
            webView.NavigateAndWait(url);
            return await webView.GetPageSourceAsync();
        }

        protected override async Task<SteamUserToken?> GetSteamUserTokenFromWebViewAsync(IWebView webView)
        {
            var url = webView.GetCurrentAddress();
            if (url.Contains("/login"))
                return null;

            var source = await webView.GetPageSourceAsync();
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
