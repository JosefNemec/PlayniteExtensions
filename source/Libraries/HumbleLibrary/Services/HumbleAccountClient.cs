using AngleSharp.Parser.Html;
using HumbleLibrary.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HumbleLibrary.Services
{
    public class HumbleAccountClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly IWebView webView;
        private const string loginUrl = @"https://www.humblebundle.com/login?goto=%2Fhome%2Flibrary&qs=hmb_source%3Dnavbar";
        private const string libraryUrl = @"https://www.humblebundle.com/home/library?hmb_source=navbar";
        private const string logoutUrl = @"https://www.humblebundle.com/logout?goto=/";
        private const string ordersUrlRoot = @"https://www.humblebundle.com/api/v1/orders?all_tpkds=true";

        public HumbleAccountClient(IWebView webView)
        {
            this.webView = webView;
        }

        public void Login()
        {
            webView.LoadingChanged += (s, e) =>
            {
                if (webView.GetCurrentAddress() == libraryUrl)
                {
                    webView.Close();
                }
            };

            webView.DeleteDomainCookies(".humblebundle.com");
            webView.DeleteDomainCookies("www.humblebundle.com");
            webView.Navigate(loginUrl);
            webView.OpenDialog();
        }

        public bool GetIsUserLoggedIn()
        {
            webView.NavigateAndWait(libraryUrl);
            return webView.GetPageSource().Contains("\"gamekeys\":");
        }

        internal List<string> GetLibraryKeys()
        {
            webView.NavigateAndWait(libraryUrl);
            var parser = new HtmlParser();
            var document = parser.Parse(webView.GetPageSource());
            var userInfo = document.QuerySelector("#user-home-json-data");
            if (userInfo == null)
            {
                throw new Exception("User is not authenticated.");
            }
            else
            {
                var parsedInfo = Serialization.FromJson<UserHome>(userInfo.TextContent);
                return parsedInfo.gamekeys ?? new List<string>();
            }
        }

        internal List<Order> GetOrders(List<string> gamekeys)
        {
            var orders = new List<Order>();
            var perPage = 40;
            var bulkKeys = "";
            for (var i = 0; i < gamekeys.Count; i += perPage)
            {
                for (var j = i; j < i + perPage && j < gamekeys.Count; j++)
                {
                    bulkKeys += $"&gamekeys={gamekeys[j]}";
                }

                webView.NavigateAndWait(ordersUrlRoot + bulkKeys);
                var strContent = webView.GetPageText();
                if (Serialization.TryFromJson<Dictionary<string, Order>>(strContent, out var pageOrders))
                {
                    orders.AddRange(pageOrders.Select(a => a.Value));
                }
                else
                {
                    logger.Error("Failed to parse Humble order page.");
                    logger.Debug(strContent);
                }

                bulkKeys = "";
            }

            return orders;
        }
    }
}
