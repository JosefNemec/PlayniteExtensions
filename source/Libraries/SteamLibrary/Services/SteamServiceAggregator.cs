using Playnite.SDK;
using Playnite.SDK.Models;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SteamLibrary.Models;

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

                        existingGame.Source = game.Source;
                    }
                }
            }

            bool TryAddGames(Func<IEnumerable<ISteamApp>> getGamesFunc, string importSource, HashSet<string> gameIdsOutput = null, bool overwriteName = false)
            {
                try
                {
                    var apps = getGamesFunc().ToList();
                    var query = apps.Where(x => x.BackendAppInfo is null)
                        .Select(x => x.Id)
                        .Distinct()
                        .ToList();
                    var infos = playniteBackend.GetAppInfo(query).Result.ToDictionary(x => x.AppId);
                    foreach (var app in apps)
                    {
                        if (infos.TryGetValue((uint) app.Id.ToUInt64(), out var info))
                        {
                            app.BackendAppInfo = info;
                        }

                        if (string.IsNullOrWhiteSpace(app.BackendAppInfo.Name) || string.IsNullOrWhiteSpace(app.BackendAppInfo.Type))
                        {
                            logger.Warn($"{app.Id} own={app.IsOwned} incomplete backend info: [{app.BackendAppInfo!.Name}] [{app.BackendAppInfo.Type}]");
                        }
                    }

                    var games = apps
                        .Where(x => Filter(x, settings))
                        .Select(x => x.ToGame())
                        .ToList();

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

            if (settings.ImportInstalled)
            {
                TryAddGames(() => SteamLocalService.GetInstalledGames(settings.ImportInstalledMods).Values, "Installed", installedGameIds);
            }

            if (settings.ConnectAccount)
            {
                var importedOnlineOwn = new HashSet<string>();
                var importedOnlineFamily = new HashSet<string>();
                var familySharingUserIds = new HashSet<string>();
                if (settings.UseApiLogin)
                {
                    if (settings.UserId.IsNullOrEmpty())
                    {
                        throw new Exception(playniteApi.Resources.GetString(LOC.SteamNotLoggedInError));
                    }

                    TryAddGames(() => playerService.GetOwnedGamesApiKey(settings, ulong.Parse(settings.UserId), settings.RuntimeApiKey), "PlayerService (API key)", importedOnlineOwn, true);
                }
                else
                {
                    try
                    {
                        var userToken = await storeService.GetAccessTokenAsync();
                        // endpoint query order is important: if a game gets imported as own and from family, "Steam" source should win

                        if (settings.ImportGamesFamily || settings.ImportFreeFamily || settings.ImportToolsFamily || settings.ImportToolsOwn)
                        {
                            // game, demo, beta, tool - returns family content
                            // also required to filter out family tools that steam wrongly displays as own
                            // also returns all tools
                            var familyGames = familyGroupsService.GetSharedGames(settings, userToken, familySharingUserIds);
                            TryAddGames(() => familyGames, "Family Sharing", importedOnlineFamily, true);
                        }

                        /*if (settings.ImportFreeOwn)
                        {
                            // demo - returns inconclusive data. better results with next endpoint, this can be skipped entirely
                            var ownedGames = playerService.GetOwnedGamesWeb(settings, userToken);
                            TryAddGames(() => ownedGames, "PlayerService (access token)", importedOnlineOwn, true);
                        }*/

                        if (settings.ImportFreeOwn || settings.ImportToolsOwn)
                        {
                            // demo, tool - returns all demos and some tools
                            // works only when steam client is running. there is a note in UI about that
                            var clientGames = clientCommService.GetClientAppList(settings, userToken).ToList();
                            //FixToolsOwnership(clientGames, importedOnlineFamily);
                            TryAddGames(() => clientGames, "GetClientAppList", importedOnlineOwn, true);
                        }

                        if (settings.ImportGamesOwn || settings.ImportAppsOwn || settings.ImportMediaOwn || settings.ImportFreeOwn)
                        {
                            // game, app, media, beta - returns everything perfectly without any issues
                            // no need to query anything else for these types
                            var userdataGames = await GetUserdataGamesAsync(settings);
                            TryAddGames(() => userdataGames, "userdata", importedOnlineOwn, true);
                        }
                    }
                    catch (Exception e)
                    {
                        importError = e;
                        logger.Error(e, "Failed to import Steam games");
                    }
                }

                foreach (var extraAccount in ParseAdditionalAccounts(settings))
                {
                    if (familySharingUserIds.Contains(extraAccount.Item1.ToString()))
                        logger.Info($"Skipped extra account import for {extraAccount.Item1} because it's in the family sharing group");
                    else
                        TryAddGames(() => playerService.GetOwnedGamesApiKey(settings, extraAccount.Item1, extraAccount.Item2, false), $"Extra Account ({extraAccount.Item1})", importedOnlineOwn, true);
                }

                if (settings.ImportInstalledIgnoreOthers)
                {
                    // when a foreign game is installed AND also imported from family, there is no reason to exclude it
                    var installedNotOwnedNotFamily = installedGameIds.Except(importedOnlineOwn).Except(importedOnlineFamily);
                    foreach (var installedGameId in installedNotOwnedNotFamily)
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

            foreach (var unnamed in allGames.Where(x => x.Value.Name.IsNullOrWhiteSpace()))
            {
                var game = unnamed.Value;
                logger.Warn($"Unnamed game [{game.GameId}]");
                allGames.Remove(unnamed.Key);
            }

            var output = allGames.Values.ToList();
            UpdateExistingGames(output);
            return output;
        }

        private bool Filter(ISteamApp app, SteamLibrarySettings settings)
        {
            var b = app.BackendAppInfo;
            logger.Debug($"FILTER [{app.Id}] [{b.Name}] {app.GetType().Name} {b.Type} useless={b.IsUseless} game={b.IsGame} app={b.IsApp} media={b.IsMedia} free={b.IsFree} tool={b.IsTool} own={app.IsOwned}");

            if (b.IsUseless)
            {
                return false;
            }

            if (app is LocalSteamApp && settings.ImportInstalled)
            {
                return true;
            }

            if (app is ModInfo && settings.ImportInstalledMods)
            {
                return true;
            }

            if (app is ExtraIdSteamApp)
            {
                return true;
            }

            if (b.IsGame && app.IsOwned && settings.ImportGamesOwn)
            {
                return true;
            }

            if (b.IsGame && !app.IsOwned && settings.ImportGamesFamily)
            {
                return true;
            }

            if (b.IsApp && settings.ImportAppsOwn)
            {
                return true;
            }

            if (b.IsMedia && settings.ImportMediaOwn)
            {
                return true;
            }

            if (b.IsFree && app.IsOwned && settings.ImportFreeOwn)
            {
                return true;
            }

            if (b.IsFree && !app.IsOwned && settings.ImportFreeFamily)
            {
                return true;
            }

            if (b.IsTool && app.IsOwned && settings.ImportToolsOwn)
            {
                return true;
            }

            if (b.IsTool && !app.IsOwned && settings.ImportToolsFamily)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// dynamicstore/userdata + playnite backend
        /// </summary>
        private async Task<IEnumerable<ISteamApp>> GetUserdataGamesAsync(SteamLibrarySettings settings)
        {
            var userdata = await storeService.GetUserDataAsync();
            var appIds = userdata.rgOwnedApps.Select(x => new GameID(x)).ToList();
            var appInfos = await playniteBackend.GetAppInfo(appIds);
            var output = new List<ISteamApp>();
            foreach (var app in appInfos)
            {
                app.LocalizeName(settings.LanguageKey);
                output.Add(new BackendOwnSteamDbApp(app));
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

        private IEnumerable<ISteamApp> GetGamesFromExtraIds(SteamLibrarySettings settings)
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

                yield return new ExtraIdSteamApp()
                {
                    Id = uint.Parse(parseResult.Item1),
                    Name = parseResult.Item2,
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
