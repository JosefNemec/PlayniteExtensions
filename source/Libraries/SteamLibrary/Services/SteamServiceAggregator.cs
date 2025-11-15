using Playnite.SDK;
using Playnite.SDK.Models;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SteamLibrary.Services
{
    public class SteamServiceAggregator
    {
        private readonly PlayerService playerService;
        private readonly SteamCommunityService communityService;
        private readonly ClientCommService clientCommService;
        private readonly FamilyGroupsService familyGroupsService;
        private readonly IPlayniteAPI playniteApi;
        private readonly SteamLibrary plugin;
        private readonly ILogger logger = LogManager.GetLogger();
        private static readonly Regex steamItemPattern = new Regex(@"^(.*):\s*https://store.steampowered.com/app/(\d+)$", RegexOptions.Compiled);

        public SteamServiceAggregator(PlayerService playerService, SteamCommunityService communityService, ClientCommService clientCommService, FamilyGroupsService familyGroupsService, IPlayniteAPI playniteApi, SteamLibrary plugin)
        {
            this.playerService = playerService;
            this.communityService = communityService;
            this.clientCommService = clientCommService;
            this.familyGroupsService = familyGroupsService;
            this.playniteApi = playniteApi;
            this.plugin = plugin;
        }

        public IEnumerable<GameMetadata> GetGames(SteamLibrarySettings settings)
        {
            var installedGameIds = new HashSet<string>();

            var allGames = new Dictionary<string, GameMetadata>();
            Exception importError = null;

            void AddGames(IEnumerable<GameMetadata> gamesToAdd, bool overwriteName = false)
            {
                foreach (var game in gamesToAdd)
                {
                    if (!allGames.TryGetValue(game.GameId, out var existingGame))
                    {
                        allGames.Add(game.GameId, game);
                    }
                    else
                    {
                        if (overwriteName)
                            existingGame.Name = game.Name;

                        if (existingGame.InstallSize == null)
                            existingGame.InstallSize = game.InstallSize;

                        if (existingGame.LastActivity == null)
                            existingGame.LastActivity = game.LastActivity;

                        if (existingGame.Playtime == 0)
                            existingGame.Playtime = game.Playtime;
                    }
                }
            }

            void TryAddGames(Func<IEnumerable<GameMetadata>> getGamesFunc, string importSource, HashSet<string> gameIdsOutput = null, bool overwriteName = false)
            {
                try
                {
                    var games = getGamesFunc().ToList();
                    logger.Info($"Found {games.Count} {importSource} Steam games.");
                    AddGames(games, overwriteName);

                    if (gameIdsOutput != null)
                        foreach (var game in games)
                            gameIdsOutput.Add(game.GameId);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to import {importSource} Steam games.");
                    importError = e;
                }
            }

            if (settings.ImportInstalledGames)
                TryAddGames(() => SteamLocalService.GetInstalledGames().Values, "Installed", installedGameIds);

            TryAddGames(() => GetGamesFromExtraIds(settings), "Settings Game-IDs");

            if (settings.ConnectAccount)
            {
                var onlineLibraryGameIds = new HashSet<string>();
                
                if (settings.IsPrivateAccount)
                {
                    if (settings.UserId.IsNullOrEmpty())
                    {
                        throw new Exception(playniteApi.Resources.GetString(LOC.SteamNotLoggedInError));
                    }

                    TryAddGames(() => playerService.GetOwnedGamesApiKey(settings, ulong.Parse(settings.UserId), settings.RuntimeApiKey, settings.IncludeFreeSubGames), "PlayerService (API key)", onlineLibraryGameIds, true);
                }
                else
                {
                    try
                    {
                        var userToken = communityService.GetAccessToken();
                        TryAddGames(() => playerService.GetOwnedGamesWeb(settings, userToken, settings.IncludeFreeSubGames), "PlayerService (access token)", onlineLibraryGameIds, true);
                        TryAddGames(() => clientCommService.GetClientAppList(settings, userToken), "GetClientAppList", onlineLibraryGameIds, true);
                        TryAddGames(() => familyGroupsService.GetSharedGames(settings, userToken), "Family Sharing", onlineLibraryGameIds, true);
                    }
                    catch (Exception e)
                    {
                        importError = e;
                        logger.Error(e, "Failed to get access token for Steam account.");
                    }
                }

                foreach (var extraAccount in ParseAdditionalAccounts(settings))
                {
                    TryAddGames(() => playerService.GetOwnedGamesApiKey(settings, extraAccount.Item1, extraAccount.Item2, false, false), $"Extra Account ({extraAccount.Item1})", onlineLibraryGameIds, true);
                }

                if (settings.IgnoreOtherInstalled)
                {
                    var idsOfInstalledGamesFromAccountsNotUnderUserControl = installedGameIds.Except(onlineLibraryGameIds).ToList();
                    foreach (var installedGameId in idsOfInstalledGamesFromAccountsNotUnderUserControl)
                    {
                        if (IsModId(installedGameId))
                            continue;

                        allGames.Remove(installedGameId);
                    }
                }
            }

            if (importError != null)
            {
                playniteApi.Notifications.Add(new NotificationMessage(
                                                  plugin.ImportErrorMessageId,
                                                  string.Format(playniteApi.Resources.GetString("LOCLibraryImportError"), plugin.Name) +
                                                  System.Environment.NewLine + importError.Message,
                                                  NotificationType.Error,
                                                  () => plugin.OpenSettingsView()));
            }
            else
            {
                playniteApi.Notifications.Remove(plugin.ImportErrorMessageId);
            }

            foreach (var game in allGames.Values)
            {
                if (!settings.ImportUninstalledGames && !game.IsInstalled)
                    continue;

                yield return game;
            }
        }

        private static bool IsModId(string gameId) => new GameID(ulong.Parse(gameId)).IsMod;

        private IEnumerable<GameMetadata> GetGamesFromExtraIds(SteamLibrarySettings settings)
        {
            if (!settings.ExtraIDsToImport.HasItems())
                yield break;

            foreach (var extraItem in settings.ExtraIDsToImport)
            {
                var parseResult = ParseExtraIdItem(extraItem);
                if (parseResult is null)
                {
                    continue;
                }

                yield return new GameMetadata
                {
                    GameId = parseResult.Item1,
                    Name = parseResult.Item2,
                    Source = new MetadataNameProperty("Steam"),
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                };
            }
        }

        private IEnumerable<Tuple<ulong, string>> ParseAdditionalAccounts(SteamLibrarySettings settings)
        {
            if (!settings.AdditionalAccounts.HasItems())
                yield break;

            foreach (var account in settings.AdditionalAccounts)
            {
                if (ulong.TryParse(account.AccountId, out var id))
                {
                    yield return new Tuple<ulong, string>(id, account.RuntimeApiKey);
                }
                else
                {
                    logger.Error($"Steam account ID provided ({account.AccountId}) is not valid account ID.");
                }
            }
        }
        
        /// <summary>
        /// Parse a string in Steam drag-and-drop format or custom Playnite format
        /// </summary>
        /// <example>Counter-Strike: Source: https://store.steampowered.com/app/240</example>
        /// <example>240;Counter-Strike: Source</example>
        /// <returns>id and name</returns>
        private static Tuple<string, string> ParseExtraIdItem(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            string idToken;
            string nameToken;
            var match = steamItemPattern.Match(value);
            if (match.Success)
            {
                idToken = match.Groups[2].Value;
                nameToken = match.Groups[1].Value;
            }
            else
            {
                var split = value.Split(';');
                if (split.Length < 2)
                {
                    return null;
                }

                idToken = split[0];
                nameToken = split[1];
            }

            if (!uint.TryParse(idToken, out _))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(nameToken))
            {
                return null;
            }

            return new Tuple<string, string>(idToken, nameToken);
        }
    }
}
