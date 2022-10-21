using OriginLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OriginLibrary.Services
{
    public class OriginAccountClient
    {
        private const string loginUrl = @"https://www.ea.com/login";
        private const string profileUrl = @"https://myaccount.ea.com/cp-ui/aboutme/index";
        private const string logoutUrl = @"https://www.ea.com/logout";
        private const string tokenUrl = @"https://accounts.ea.com/connect/auth?client_id=ORIGIN_JS_SDK&response_type=token&redirect_uri=nucleus:rest&prompt=none";
        private ILogger logger = LogManager.GetLogger();
        private IWebView webView;

        public OriginAccountClient(IWebView webView)
        {
            this.webView = webView;
        }

        public List<AccountEntitlementsResponse.Entitlement> GetOwnedGames(long userId, AuthTokenResponse token)
        {
            var client = new WebClient();
            client.Headers.Add("authtoken", token.access_token);
            client.Headers.Add("accept", "application/vnd.origin.v3+json; x-cache/force-write");

            var stringData = client.DownloadString(string.Format(@"https://api1.origin.com/ecommerce2/consolidatedentitlements/{0}?machine_hash=1", userId));
            var data = Serialization.FromJson<AccountEntitlementsResponse>(stringData);
            return data.entitlements;
        }

        public AccountInfoResponse GetAccountInfo(AuthTokenResponse token)
        {
            var client = new WebClient();
            client.Headers.Add("Authorization", token.token_type + " " + token.access_token);
            var stringData = client.DownloadString(@"https://gateway.ea.com/proxy/identity/pids/me");
            return Serialization.FromJson<AccountInfoResponse>(stringData);
        }

        public UsageResponse GetUsage(long userId, string gameId, AuthTokenResponse token)
        {
            var gameStoreData = OriginApiClient.GetGameStoreData(gameId);
            string multiplayerId = gameStoreData.platforms.First(a => a.platform == "PCWIN").multiplayerId;
            string masterTitleId = gameStoreData.masterTitleId;

            var client = new WebClient();
            client.Headers.Add("authtoken", token.access_token);
            client.Headers.Add("X-Origin-Platform", "PCWIN");
            if (!string.IsNullOrEmpty(multiplayerId))
            {
                client.Headers.Add("MultiplayerId", multiplayerId);
            }

            var stringData = client.DownloadString(string.Format(@"https://api1.origin.com/atom/users/{0}/games/{1}/usage", userId, masterTitleId));
            return new UsageResponse(stringData);
        }

        public AuthTokenResponse GetAccessToken()
        {
            webView.NavigateAndWait(tokenUrl);
            var stringInfo = webView.GetPageText();
            var tokenData = Serialization.FromJson<AuthTokenResponse>(stringInfo);
            return tokenData;
        }

        public void Login()
        {
            webView.LoadingChanged += async (s, e) =>
            {
                var address = webView.GetCurrentAddress();
                if (address.StartsWith(profileUrl) && (await webView.GetPageTextAsync()).Contains("EA ID"))
                {
                    webView.Close();
                    return;
                }

                if (address.StartsWith(@"https://www.ea.com/") &&
                    !address.StartsWith(loginUrl) &&
                    !address.StartsWith(logoutUrl) &&
                    !address.StartsWith(profileUrl))
                {
                    webView.Navigate(profileUrl);
                }
            };

            webView.Navigate(logoutUrl);
            webView.OpenDialog();
        }

        public bool GetIsUserLoggedIn()
        {
            var token = GetAccessToken();
            return string.IsNullOrEmpty(token?.error);
        }
    }
}
