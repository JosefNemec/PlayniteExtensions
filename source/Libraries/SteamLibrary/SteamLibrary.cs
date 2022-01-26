using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using SteamLibrary.Models;
using SteamLibrary.Services;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Playnite;
using System.Windows;
using System.Reflection;
using System.Collections.ObjectModel;
using Playnite.Common.Web;
using Steam;
using System.Diagnostics;
using Playnite.SDK.Data;
using System.Windows.Media;

namespace SteamLibrary
{
    [LoadPlugin]
    public class SteamLibrary : LibraryPluginBase<SteamLibrarySettingsViewModel>
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly Configuration config;
        internal SteamServicesClient ServicesClient;
        internal TopPanelItem TopPanelFriendsButton;

        public SteamLibrary(IPlayniteAPI api) : base(
            "Steam",
            Guid.Parse("CB91DFC9-B977-43BF-8E70-55F46E410FAB"),
            new LibraryPluginProperties { CanShutdownClient = true, HasSettings = true },
            new SteamClient(),
            Steam.Icon,
            (_) => new SteamLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new SteamLibrarySettingsViewModel(this, PlayniteApi)
            {
                SteamUsers = GetSteamUsers()
            };

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
                Activated = () => Process.Start(@"steam://open/friends"),
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
            kv.ReadFileAsText(path);

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
                Name = name.RemoveTrademarks(),
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
                try
                {
                    var game = GetInstalledGameFromFile(Path.Combine(path, file));
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
            var firstPartyMods = new string[] { "bshift", "cstrike", "czero", "czeror", "dmc", "dod", "gearbox", "ricochet", "tfc", "valve" };
            var dirInfo = new DirectoryInfo(path);

            foreach (var folder in dirInfo.GetDirectories().Where(a => !firstPartyMods.Contains(a.Name)).Select(a => a.FullName))
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
                Name = modInfo.Name.RemoveTrademarks(),
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

        internal static List<string> GetLibraryFolders()
        {
            var dbs = new List<string>() { Steam.InstallationPath };
            var configPath = Path.Combine(Steam.InstallationPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(configPath))
            {
                return dbs;
            }

            try
            {
                using (var fs = new FileStream(configPath, FileMode.Open, FileAccess.Read))
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
                return GetLibraryGames(userId, GetPrivateOwnedGames(userId, settings.ApiKey, settings.IncludeFreeSubGames)?.response?.games);
            }
            else
            {
                return GetLibraryGames(userId, ServicesClient.GetSteamLibrary(userId, settings.IncludeFreeSubGames));
            }
        }

        internal GetOwnedGamesResult GetPrivateOwnedGames(ulong userId, string apiKey, bool freeSub)
        {
            var libraryUrl = @"https://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={0}&include_appinfo=1&include_played_free_games=1&format=json&steamid={1}&skip_unvetted_apps=0";
            if (freeSub)
            {
                libraryUrl += "&include_free_sub=1";
            }

            using (var webClient = new WebClient { Encoding = Encoding.UTF8 })
            {
                var stringLibrary = webClient.DownloadString(string.Format(libraryUrl, apiKey, userId));
                return Serialization.FromJson<GetOwnedGamesResult>(stringLibrary);
            }
        }

        internal List<GameMetadata> GetLibraryGames(ulong userId, List<GetOwnedGamesResult.Game> ownedGames)
        {
            if (ownedGames == null)
            {
                throw new Exception("No games found on specified Steam account.");
            }

            IDictionary<string, DateTime> lastActivity = null;
            try
            {
                lastActivity = GetGamesLastActivity(userId);
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
                    Name = game.name.RemoveTrademarks(),
                    GameId = game.appid.ToString(),
                    Playtime = (ulong)(game.playtime_forever * 60),
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                };

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

                result.Add(gameId, DateTimeOffset.FromUnixTimeSeconds(app["LastPlayed"].AsLong()).LocalDateTime);
            }

            return result;
        }

        public void ImportSteamLastActivity(ulong accountId)
        {
            var dialogs = PlayniteApi.Dialogs;
            var resources = PlayniteApi.Resources;
            var db = PlayniteApi.Database;

            if (accountId == 0)
            {
                dialogs.ShowMessage(
                    resources.GetString(LOC.SettingsSteamLastActivityImportErrorAccount),
                    resources.GetString("LOCImportError"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!db.IsOpen)
            {
                dialogs.ShowMessage(
                    resources.GetString(LOC.SettingsSteamLastActivityImportErrorDb),
                    resources.GetString("LOCImportError"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (db.BufferedUpdate())
                {
                    foreach (var kvp in GetGamesLastActivity(accountId))
                    {
                        var dbGame = db.Games.FirstOrDefault(a => a.PluginId == Id && a.GameId == kvp.Key);
                        if (dbGame == null)
                        {
                            continue;
                        }

                        if (dbGame.LastActivity >= kvp.Value)
                        {
                            continue;
                        }
                        dbGame.LastActivity = kvp.Value;
                    }
                }

                dialogs.ShowMessage(resources.GetString("LOCImportCompleted"));
            }
            catch (Exception exc) when (!Environment.IsDebugBuild)
            {
                Logger.Error(exc, "Failed to import Steam last activity.");
                dialogs.ShowMessage(
                    resources.GetString(LOC.SettingsSteamLastActivityImportError),
                    resources.GetString("LOCImportError"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public List<GameMetadata> GetCategorizedGames(ulong steamId)
        {
            var id = new SteamID(steamId);
            var result = new List<GameMetadata>();
            var vdf = Path.Combine(Steam.InstallationPath, "userdata", id.AccountID.ToString(), "7", "remote", "sharedconfig.vdf");
            var sharedconfig = new KeyValue();
            sharedconfig.ReadFileAsText(vdf);

            var apps = sharedconfig["Software"]["Valve"]["Steam"]["apps"];
            foreach (var app in apps.Children)
            {
                if (app.Children.Count == 0)
                {
                    continue;
                }

                var appData = new List<string>();
                var isFavorite = false;
                foreach (var tag in app["tags"].Children)
                {
                    if (tag.Value == "favorite")
                    {
                        isFavorite = true;
                    }
                    else
                    {
                        appData.Add(tag.Value);
                    }
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

                result.Add(new GameMetadata()
                {
                    Source = new MetadataNameProperty("Steam"),
                    GameId = gameId,
                    Categories = appData.Select(a => new MetadataNameProperty(a)).Cast<MetadataProperty>().ToHashSet(),
                    Hidden = app["hidden"].AsInteger() == 1,
                    Favorite = isFavorite,
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                });
            }

            return result;
        }

        public void ImportSteamCategories(ulong accountId)
        {
            var dialogs = PlayniteApi.Dialogs;
            var resources = PlayniteApi.Resources;
            var db = PlayniteApi.Database;

            if (dialogs.ShowMessage(
                resources.GetString(LOC.SettingsSteamCatImportWarn),
                resources.GetString(LOC.SettingsSteamCatImportWarnTitle),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            if (accountId == 0)
            {
                dialogs.ShowMessage(
                    resources.GetString(LOC.SettingsSteamCatImportErrorAccount),
                    resources.GetString("LOCImportError"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!db.IsOpen)
            {
                dialogs.ShowMessage(
                    resources.GetString(LOC.SettingsSteamCatImportErrorDb),
                    resources.GetString("LOCImportError"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using (db.BufferedUpdate())
                {
                    foreach (var game in GetCategorizedGames(accountId))
                    {
                        var dbGame = db.Games.FirstOrDefault(a => a.PluginId == Id && a.GameId == game.GameId);
                        if (dbGame == null)
                        {
                            continue;
                        }

                        if (game.Categories.HasItems())
                        {
                            dbGame.CategoryIds = db.Categories.Add(game.Categories).Select(a => a.Id).ToList();
                        }
                        else
                        {
                            dbGame.CategoryIds = null;
                        }

                        if (game.Hidden)
                        {
                            dbGame.Hidden = game.Hidden;
                        }

                        if (game.Favorite)
                        {
                            dbGame.Favorite = game.Favorite;
                        }

                        db.Games.Update(dbGame);
                    }
                }

                dialogs.ShowMessage(resources.GetString("LOCImportCompleted"));
            }
            catch (Exception exc) when (!Environment.IsDebugBuild)
            {
                Logger.Error(exc, "Failed to import Steam categories.");
                dialogs.ShowMessage(
                    resources.GetString(LOC.SettingsSteamCatImportError),
                    resources.GetString("LOCImportError"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

            yield return new SteamPlayController(args.Game);
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

            if (SettingsViewModel.Settings.ConnectAccount)
            {
                try
                {
                    var libraryGames = GetLibraryGames(SettingsViewModel.Settings);
                    Logger.Debug($"Found {libraryGames.Count} library Steam games.");

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
