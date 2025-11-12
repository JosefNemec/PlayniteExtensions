using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using SteamKit2;
using SteamLibrary.Models;
using SteamLibrary.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamLibrary
{
    [Flags]
    public enum AppStateFlags
    {
        Invalid = 0,
        Uninstalled = 1,
        UpdateRequired = 2,
        FullyInstalled = 4,
        Encrypted = 8,
        Locked = 16,
        FilesMissing = 32,
        AppRunning = 64,
        FilesCorrupt = 128,
        UpdateRunning = 256,
        UpdatePaused = 512,
        UpdateStarted = 1024,
        Uninstalling = 2048,
        BackupRunning = 4096,
        Reconfiguring = 65536,
        Validating = 131072,
        AddingFiles = 262144,
        Preallocating = 524288,
        Downloading = 1048576,
        Staging = 2097152,
        Committing = 4194304,
        UpdateStopping = 8388608
    }

    [LoadPlugin]
    public class SteamLibrary : LibraryPluginBase<SteamLibrarySettingsViewModel>
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly Configuration config;
        internal SteamServicesClient ServicesClient;
        internal TopPanelItem TopPanelFriendsButton;

        private static readonly string[] firstPartyModPrefixes = new string[] { "bshift", "cstrike", "czero", "dmc", "dod", "gearbox", "ricochet", "tfc", "valve" };

        public SteamLibrary(IPlayniteAPI api) : base(
            "Steam",
            Guid.Parse("CB91DFC9-B977-43BF-8E70-55F46E410FAB"),
            new LibraryPluginProperties { CanShutdownClient = true, HasSettings = true },
            new SteamClient(),
            Steam.Icon,
            (_) => new SteamLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new SteamLibrarySettingsViewModel(this, PlayniteApi);
            config = GetPluginConfiguration<Configuration>();
            ServicesClient = new SteamServicesClient(config.ServicesEndpoint, api.ApplicationInfo.ApplicationVersion);
            TopPanelFriendsButton = new TopPanelItem()
            {
                Icon = new TextBlock
                {
                    Text = char.ConvertFromUtf32(0xecf9),
                    FontSize = 20,
                    FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily
                },
                Title = ResourceProvider.GetString(LOC.SteamFriendsTooltip),
                Activated = () =>
                {
                    try
                    {
                        Process.Start(@"steam://open/friends");
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "Failed to open Steam friends.");
                        PlayniteApi.Dialogs.ShowErrorMessage(e.Message, "");
                    }
                },
                Visible = SettingsViewModel.Settings.ShowFriendsButton
            };
        }

        internal static GameAction CreatePlayTask(GameID gameId)
        {
            return new GameAction()
            {
                Name = "Play",
                Type = GameActionType.URL,
                Path = @"steam://rungameid/" + gameId
            };
        }

        internal static GameMetadata GetInstalledGameFromFile(string path)
        {
            var kv = new KeyValue();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                kv.ReadAsText(fs);
            }

            if (!kv["StateFlags"].Value.IsNullOrEmpty() && Enum.TryParse<AppStateFlags>(kv["StateFlags"].Value, out var appState))
            {
                if (!appState.HasFlag(AppStateFlags.FullyInstalled))
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            var name = string.Empty;
            if (string.IsNullOrEmpty(kv["name"].Value))
            {
                if (kv["UserConfig"]["name"].Value != null)
                {
                    name = StringExtensions.NormalizeGameName(kv["UserConfig"]["name"].Value);
                }
            }
            else
            {
                name = StringExtensions.NormalizeGameName(kv["name"].Value);
            }

            var gameId = new GameID(kv["appID"].AsUnsignedInteger());
            var installDir = Path.Combine((new FileInfo(path)).Directory.FullName, "common", kv["installDir"].Value);
            if (!Directory.Exists(installDir))
            {
                installDir = Path.Combine((new FileInfo(path)).Directory.FullName, "music", kv["installDir"].Value);
                if (!Directory.Exists(installDir))
                {
                    installDir = string.Empty;
                }
            }

            var game = new GameMetadata()
            {
                Source = new MetadataNameProperty("Steam"),
                GameId = gameId.ToString(),
                Name = name.RemoveTrademarks().Trim(),
                InstallDirectory = installDir,
                IsInstalled = true,
                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
            };

            return game;
        }

        internal static List<GameMetadata> GetInstalledGamesFromFolder(string path)
        {
            var games = new List<GameMetadata>();

            foreach (var file in Directory.GetFiles(path, @"appmanifest*"))
            {
                if (file.EndsWith("tmp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var game = GetInstalledGameFromFile(Path.Combine(path, file));
                    if (game == null)
                    {
                        continue;
                    }

                    if (game.InstallDirectory.IsNullOrEmpty() || game.InstallDirectory.Contains(@"steamapps\music"))
                    {
                        logger.Info($"Steam game {game.Name} is not properly installed or it's a soundtrack, skipping.");
                        continue;
                    }

                    games.Add(game);
                }
                catch (Exception exc)
                {
                    // Steam can generate invalid acf file according to issue #37
                    logger.Error(exc, $"Failed to get information about installed game from: {file}");
                }
            }

            return games;
        }

        internal static List<GameMetadata> GetInstalledGoldSrcModsFromFolder(string path)
        {
            var games = new List<GameMetadata>();
            var dirInfo = new DirectoryInfo(path);

            foreach (var folder in dirInfo.GetDirectories().Where(a => !firstPartyModPrefixes.Any(prefix => a.Name.StartsWith(prefix))).Select(a => a.FullName))
            {
                try
                {
                    var game = GetInstalledModFromFolder(folder, ModInfo.ModType.HL);
                    if (game != null)
                    {
                        games.Add(game);
                    }
                }
                catch (Exception exc)
                {
                    // GameMetadata.txt may not exist or may be invalid
                    logger.Error(exc, $"Failed to get information about installed GoldSrc mod from: {path}");
                }
            }

            return games;
        }

        internal static List<GameMetadata> GetInstalledSourceModsFromFolder(string path)
        {
            var games = new List<GameMetadata>();

            foreach (var folder in Directory.GetDirectories(path))
            {
                try
                {
                    var game = GetInstalledModFromFolder(folder, ModInfo.ModType.HL2);
                    if (game != null)
                    {
                        games.Add(game);
                    }
                }
                catch (Exception exc)
                {
                    // GameMetadata.txt may not exist or may be invalid
                    logger.Error(exc, $"Failed to get information about installed Source mod from: {path}");
                }
            }

            return games;
        }

        internal static GameMetadata GetInstalledModFromFolder(string path, ModInfo.ModType modType)
        {
            var modInfo = ModInfo.GetFromFolder(path, modType);
            if (modInfo == null)
            {
                return null;
            }

            var game = new GameMetadata()
            {
                Source = new MetadataNameProperty("Steam"),
                GameId = modInfo.GameId.ToString(),
                Name = modInfo.Name.RemoveTrademarks().Trim(),
                InstallDirectory = path,
                IsInstalled = true,
                Developers = new HashSet<MetadataProperty>() { new MetadataNameProperty(modInfo.Developer) },
                Links = modInfo.Links,
                Tags = modInfo.Categories?.Select(a => new MetadataNameProperty(a)).Cast<MetadataProperty>().ToHashSet(),
                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
            };

            if (!modInfo.IconPath.IsNullOrEmpty() && File.Exists(modInfo.IconPath))
            {
                game.Icon = new MetadataFile(modInfo.IconPath);
            }

            return game;
        }

        internal static Dictionary<string, GameMetadata> GetInstalledGames(bool includeMods = true)
        {
            var games = new Dictionary<string, GameMetadata>();
            if (!Steam.IsInstalled)
            {
                throw new Exception("Steam installation not found.");
            }

            foreach (var folder in GetLibraryFolders())
            {
                var libFolder = Path.Combine(folder, "steamapps");
                if (Directory.Exists(libFolder))
                {
                    GetInstalledGamesFromFolder(libFolder).ForEach(a =>
                    {
                        // Ignore redist
                        if (a.GameId == "228980")
                        {
                            return;
                        }

                        if (!games.ContainsKey(a.GameId))
                        {
                            games.Add(a.GameId, a);
                        }
                    });
                }
                else
                {
                    logger.Warn($"Steam library {libFolder} not found.");
                }
            }

            if (includeMods)
            {
                try
                {
                    // In most cases, this will be inside the folder where Half-Life is installed.
                    var modInstallPath = Steam.ModInstallPath;
                    if (!string.IsNullOrEmpty(modInstallPath) && Directory.Exists(modInstallPath))
                    {
                        GetInstalledGoldSrcModsFromFolder(Steam.ModInstallPath).ForEach(a =>
                        {
                            if (!games.ContainsKey(a.GameId))
                            {
                                games.Add(a.GameId, a);
                            }
                        });
                    }

                    // In most cases, this will be inside the library folder where Steam is installed.
                    var sourceModInstallPath = Steam.SourceModInstallPath;
                    if (!string.IsNullOrEmpty(sourceModInstallPath) && Directory.Exists(sourceModInstallPath))
                    {
                        GetInstalledSourceModsFromFolder(Steam.SourceModInstallPath).ForEach(a =>
                        {
                            if (!games.ContainsKey(a.GameId))
                            {
                                games.Add(a.GameId, a);
                            }
                        });
                    }
                }
                catch (Exception e) when (!Environment.IsDebugBuild)
                {
                    logger.Error(e, "Failed to import Steam mods.");
                }
            }

            return games;
        }

        internal static List<string> GetLibraryFolders(KeyValue foldersData)
        {
            var dbs = new List<string>();
            foreach (var child in foldersData.Children)
            {
                if (int.TryParse(child.Name, out int _))
                {
                    if (!child.Value.IsNullOrEmpty())
                    {
                        dbs.Add(child.Value);
                    }
                    else if (child.Children.HasItems())
                    {
                        var path = child.Children.FirstOrDefault(a => a.Name?.Equals("path", StringComparison.OrdinalIgnoreCase) == true);
                        if (!path.Value.IsNullOrEmpty())
                        {
                            dbs.Add(path.Value);
                        }
                    }
                }
            }

            return dbs;
        }

        internal static HashSet<string> GetLibraryFolders()
        {
            var dbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Steam.InstallationPath };
            var configPath = Path.Combine(Steam.InstallationPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(configPath))
            {
                return dbs;
            }

            try
            {
                using (var fs = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var kv = new KeyValue();
                    kv.ReadAsText(fs);
                    foreach (var dir in GetLibraryFolders(kv))
                    {
                        if (Directory.Exists(dir))
                        {
                            dbs.Add(dir);
                        }
                        else
                        {
                            logger.Warn($"Found external Steam directory, but path doesn't exists: {dir}");
                        }
                    }
                }
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                logger.Error(e, "Failed to get additional Steam library folders.");
            }

            return dbs;
        }

        internal List<LocalSteamUser> GetSteamUsers()
        {
            var users = new List<LocalSteamUser>();
            if (File.Exists(Steam.LoginUsersPath))
            {
                var config = new KeyValue();

                try
                {
                    config.ReadFileAsText(Steam.LoginUsersPath);
                    foreach (var user in config.Children)
                    {
                        users.Add(new LocalSteamUser()
                        {
                            Id = ulong.Parse(user.Name),
                            AccountName = user["AccountName"].Value,
                            PersonaName = user["PersonaName"].Value,
                            Recent = user["mostrecent"].AsBoolean()
                        });
                    }
                }
                catch (Exception e) when (!Environment.IsDebugBuild)
                {
                    Logger.Error(e, "Failed to get list of local users.");
                }
            }

            return users;
        }

        internal List<GameMetadata> GetLibraryGames(SteamLibrarySettings settings)
        {
            if (settings.UserId.IsNullOrEmpty())
            {
                throw new Exception(PlayniteApi.Resources.GetString(LOC.SteamNotLoggedInError));
            }

            var userId = ulong.Parse(settings.UserId);
            if (settings.IsPrivateAccount)
            {
                return GetLibraryGames(userId, GetOwnedGamesApiKey(userId, settings.RutnimeApiKey, settings.IncludeFreeSubGames)?.response?.games);
            }
            else
            {
                return GetGamesViaSteamCommunity(settings);
            }
        }

        private List<GameMetadata> GetGamesViaSteamCommunity(SteamLibrarySettings settings)
        {
            var userToken = GetAccessToken();
            var ownedGames = GetOwnedGamesWeb(userToken.UserId, userToken.AccessToken, settings.IncludeFreeSubGames);
            return GetLibraryGames(userToken.UserId, ownedGames?.response?.games);
        }

        internal struct SteamUserToken
        {
            public ulong UserId;
            public string AccessToken;
        }

        internal SteamUserToken GetAccessToken()
        {
            using (var view = PlayniteApi.WebViews.CreateOffscreenView())
            {
                view.NavigateAndWait("https://steamcommunity.com/my/edit/info");
                var url = view.GetCurrentAddress();
                if (url.Contains("/login"))
                    throw new Exception(PlayniteApi.Resources.GetString(LOC.SteamNotLoggedInError));
                    
                var source = view.GetPageSource();
                var userIdMatch = Regex.Match(source, @"g_steamID = ""(?<id>[0-9]+)""");
                var tokenMatch = Regex.Match(source, @"&quot;webapi_token&quot;:&quot;(?<token>[^&]+)&quot;");
                
                if (!userIdMatch.Success || !tokenMatch.Success)
                    throw new Exception("Could not find Steam user ID or token");
                
                return new SteamUserToken
                {
                    UserId = ulong.Parse(userIdMatch.Groups["id"].Value),
                    AccessToken =  tokenMatch.Groups["token"].Value,
                };
            }
        }

        internal GetOwnedGamesResult GetOwnedGamesWeb(ulong userId, string accessToken, bool freeSub) => PlayerServiceGetOwnedGames(userId, "access_token", accessToken, freeSub);

        internal GetOwnedGamesResult GetOwnedGamesApiKey(ulong userId, string apiKey, bool freeSub) => PlayerServiceGetOwnedGames(userId, "key", apiKey, freeSub);

        private GetOwnedGamesResult PlayerServiceGetOwnedGames(ulong userId, string keyType, string key, bool freeSub)
        {
            var parameters = new Dictionary<string, string>
            {
                { keyType, key },
                { "steamid", userId.ToString() },
                { "include_appinfo", "true" },
                { "include_played_free_games", "true" },
                { "include_free_sub", freeSub.ToString() },
            };
            var urlStringBuilder = new StringBuilder("https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?format=json");
            foreach (var parameter in parameters)
            {
                urlStringBuilder.Append('&');
                urlStringBuilder.Append(parameter.Key);
                urlStringBuilder.Append('=');
                urlStringBuilder.Append(Uri.EscapeUriString(parameter.Value));
            }

            var libraryUrl = urlStringBuilder.ToString();

            using (var webClient = new WebClient { Encoding = Encoding.UTF8 })
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        var stringLibrary = webClient.DownloadString(libraryUrl);
                        return Serialization.FromJson<GetOwnedGamesResult>(stringLibrary);
                    }
                    catch (WebException e) when (e.Response is HttpWebResponse response)
                    {
                        // For some reason Steam Web API likes to return 429 even if you
                        // don't make a request in several hours, so just retry couple times.
                        if (response.StatusCode == (HttpStatusCode)429)
                        {
                            logger.Debug("Steam GetOwnedGames returned 429, trying again.");
                            Thread.Sleep(5_000);
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "Failed to get games from Steam Web API.");
                        break;
                    }
                }

                throw new Exception("Failed to get account data from Steam Web API, check your API key and connection to Steam's servers.");
            }
        }

        internal List<GameMetadata> GetLibraryGames(ulong userId, List<GetOwnedGamesResult.Game> ownedGames, bool includePlayTime = true)
        {
            if (ownedGames == null)
            {
                throw new Exception("No games found on specified Steam account.");
            }

            IDictionary<string, DateTime> lastActivity = null;
            try
            {
                if (includePlayTime)
                {
                    lastActivity = GetGamesLastActivity(userId);
                }
            }
            catch (Exception exc)
            {
                Logger.Warn(exc, "Failed to import Steam last activity.");
            }

            var games = new List<GameMetadata>();
            foreach (var game in ownedGames)
            {
                // Ignore games without name, like 243870
                if (string.IsNullOrEmpty(game.name))
                {
                    continue;
                }

                var newGame = new GameMetadata()
                {
                    Source = new MetadataNameProperty("Steam"),
                    Name = game.name.RemoveTrademarks().Trim(),
                    GameId = game.appid.ToString(),
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                };

                if (includePlayTime)
                {
                    newGame.Playtime = (ulong)(game.playtime_forever * 60);
                }

                if (lastActivity != null && lastActivity.TryGetValue(newGame.GameId, out var gameLastActivity) && newGame.Playtime > 0)
                {
                    newGame.LastActivity = gameLastActivity;
                }

                games.Add(newGame);
            }

            return games;
        }

        public IDictionary<string, DateTime> GetGamesLastActivity(ulong steamId)
        {
            var id = new SteamID(steamId);
            var result = new Dictionary<string, DateTime>();
            var vdf = Path.Combine(Steam.InstallationPath, "userdata", id.AccountID.ToString(), "config", "localconfig.vdf");
            if (!FileSystem.FileExists(vdf))
            {
                return result;
            }

            var sharedconfig = new KeyValue();
            sharedconfig.ReadFileAsText(vdf);

            var apps = sharedconfig["Software"]["Valve"]["Steam"]["apps"];
            foreach (var app in apps.Children)
            {
                if (app.Children.Count == 0)
                {
                    continue;
                }

                string gameId = app.Name;
                if (app.Name.Contains('_'))
                {
                    // Mods are keyed differently, "<appId>_<modId>"
                    // Ex. 215_2287856061
                    string[] parts = app.Name.Split('_');
                    if (uint.TryParse(parts[0], out uint appId) && uint.TryParse(parts[1], out uint modId))
                    {
                        var gid = new GameID()
                        {
                            AppID = appId,
                            AppType = GameID.GameType.GameMod,
                            ModID = modId
                        };
                        gameId = gid;
                    }
                    else
                    {
                        // Malformed app id?
                        continue;
                    }
                }

                var dt = DateTimeOffset.FromUnixTimeSeconds(app["LastPlayed"].AsLong()).LocalDateTime;
                if (dt.Year > 1970)
                {
                    result.Add(gameId, dt);
                }
            }

            return result;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            SettingsViewModel.IsFirstRunUse = firstRunSettings;
            return SettingsViewModel;
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new SteamMetadataProvider(this);
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new SteamInstallController(args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new SteamUninstallController(args.Game);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new SteamPlayController(args.Game, SettingsViewModel.Settings, PlayniteApi);
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var allGames = new List<GameMetadata>();
            var installedGames = new Dictionary<string, GameMetadata>();
            Exception importError = null;

            if (SettingsViewModel.Settings.ImportInstalledGames)
            {
                try
                {
                    installedGames = GetInstalledGames();
                    Logger.Debug($"Found {installedGames.Count} installed Steam games.");
                    allGames.AddRange(installedGames.Values.ToList());
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import installed Steam games.");
                    importError = e;
                }
            }

            if (SettingsViewModel.Settings.ExtraIDsToImport.HasItems())
            {
                foreach (var extraItem in SettingsViewModel.Settings.ExtraIDsToImport)
                {
                    if (extraItem.IsNullOrWhiteSpace())
                        continue;

                    var split = extraItem.Split(';');
                    if (split.Length < 2)
                        continue;

                    if (!uint.TryParse(split[0], out var appId))
                        continue;

                    if (allGames.Any(a => a.GameId == split[0]))
                        continue;

                    allGames.Add(new GameMetadata
                    {
                        GameId = split[0],
                        Name = split[1],
                        Source = new MetadataNameProperty("Steam"),
                        Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                    });
                }
            }

            if (SettingsViewModel.Settings.ConnectAccount)
            {
                try
                {
                    var libraryGames = GetLibraryGames(SettingsViewModel.Settings);
                    if (SettingsViewModel.Settings.AdditionalAccounts.HasItems())
                    {
                        foreach (var account in SettingsViewModel.Settings.AdditionalAccounts)
                        {
                            if (ulong.TryParse(account.AccountId, out var id))
                            {
                                try
                                {
                                    var accGames = GetOwnedGamesApiKey(id, account.RutnimeApiKey, SettingsViewModel.Settings.IncludeFreeSubGames);
                                    var parsedGames = GetLibraryGames(id, accGames.response.games, account.ImportPlayTime);
                                    foreach (var accGame in parsedGames)
                                    {
                                        if (!libraryGames.Any(a => a.GameId == accGame.GameId))
                                        {
                                            libraryGames.Add(accGame);
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    logger.Error(e, $"Failed to import games from {account.AccountId} Steam account.");
                                    importError = e;
                                }
                            }
                            else
                            {
                                Logger.Error("Steam account ID provided is not valid account ID.");
                            }
                        }
                    }

                    Logger.Debug($"Found {libraryGames.Count} library Steam games.");

                    if (SettingsViewModel.Settings.IgnoreOtherInstalled && SettingsViewModel.Settings.AdditionalAccounts.HasItems())
                    {
                        foreach (var installedGameId in installedGames.Keys.ToList())
                        {
                            if (new GameID(ulong.Parse(installedGameId)).IsMod)
                            {
                                continue;
                            }

                            if (libraryGames.FirstOrDefault(a => a.GameId == installedGameId) == null)
                            {
                                allGames.Remove(installedGames[installedGameId]);
                                installedGames.Remove(installedGameId);
                            }
                        }
                    }

                    if (!SettingsViewModel.Settings.ImportUninstalledGames)
                    {
                        libraryGames = libraryGames.Where(lg => installedGames.ContainsKey(lg.GameId)).ToList();
                    }

                    foreach (var game in libraryGames)
                    {
                        if (installedGames.TryGetValue(game.GameId, out var installed))
                        {
                            installed.Playtime = game.Playtime;
                            installed.LastActivity = game.LastActivity;
                        }
                        else
                        {
                            allGames.Add(game);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import linked account Steam games details.");
                    importError = e;
                }
            }

            if (importError != null)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    ImportErrorMessageId,
                    string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                    System.Environment.NewLine + importError.Message,
                    NotificationType.Error,
                    () => OpenSettingsView()));
            }
            else
            {
                PlayniteApi.Notifications.Remove(ImportErrorMessageId);
            }

            return allGames;
        }

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return TopPanelFriendsButton;
        }
    }
}