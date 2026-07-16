using SteamLibrary.Models;
using SteamLibrary.Services.Base;
using System.Collections.Generic;

namespace SteamLibrary.Services
{
    public class PlayerService : SteamApiServiceBase
    {
        /// <summary>
        /// IPlayerService/GetOwnedGames
        /// </summary>
        public IEnumerable<ISteamApp> GetOwnedGamesWeb(SteamLibrarySettings settings, SteamUserToken userToken) =>
            PlayerServiceGetOwnedGames(settings, userToken.UserId, "access_token", userToken.AccessToken, true);

        /// <summary>
        /// IPlayerService/GetOwnedGames
        /// </summary>
        public IEnumerable<ISteamApp> GetOwnedGamesApiKey(SteamLibrarySettings settings, ulong userId, string apiKey, bool includePlaytime = true) =>
            PlayerServiceGetOwnedGames(settings, userId, "key", apiKey, includePlaytime);

        private IEnumerable<ISteamApp> PlayerServiceGetOwnedGames(SteamLibrarySettings settings, ulong userId, string keyType, string key, bool includePlaytime)
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
                { "include_free_sub", "true" },
                { "skip_unvetted_apps", "false" },
                { "language", settings.LanguageKey },
            };
            var response = Get<GetOwnedGamesResponse>("https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/", parameters, retrySettings);
            foreach (var game in response.games)
            {
                game.IncludePlaytime = includePlaytime;
            }
            return response.games;
        }


    }
}
