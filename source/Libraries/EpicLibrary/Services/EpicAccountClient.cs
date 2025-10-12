using EpicLibrary.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using PlayniteExtensions.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace EpicLibrary.Services
{
    public class TokenException : Exception
    {
        public TokenException(string message) : base(message)
        {
        }
    }

    public class ApiRedirectResponse
    {
        public string redirectUrl { get; set; }
        public string sid { get; set; }
        public string authorizationCode { get; set; }
    }

    public class EpicAccountClient
    {
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI api;
        private string tokensPath;
        private readonly string loginUrl = "https://www.epicgames.com/id/login?responseType=code";
        private readonly string authCodeUrl = "https://www.epicgames.com/id/api/redirect?clientId=34a02cf8f4414e29b15921876da36f9a&responseType=code";
        private readonly string oauthUrl = @"";
        private readonly string accountUrl = @"";
        private readonly string catalogUrl = @"";
        private readonly string playtimeUrl = @"";
        private readonly string libraryItemsUrl = @"";
        private const string authEncodedString = "MzRhMDJjZjhmNDQxNGUyOWIxNTkyMTg3NmRhMzZmOWE6ZGFhZmJjY2M3Mzc3NDUwMzlkZmZlNTNkOTRmYzc2Y2Y=";
        private const string userAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) EpicGamesLauncher";

        public EpicAccountClient(IPlayniteAPI api, string tokensPath)
        {
            this.api = api;
            this.tokensPath = tokensPath;
            var oauthUrlMask = @"https://{0}/account/api/oauth/token";
            var accountUrlMask = @"https://{0}/account/api/public/account/";
            var libraryItemsUrlMask = @"https://{0}/library/api/public/items?includeMetadata=true&platform=Windows";
            var catalogUrlMask = @"https://{0}/catalog/api/shared/namespace/";
            var playtimeUrlMask = @"https://{0}/library/api/public/playtime/account/{1}/all";

            var loadedFromConfig = false;
            if (!string.IsNullOrEmpty(EpicLauncher.PortalConfigPath) && File.Exists(EpicLauncher.PortalConfigPath))
            {
                try
                {
                    var config = IniParser.Parse(File.ReadAllLines(EpicLauncher.PortalConfigPath));
                    oauthUrl = string.Format(oauthUrlMask, config["Portal.OnlineSubsystemMcp.OnlineIdentityMcp Prod"]["Domain"].TrimEnd('/'));
                    accountUrl = string.Format(accountUrlMask, config["Portal.OnlineSubsystemMcp.OnlineIdentityMcp Prod"]["Domain"].TrimEnd('/'));
                    libraryItemsUrl = string.Format(libraryItemsUrlMask, config["Portal.OnlineSubsystemMcp.OnlineLibraryServiceMcp Prod"]["Domain"].TrimEnd('/'));
                    catalogUrl = string.Format(catalogUrlMask, config["Portal.OnlineSubsystemMcp.OnlineCatalogServiceMcp Prod"]["Domain"].TrimEnd('/'));
                    playtimeUrl = string.Format(playtimeUrlMask, config["Portal.OnlineSubsystemMcp.OnlineLibraryServiceMcp Prod"]["Domain"].TrimEnd('/'), "{0}");
                    loadedFromConfig = true;
                }
                catch (Exception e) when (!Debugger.IsAttached)
                {
                    logger.Error(e, "Failed to parse portal config, using default API endpoints.");
                }
            }

            if (!loadedFromConfig)
            {
                oauthUrl = string.Format(oauthUrlMask, "account-public-service-prod03.ol.epicgames.com");
                accountUrl = string.Format(accountUrlMask, "account-public-service-prod03.ol.epicgames.com");
                libraryItemsUrl = string.Format(libraryItemsUrlMask, "library-service.live.use1a.on.epicgames.com");
                catalogUrl = string.Format(catalogUrlMask, "catalog-public-service-prod06.ol.epicgames.com");
                playtimeUrl = string.Format(playtimeUrlMask, "library-service.live.use1a.on.epicgames.com", "{0}");
            }
        }

        public void LoginAlternative()
        {
            api.Dialogs.ShowMessage(
                api.Resources.GetString(LOC.EpicAlternativeAuthInstructions), "",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.None);
            ProcessStarter.StartUrl(authCodeUrl);
            var res = api.Dialogs.SelectString(LOC.EpicAuthCodeInputMessage, "", "");
            if (!res.Result || res.SelectedString.IsNullOrWhiteSpace())
            {
                return;
            }

            AuthenticateUsingAuthCode(res.SelectedString.Trim().Trim('"'));
        }

        public void Login()
        {
            var loggedIn = false;
            var apiRedirectContent = string.Empty;
            var authorizationCode = "";

            using (var view = api.WebViews.CreateView(new WebViewSettings
            {
                WindowWidth = 580,
                WindowHeight = 700,
                // This is needed otherwise captcha won't pass
                UserAgent = userAgent
            }))
            {
                view.LoadingChanged += async (s, e) =>
                {
                    var address = view.GetCurrentAddress();
                    var pageText = await view.GetPageTextAsync();
                    if (!pageText.IsNullOrEmpty() && pageText.Contains(@"localhost") && !e.IsLoading)
                    {
                        var source = await view.GetPageSourceAsync();
                        var matches = Regex.Matches(source, @"localhost\/launcher\/authorized\?code=([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
                        if (matches.Count > 0)
                        {
                            authorizationCode = matches[0].Groups[1].Value;
                            loggedIn = true;
                        }
                        view.Close();
                    }
                };

                view.DeleteDomainCookies(".epicgames.com");
                view.Navigate(loginUrl);
                view.OpenDialog();
            }

            if (!loggedIn)
            {
                return;
            }

            FileSystem.DeleteFile(tokensPath);
            if (string.IsNullOrEmpty(authorizationCode))
            {
                logger.Error("Failed to get login exchange key for Epic account.");
                return;
            }

            AuthenticateUsingAuthCode(authorizationCode);
        }

        private void AuthenticateUsingAuthCode(string authorizationCode)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", "basic " + authEncodedString);
                using (var content = new StringContent($"grant_type=authorization_code&code={authorizationCode}&token_type=eg1"))
                {
                    content.Headers.Clear();
                    content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                    var response = httpClient.PostAsync(oauthUrl, content).GetAwaiter().GetResult();
                    var respContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    FileSystem.CreateDirectory(Path.GetDirectoryName(tokensPath));
                    Encryption.EncryptToFile(
                        tokensPath,
                        respContent,
                        Encoding.UTF8,
                        WindowsIdentity.GetCurrent().User.Value);
                }
            }
        }

        public bool GetIsUserLoggedIn()
        {
            var tokens = loadTokens();
            if (tokens == null)
            {
                return false;
            }

            try
            {
                var account = InvokeRequest<AccountResponse>(accountUrl + tokens.account_id, tokens).GetAwaiter().GetResult().Item2;
                return account.id == tokens.account_id;
            }
            catch (Exception e)
            {
                if (e is TokenException)
                {
                    renewTokens(tokens.refresh_token);
                    tokens = loadTokens();
                    if (tokens.account_id.IsNullOrEmpty() || tokens.access_token.IsNullOrEmpty())
                    {
                        return false;
                    }

                    var account = InvokeRequest<AccountResponse>(accountUrl + tokens.account_id, tokens).GetAwaiter().GetResult().Item2;
                    return account.id == tokens.account_id;
                }
                else
                {
                    logger.Error(e, "Failed to validation Epic authentication.");
                    return false;
                }
            }
        }

        public List<Asset> GetAssets()
        {
            if (!GetIsUserLoggedIn())
            {
                throw new Exception("User is not authenticated.");
            }

            var response = InvokeRequest<LibraryItemsResponse>(libraryItemsUrl, loadTokens()).GetAwaiter().GetResult();
            var assets = new List<Asset>();
            assets.AddRange(response.Item2.records);

            string nextCursor = response.Item2.responseMetadata?.nextCursor;
            while (nextCursor != null)
            {
                response = InvokeRequest<LibraryItemsResponse>(
                    $"{libraryItemsUrl}&cursor={nextCursor}",
                    loadTokens()).GetAwaiter().GetResult();
                assets.AddRange(response.Item2.records);
                nextCursor = response.Item2.responseMetadata.nextCursor;
            }
            return assets;
        }

        public List<PlaytimeItem> GetPlaytimeItems()
        {
            if (!GetIsUserLoggedIn())
            {
                throw new Exception("User is not authenticated.");
            }

            var tokens = loadTokens();
            var formattedPlaytimeUrl = string.Format(playtimeUrl, tokens.account_id);
            return InvokeRequest<List<PlaytimeItem>>(formattedPlaytimeUrl, tokens).GetAwaiter().GetResult().Item2;
        }

        public CatalogItem GetCatalogItem(string nameSpace, string id, string cachePath)
        {
            Dictionary<string, CatalogItem> result = null;
            if (!cachePath.IsNullOrEmpty() && FileSystem.FileExists(cachePath))
            {
                try
                {
                    result = Serialization.FromJson<Dictionary<string, CatalogItem>>(FileSystem.ReadStringFromFile(cachePath));
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to load Epic catalog cache.");
                }
            }

            if (result == null)
            {
                var url = string.Format("{0}/bulk/items?id={1}&country=US&locale=en-US&includeMainGameDetails=true", nameSpace, id);
                var catalogResponse = InvokeRequest<Dictionary<string, CatalogItem>>(catalogUrl + url, loadTokens()).GetAwaiter().GetResult();
                result = catalogResponse.Item2;
                FileSystem.WriteStringToFile(cachePath, catalogResponse.Item1);
            }

            if (result.TryGetValue(id, out var catalogItem))
            {
                return catalogItem;
            }
            else
            {
                throw new Exception($"Epic catalog item for {id} {nameSpace} not found.");
            }
        }

        private void renewTokens(string refreshToken)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", "basic " + authEncodedString);
                using (var content = new StringContent($"grant_type=refresh_token&refresh_token={refreshToken}&token_type=eg1"))
                {
                    content.Headers.Clear();
                    content.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                    var response = httpClient.PostAsync(oauthUrl, content).GetAwaiter().GetResult();
                    var respContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    FileSystem.CreateDirectory(Path.GetDirectoryName(tokensPath));
                    Encryption.EncryptToFile(
                        tokensPath,
                        respContent,
                        Encoding.UTF8,
                        WindowsIdentity.GetCurrent().User.Value);
                }
            }
        }

        private async Task<Tuple<string, T>> InvokeRequest<T>(string url, OauthResponse tokens) where T : class
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", tokens.token_type + " " + tokens.access_token);
                var response = await httpClient.GetAsync(url);
                var str = await response.Content.ReadAsStringAsync();

                if (Serialization.TryFromJson<ErrorResponse>(str, out var error) && !string.IsNullOrEmpty(error.errorCode))
                {
                    throw new TokenException(error.errorCode);
                }
                else
                {
                    try
                    {
                        return new Tuple<string, T>(str, Serialization.FromJson<T>(str));
                    }
                    catch
                    {
                        // For cases like #134, where the entire service is down and doesn't even return valid error messages.
                        logger.Error(str);
                        throw new Exception("Failed to get data from Epic service.");
                    }
                }
            }
        }

        private OauthResponse loadTokens()
        {
            if (File.Exists(tokensPath))
            {
                try
                {
                    return Serialization.FromJson<OauthResponse>(
                        Encryption.DecryptFromFile(
                            tokensPath,
                            Encoding.UTF8,
                            WindowsIdentity.GetCurrent().User.Value));
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to load saved tokens.");
                }
            }

            return null;
        }

        private string getExchangeToken(string sid)
        {
            var cookieContainer = new CookieContainer();
            using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
            using (var httpClient = new HttpClient(handler))
            {
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("X-Epic-Event-Action", "login");
                httpClient.DefaultRequestHeaders.Add("X-Epic-Event-Category", "login");
                httpClient.DefaultRequestHeaders.Add("X-Epic-Strategy-Flags", "");
                httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

                httpClient.GetAsync(@"https://www.epicgames.com/id/api/set-sid?sid=" + sid).GetAwaiter().GetResult();
                var resp = httpClient.GetAsync(@"https://www.epicgames.com/id/api/csrf").GetAwaiter().GetResult();
                var cookies = resp.Headers.Single(header => header.Key == "Set-Cookie").Value;
                if (cookies != null)
                {
                    var match = Regex.Match(cookies.First(), @"=(.+);");
                    var xsrf = match.Groups[1].Value;
                    httpClient.DefaultRequestHeaders.Add("X-XSRF-TOKEN", xsrf);
                    var country = "US";
                    try
                    {
                        country = System.Globalization.CultureInfo.CurrentCulture.Name.Split(new char[] { '-' }).Last();
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, $"Failed to get country for auth request.");
                    }

                    cookieContainer.Add(new Uri("https://www.epicgames.com"), new Cookie("EPIC_COUNTRY", country));
                    resp = httpClient.PostAsync("https://www.epicgames.com/id/api/exchange/generate", null).GetAwaiter().GetResult();
                    var respContent = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return Serialization.FromJson<Dictionary<string, string>>(respContent)["code"];
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
