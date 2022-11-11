﻿using GogLibrary.Models;
using Playnite.Common;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GogLibrary.Services
{
    public class GogApiClient
    {
        private ILogger logger = LogManager.GetLogger();

        public GogApiClient()
        {
        }

        public StorePageResult.ProductDetails GetGameStoreData(string gameUrl)
        {
            string[] data;

            try
            {
                data = HttpDownloader.DownloadString(gameUrl, new List<System.Net.Cookie>() { new System.Net.Cookie("gog_lc", Gog.EnStoreLocaleString) }).Split('\n');
            }
            catch (WebException)
            {
                return null;
            }

            var dataStarted = false;
            var stringData = string.Empty;
            foreach (var line in data)
            {
                var trimmed = line.TrimStart();
                if (line.TrimStart().StartsWith("window.productcardData"))
                {
                    dataStarted = true;
                    stringData = trimmed.Substring(25).TrimEnd(';');
                    continue;
                }

                if (line.TrimStart().StartsWith("window.activeFeatures"))
                {
                    var desData = Serialization.FromJson<StorePageResult>(stringData.TrimEnd(';'));
                    if (desData.cardProduct == null)
                    {
                        return null;
                    }

                    return desData.cardProduct;
                }

                if (dataStarted)
                {
                    stringData += trimmed;
                }
            }

            logger.Warn("Failed to get store data from page, no data found. " + gameUrl);
            return null;
        }

        public ProductApiDetail GetGameDetails(string id, string locale = "en")
        {
            var baseUrl = @"http://api.gog.com/products/{0}?expand=description&locale={1}";

            try
            {
                var stringData = HttpDownloader.DownloadString(string.Format(baseUrl, id, locale), new List<Cookie>() { new Cookie("gog_lc", Gog.EnStoreLocaleString) });
                return Serialization.FromJson<ProductApiDetail>(stringData);
            }
            catch (WebException exc)
            {
                logger.Warn(exc, "Failed to download GOG game details for " + id);
                return null;
            }
        }

        public List<StoreGamesFilteredListResponse.Product> GetStoreSearch(string searchTerm)
        {
            var baseUrl = @"https://www.gog.com/games/ajax/filtered?limit=20&search={0}";
            var url = string.Format(baseUrl, WebUtility.UrlEncode(searchTerm));

            try
            {
                var stringData = HttpDownloader.DownloadString(url);
                return Serialization.FromJson<StoreGamesFilteredListResponse>(stringData)?.products;
            }
            catch (WebException exc)
            {
                logger.Warn(exc, "Failed to get GOG store search data for " + searchTerm);
                return null;
            }
        }
    }
}
