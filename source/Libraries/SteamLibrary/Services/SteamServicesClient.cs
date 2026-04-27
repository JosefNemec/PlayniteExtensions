using Playnite.SDK;
using PlayniteExtensions.Common;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SteamKit2;
using SteamLibrary.Models;

namespace SteamLibrary.Services
{
    public class SteamServicesClient : BackendClient
    {
        private readonly ILogger logger = LogManager.GetLogger();

        public SteamServicesClient(string endpoint) : base(endpoint)
        {
        }

        /// <summary>
        /// steam/appinfo
        /// </summary>
        public async Task<List<BackendAppInfo>> GetAppInfo(List<GameID> appIds)
        {
            // TODO local cache maybe?
            var ids = appIds.Select(x => x.ToUInt64()).ToList();
            var request = new BackendSteamDbItemsRequest()
            {
                AppIds = ids
            };

            return await PostRequest<List<BackendAppInfo>>("steam/appinfo", request);
        }
    }
}
