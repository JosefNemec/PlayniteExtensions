using GogLibrary.Models;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.UI.WebControls;

namespace GogLibrary.Services
{
    public class GogAccountClient
    {
        private ILogger logger = LogManager.GetLogger();
        private IWebView webView;

        public GogAccountClient(IWebView webView)
        {
            this.webView = webView;
        }

        public bool GetIsUserLoggedIn() => GetIsUserLoggedIn(webView);

        private static bool GetIsUserLoggedIn(IWebView webView)
        {
            var account = GetAccountInfo(webView);
            return account?.isLoggedIn ?? false;
        }

        public void Login(IWebView backgroundWebView)
        {
            var loginUrl = "https://www.gog.com/account/";

            webView.LoadingChanged += async (s, e) =>
            {
                var url = webView.GetCurrentAddress();
                if (!url.EndsWith("#openlogin"))
                {
                    var loggedIn = await Task.Run(() => GetIsUserLoggedIn(backgroundWebView));
                    if (loggedIn)
                    {
                        webView.Close();
                    }
                }
            };

            webView.DeleteDomainCookies(".gog.com");
            webView.Navigate(loginUrl);
            webView.OpenDialog();
        }

        public void ForceWebLanguage(string localeCode)
        {
            webView.Navigate(@"https://www.gog.com/user/changeLanguage/" + localeCode);
        }

        public AccountBasicResponse GetAccountInfo() => GetAccountInfo(webView);

        private static AccountBasicResponse GetAccountInfo(IWebView webView)
        {
            webView.NavigateAndWait(@"https://menu.gog.com/v1/account/basic");
            var stringInfo = webView.GetPageText();
            var accountInfo = Serialization.FromJson<AccountBasicResponse>(stringInfo);
            return accountInfo;
        }

        public List<LibraryGameResponse> GetOwnedGames(AccountBasicResponse account)
        {
            var baseUrl = @"https://www.gog.com/u/{0}/games/stats?sort=recent_playtime&order=desc&page={1}";
            var stringLibContent = string.Empty;
            var games = new List<LibraryGameResponse>();

            try
            {
                var url = string.Format(baseUrl, account.username, 1);
                webView.NavigateAndWait(url);
                stringLibContent = webView.GetPageText();
                var libraryData = Serialization.FromJson<PagedResponse<LibraryGameResponse>>(stringLibContent);
                if (libraryData == null)
                {
                    logger.Error("GOG library content is empty.");
                    return null;
                }

                games.AddRange(libraryData._embedded.items);
                if (libraryData.pages > 1)
                {
                    for (int i = 2; i <= libraryData.pages; i++)
                    {
                        webView.NavigateAndWait(string.Format(baseUrl, account.username, i));
                        stringLibContent = webView.GetPageText();
                        var pageData = Serialization.FromJson<PagedResponse<LibraryGameResponse>>(stringLibContent);
                        games.AddRange(pageData._embedded.items);
                    }
                }

                return games;
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to library from new API for account {account.username}, falling back to legacy.");
                logger.Debug(stringLibContent);
                return GetOwnedGames();
            }
        }

        public List<LibraryGameResponse> GetOwnedGames()
        {
            var games = new List<LibraryGameResponse>();
            var baseUrl = @"https://www.gog.com/account/getFilteredProducts?hiddenFlag=0&mediaType=1&page={0}&sortBy=title";
            webView.NavigateAndWait(string.Format(baseUrl, 1));
            var gamesList = webView.GetPageText();

            var libraryData = Serialization.FromJson<GetOwnedGamesResult>(gamesList);
            if (libraryData == null)
            {
                logger.Error("GOG library content is empty.");
                return null;
            }

            games.AddRange(libraryData.products.Select(a => new LibraryGameResponse()
            {
                game = new LibraryGameResponse.Game()
                {
                    id = a.id.ToString(),
                    title = a.title,
                    url = a.url,
                    image = a.image
                }
            }));

            if (libraryData.totalPages > 1)
            {
                for (int i = 2; i <= libraryData.totalPages; i++)
                {
                    webView.NavigateAndWait(string.Format(baseUrl, i));
                    gamesList = webView.GetPageText();
                    var pageData = libraryData = Serialization.FromJson<GetOwnedGamesResult>(gamesList);
                    games.AddRange(pageData.products.Select(a => new LibraryGameResponse()
                    {
                        game = new LibraryGameResponse.Game()
                        {
                            id = a.id.ToString(),
                            title = a.title,
                            url = a.url,
                            image = a.image
                        }
                    }));
                }
            }

            return games;
        }
    }
}
