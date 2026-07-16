using SteamLibrary.Models;
using SteamLibrary.Services.Base;
using System.Collections.Generic;
using System.Linq;

namespace SteamLibrary.Services
{
    /// <summary>
    /// Gets downloadable games for the current running Steam client session.
    /// Returns nothing when there's no Steam client running.
    /// </summary>
    public class ClientCommService : SteamApiServiceBase
    {
        /// <summary>
        /// IClientCommService/GetClientAppList
        /// </summary>
        public IEnumerable<ISteamApp> GetClientAppList(SteamLibrarySettings settings, SteamUserToken userToken)
        {
            var response = Get<GetClientAppListResponse>("https://api.steampowered.com/IClientCommService/GetClientAppList/v1/",
                                                         new Dictionary<string, string>
                                                         {
                                                             { "fields", "all" },
                                                             { "access_token", userToken.AccessToken },
                                                             { "language", settings.LanguageKey },
                                                         });

            return response?.apps ?? Enumerable.Empty<ISteamApp>();
        }


    }
}
