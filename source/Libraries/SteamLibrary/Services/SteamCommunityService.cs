using Playnite.SDK;
using SteamLibrary.Models;
using System;
using System.Text.RegularExpressions;

namespace SteamLibrary.Services
{
    public class SteamCommunityService
    {
        private IPlayniteAPI PlayniteApi { get; }

        public SteamCommunityService(IPlayniteAPI playniteApi)
        {
            PlayniteApi = playniteApi;
        }
        
        public SteamUserToken GetAccessToken()
        {
            using (var view = PlayniteApi.WebViews.CreateOffscreenView())
            {
                view.NavigateAndWait("https://steamcommunity.com/my/edit/info");
                var url = view.GetCurrentAddress();
                if (url.Contains("/login"))
                    throw new Exception(PlayniteApi.Resources.GetString(LOC.SteamNotLoggedInError));
                    
                var source = view.GetPageSource();
                var userIdMatch = Regex.Match(source, @"g_steamID = ""(?<id>[0-9]+)""");
                var tokenMatch = Regex.Match(source, "&quot;webapi_token&quot;:&quot;(?<token>[^&]+)&quot;");
                
                if (!userIdMatch.Success || !tokenMatch.Success)
                    throw new Exception("Could not find Steam user ID or token");
                
                return new SteamUserToken
                {
                    UserId = ulong.Parse(userIdMatch.Groups["id"].Value),
                    AccessToken =  tokenMatch.Groups["token"].Value,
                };
            }
        }
    }
}
