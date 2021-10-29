using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Playnite.Common.Web;
using Playnite.SDK.Data;
using Steam.Models;

namespace Steam
{
    public class WebApiClient : IDisposable
    {
        private readonly WebClient webClient;

        public WebApiClient()
        {
            webClient = new WebClient { Encoding = Encoding.UTF8 };
        }

        public AppReviewsResult GetUserRating(uint appId)
        {
            var url = $"https://store.steampowered.com/appreviews/{appId}?json=1&purchase_type=all&language=all";
            return Serialization.FromJson<AppReviewsResult>(webClient.DownloadString(url));
        }

        public StoreAppDetailsResult.AppDetails GetStoreAppDetail(uint appId)
        {
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=english";
            var parsedData = Serialization.FromJson<Dictionary<string, StoreAppDetailsResult>>(webClient.DownloadString(url));
            var response = parsedData[appId.ToString()];

            // No store data for this appid
            if (response.success != true)
            {
                return null;
            }

            return response.data;
        }

        public void Dispose()
        {
            webClient.Dispose();
        }
    }
}
