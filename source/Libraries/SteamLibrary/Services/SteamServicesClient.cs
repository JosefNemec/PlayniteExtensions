using Playnite.Backend.Steam;
using Playnite.SDK;
using PlayniteExtensions.Common;
using SteamLibrary.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SteamLibrary.Services
{
    public class SteamServicesClient : BackendClient
    {
        private readonly ILogger logger = LogManager.GetLogger();

        public SteamServicesClient(string endpoint) : base(endpoint)
        {
        }

        public async Task<List<SteamDbItem>> GetAppInfos(List<uint> appIds)
        {
            var request = new SteamDbItemsRequest()
            {
                AppIds = appIds
            };

            return await PostRequest<List<SteamDbItem>>("steam/appinfo", request);
        }
    }
}
