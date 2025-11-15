using Playnite.SDK.Models;
using SteamLibrary.Models;
using System;
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
        public IEnumerable<GameMetadata> GetClientAppList(SteamLibrarySettings settings, SteamUserToken userToken)
        {
            var response = Get<GetClientAppListResponse>("https://api.steampowered.com/IClientCommService/GetClientAppList/v1/",
                                                         new Dictionary<string, string>
                                                         {
                                                             { "fields", "games" }, // could also add tools here like so: games|tools
                                                             { "access_token", userToken.AccessToken },
                                                             { "language", settings.LanguageKey },
                                                         });
            
            return response?.apps?.Select(ToGame)
                   ?? Enumerable.Empty<GameMetadata>();
        }

        private static GameMetadata ToGame(SteamClientApp app)
        {
            return new GameMetadata
            {
                GameId = app.appid.ToString(),
                Name = app.app.RemoveTrademarks().Trim(),
                InstallSize = GetInstallSize(app),
                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                Source = new MetadataNameProperty(SourceNames.Steam)
            };
        }

        private static ulong? GetInstallSize(SteamClientApp app)
        {
            if (ulong.TryParse(app.bytes_required, out ulong size))
                return size;

            return null;
        }
    }
}
