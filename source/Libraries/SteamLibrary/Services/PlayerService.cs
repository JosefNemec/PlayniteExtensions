using Playnite.SDK.Models;
using SteamLibrary.Models;
using SteamLibrary.Services.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SteamLibrary.Services
{
    public class PlayerService : SteamApiServiceBase
    {
        public IEnumerable<GameMetadata> GetOwnedGamesWeb(SteamLibrarySettings settings, SteamUserToken userToken, bool freeSub) =>
            PlayerServiceGetOwnedGames(settings, userToken.UserId, "access_token", userToken.AccessToken, freeSub, true);

        public IEnumerable<GameMetadata> GetOwnedGamesApiKey(SteamLibrarySettings settings, ulong userId, string apiKey, bool freeSub, bool includePlaytime = true) =>
            PlayerServiceGetOwnedGames(settings, userId, "key", apiKey, freeSub, includePlaytime);

        private IEnumerable<GameMetadata> PlayerServiceGetOwnedGames(SteamLibrarySettings settings, ulong userId, string keyType, string key, bool freeSub, bool includePlaytime)
        {
            // For some reason Steam Web API likes to return 429 even if you
            // don't make a request in several hours, so just retry couple times.
            var retrySettings = new SteamApiRetrySettings(5, 5, 429);
            var parameters = new Dictionary<string, string>
            {
                { "format", "json" },
                { keyType, key },
                { "steamid", userId.ToString() },
                { "include_appinfo", "true" },
                { "include_played_free_games", "true" },
                { "include_free_sub", freeSub.ToString() },
                { "language", settings.LanguageKey },
            };
            var response = Get<GetOwnedGamesResult.Response>("https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/", parameters, retrySettings);
            return response.games.Select(g => ToGame(g, includePlaytime));
        }

        private static GameMetadata ToGame(GetOwnedGamesResult.Game game, bool includePlaytime)
        {
            var output = new GameMetadata
            {
                GameId = game.appid.ToString(),
                Name = game.name.RemoveTrademarks().Trim(),
                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                Source = new MetadataNameProperty(SourceNames.Steam),
            };

            if (includePlaytime)
            {
                output.Playtime = game.playtime_forever * 60;
                output.LastActivity = GetLastPlayedDateTime(game.rtime_last_played);
            }

            return output;
        }
    }
}
