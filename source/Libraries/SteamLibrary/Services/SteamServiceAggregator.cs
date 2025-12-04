using Playnite.SDK;
using Playnite.SDK.Models;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SteamLibrary.Services
{
    public class SteamServiceAggregator
    {
        private readonly PlayerService playerService;
        private readonly SteamStoreService storeService;
        private readonly ClientCommService clientCommService;
        private readonly FamilyGroupsService familyGroupsService;
        private readonly SteamServicesClient playniteBackend;
        private readonly IPlayniteAPI playniteApi;
        private readonly SteamLibrary plugin;
        private readonly ILogger logger = LogManager.GetLogger();
        private static readonly Regex steamItemPattern = new Regex(@"^(.*):\s*https://store.steampowered.com/app/(\d+)$", RegexOptions.Compiled);

        public SteamServiceAggregator(PlayerService playerService, SteamStoreService storeService, ClientCommService clientCommService, FamilyGroupsService familyGroupsService, SteamServicesClient playniteBackend, SteamLibrary plugin)
        {
            this.playerService = playerService;
            this.storeService = storeService;
            this.clientCommService = clientCommService;
            this.familyGroupsService = familyGroupsService;
            this.playniteBackend = playniteBackend;
            this.playniteApi = plugin.PlayniteApi;
            this.plugin = plugin;
        }

        public async Task<IEnumerable<GameMetadata>> GetGamesAsync(SteamLibrarySettings settings)
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

                        if (existingGame.Source == null)
                            existingGame.Source = game.Source;
                    }
                }
            }

            bool TryAddGames(Func<IEnumerable<GameMetadata>> getGamesFunc, string importSource, HashSet<string> gameIdsOutput = null, bool overwriteName = false)
            {
                try
                {
                    var games = getGamesFunc().ToList();
                    logger.Info($"Found {games.Count} {importSource} Steam games.");
                    AddGames(games, overwriteName);

                    if (gameIdsOutput != null)
                        foreach (var game in games)
                            gameIdsOutput.Add(game.GameId);

                    return games.Count > 0;
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to import {importSource} Steam games.");
                    importError = e;
                    return false;
                }
            }

            if (settings.ImportInstalledGames)
                TryAddGames(() => SteamLocalService.GetInstalledGames().Values, "Installed", installedGameIds);

            if (settings.ConnectAccount)
            {
                var onlineLibraryGameIds = new HashSet<string>();
                var familySharingUserIds = new HashSet<string>();

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
                        var userToken = await storeService.GetAccessTokenAsync();
                        TryAddGames(() => playerService.GetOwnedGamesWeb(settings, userToken, settings.IncludeFreeSubGames), "PlayerService (access token)", onlineLibraryGameIds, true);

                        if (!TryAddGames(() => clientCommService.GetClientAppList(settings, userToken), "GetClientAppList", onlineLibraryGameIds, true))
                            TryAddGames(() => GetSteamStoreGamesAsync(settings, allGames).GetAwaiter().GetResult(), "userdata", onlineLibraryGameIds);

                        if (settings.ImportFamilySharedGames)
                            TryAddGames(() => familyGroupsService.GetSharedGames(settings, userToken, out familySharingUserIds), "Family Sharing", onlineLibraryGameIds, true);
                    }
                    catch (Exception e)
                    {
                        importError = e;
                        logger.Error(e, "Failed to get access token for Steam account.");
                    }
                }

                foreach (var extraAccount in ParseAdditionalAccounts(settings))
                {
                    if (familySharingUserIds.Contains(extraAccount.Item1.ToString()))
                        logger.Info($"Skipped extra account import for {extraAccount.Item1} because it's in the family sharing group");
                    else
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

            TryAddGames(() => GetGamesFromExtraIds(settings), "Settings Game-IDs");

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

            var output = allGames.Values.Where(g => !g.Name.IsNullOrWhiteSpace() && (g.IsInstalled || settings.ImportUninstalledGames)).ToList();

            foreach (var game in output.Where(g => g.Source == null)) //installed games don't get a source by default
            {
                game.Source = new MetadataNameProperty(SourceNames.Steam);
            }

            UpdateExistingGames(output);

            return output;
        }

        private async Task<IEnumerable<GameMetadata>> GetSteamStoreGamesAsync(SteamLibrarySettings settings, Dictionary<string, GameMetadata> pendingImportGames)
        {
            var appIds = (await storeService.GetUserDataAsync()).rgOwnedApps;

            var existingLibraryIds = playniteApi.Database.Games.Where(g => g.PluginId == plugin.Id).Select(g => g.GameId).ToHashSet();

            var newAppIds = appIds.Where(id =>
            {
                var strId = id.ToString();
                return !pendingImportGames.ContainsKey(strId)
                       && !existingLibraryIds.Contains(strId);
            }).ToList();

            var appInfos = playniteBackend.GetAppInfos(newAppIds).Result;

            var output = new List<GameMetadata>();

            foreach (var appInfo in appInfos)
            {
                if (appInfo.LocalizedNames?.TryGetValue(settings.LanguageKey, out var appName) != true || string.IsNullOrWhiteSpace(appName))
                    appName = appInfo.Name;

                if (string.IsNullOrWhiteSpace(appName) || !"game".Equals(appInfo.Type, StringComparison.OrdinalIgnoreCase))
                    continue;

                output.Add(new GameMetadata
                {
                    Name = appName.RemoveTrademarks(),
                    GameId = appInfo.AppId.ToString(),
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                    Source = new MetadataNameProperty(SourceNames.Steam),
                });
            }
            return output;
        }

        private void UpdateExistingGames(ICollection<GameMetadata> games)
        {
            var sources = new Dictionary<string, GameSource>();

            GameSource GetOrCreateSource(string name)
            {
                if (sources.TryGetValue(name, out var source))
                    return source;

                source = playniteApi.Database.Sources.FirstOrDefault(s => name.Equals(s.Name, StringComparison.InvariantCultureIgnoreCase))
                         ?? playniteApi.Database.Sources.Add(name);

                sources.Add(name, source);
                return source;
            }

            using (playniteApi.Database.BufferedUpdate())
            {
                foreach (var newGame in games)
                {
                    var existingGame = playniteApi.Database.Games.FirstOrDefault(g => g.GameId == newGame.GameId && g.PluginId == plugin.Id);
                    if (existingGame == null)
                        continue;

                    bool update = false;

                    var oldSource = existingGame.Source;
                    if (SourceNames.AllKnown.Contains(oldSource?.Name, StringComparer.InvariantCultureIgnoreCase))
                    {
                        var newSource = GetOrCreateSource(((MetadataNameProperty)newGame.Source).Name);

                        if (oldSource?.Id != newSource.Id)
                        {
                            existingGame.SourceId = newSource.Id;
                            update = true;
                        }
                    }

                    if (!(existingGame.InstallSize > 0) && newGame.InstallSize > 0)
                    {
                        existingGame.InstallSize = newGame.InstallSize;
                        update = true;
                    }

                    if (update)
                    {
                        existingGame.Modified = DateTime.Now;
                        playniteApi.Database.Games.Update(existingGame);
                    }
                }
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
                    Source = new MetadataNameProperty(SourceNames.Steam),
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
