using Microsoft.Win32;
using OriginLibrary.Models;
using OriginLibrary.Services;
using Playnite;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace OriginLibrary
{
    [LoadPlugin]
    public class OriginLibrary : LibraryPluginBase<OriginLibrarySettingsViewModel>
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly string installManifestCacheDir;
        private static readonly string fakeOfferType = "none";

        public class InstallPackage
        {
            public string OriginalId { get; set; }
            public string ConvertedId { get; set; }
            public string Source { get; set; }
        }

        public class PlatformPath
        {
            public string CompletePath { get; set; }
            public string Root { get; set; }
            public string Path { get; set; }

            public PlatformPath(string completePath)
            {
                CompletePath = completePath;
            }

            public PlatformPath(string root, string path)
            {
                Root = root;
                Path = path;
                CompletePath = System.IO.Path.Combine(root, path);
            }
        }

        public OriginLibrary(IPlayniteAPI api) : base(
            "EA app",
            Guid.Parse("85DD7072-2F20-4E76-A007-41035E390724"),
            new LibraryPluginProperties { CanShutdownClient = true, HasSettings = true },
            new OriginClient(),
            Origin.Icon,
            (_) => new OriginLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new OriginLibrarySettingsViewModel(this, PlayniteApi);
            installManifestCacheDir = Path.Combine(GetPluginUserDataPath(), "installmanifests");
        }

        internal PlatformPath GetPathFromPlatformPath(string path, RegistryView platformView)
        {
            if (!path.StartsWith("["))
            {
                return new PlatformPath(path);
            }

            var matchPath = Regex.Match(path, @"\[(.*?)\\(.*)\\(.*)\](.*)");
            if (!matchPath.Success)
            {
                Logger.Warn("Unknown path format " + path);
                return null;
            }

            var root = matchPath.Groups[1].Value;
            RegistryKey rootKey = null;

            switch (root)
            {
                case "HKEY_LOCAL_MACHINE":
                    rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, platformView);
                    break;

                case "HKEY_CURRENT_USER":
                    rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, platformView);
                    break;

                default:
                    throw new Exception("Unknown registry root entry " + root);
            }

            var subPath = matchPath.Groups[2].Value.Trim(Path.DirectorySeparatorChar);
            var key = matchPath.Groups[3].Value;
            var executable = matchPath.Groups[4].Value.Trim(Path.DirectorySeparatorChar);
            var subKey = rootKey.OpenSubKey(subPath);
            if (subKey == null)
            {
                return null;
            }

            var keyValue = rootKey.OpenSubKey(subPath).GetValue(key);
            if (keyValue == null)
            {
                return null;
            }

            return new PlatformPath(keyValue.ToString(), executable);
        }

        internal PlatformPath GetPathFromPlatformPath(string path)
        {
            var resultPath = GetPathFromPlatformPath(path, RegistryView.Registry64);
            if (resultPath == null)
            {
                resultPath = GetPathFromPlatformPath(path, RegistryView.Registry32);
            }

            return resultPath;
        }

        private System.Collections.Specialized.NameValueCollection ParseOriginManifest(string path)
        {
            var text = File.ReadAllText(path);
            var data = HttpUtility.UrlDecode(text);
            return HttpUtility.ParseQueryString(data);
        }

        internal GameInstallerData GetGameInstallerData(string dataPath)
        {
            try
            {
                if (File.Exists(dataPath))
                {
                    var ser = new XmlSerializer(typeof(GameInstallerData));
                    return (GameInstallerData)ser.Deserialize(XmlReader.Create(dataPath));
                }
                else
                {
                    var rootDir = dataPath;
                    for (int i = 0; i < 4; i++)
                    {
                        var target = Path.Combine(rootDir, "__Installer");
                        if (Directory.Exists(target))
                        {
                            rootDir = target;
                            break;
                        }
                        else
                        {
                            rootDir = Path.Combine(rootDir, "..");
                        }
                    }

                    var instPath = Path.Combine(rootDir, "installerdata.xml");
                    if (File.Exists(instPath))
                    {
                        var ser = new XmlSerializer(typeof(GameInstallerData));
                        return (GameInstallerData)ser.Deserialize(XmlReader.Create(instPath));
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to deserialize game installer xml {dataPath}.");
            }

            return null;
        }

        internal GameLocalDataResponse GetLocalInstallerManifest(string id)
        {
            GameLocalDataResponse manifest = null;
            var manifestCacheFile = Path.Combine(installManifestCacheDir, Paths.GetSafePathName(id) + ".json");
            if (File.Exists(manifestCacheFile))
            {
                try
                {
                    manifest = Serialization.FromJsonFile<GameLocalDataResponse>(manifestCacheFile);
                    if (manifest != null)
                    {
                        return manifest;
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to read installer manifest cache file.");
                }
            }

            string origContent = null;
            try
            {
                manifest = OriginApiClient.GetGameLocalData(id, out origContent);
            }
            catch (WebException exc) when ((exc.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                logger.Warn($"EA manifest {id} not found on EA server, generating fake manifest.");
                manifest = new GameLocalDataResponse
                {
                    offerId = id,
                    offerType = fakeOfferType
                };
            }

            FileSystem.PrepareSaveFile(manifestCacheFile);
            File.WriteAllText(manifestCacheFile, origContent ?? Serialization.ToJson(manifest));
            return manifest;
        }

        public GameAction GetGamePlayTask(string installerDataPath)
        {
            var data = GetGameInstallerData(installerDataPath);
            if (data == null)
            {
                return null;
            }
            else
            {
                var launcher = data.runtime.launchers.FirstOrDefault(a => !a.trial);
                if (data.runtime.launchers.Count > 1)
                {
                    if (System.Environment.Is64BitOperatingSystem)
                    {
                        var s4 = data.runtime.launchers.FirstOrDefault(a => a.requires64BitOS && !a.trial);
                        if (s4 != null)
                        {
                            launcher = s4;
                        }
                    }
                    else
                    {
                        var s3 = data.runtime.launchers.FirstOrDefault(a => !a.requires64BitOS && !a.trial);
                        if (s3 != null)
                        {
                            launcher = s3;
                        }
                    }
                }

                var paths = GetPathFromPlatformPath(launcher.filePath);
                if (paths.CompletePath.Contains(@"://"))
                {
                    return new GameAction
                    {
                        Type = GameActionType.URL,
                        Path = paths.CompletePath
                    };
                }
                else
                {
                    var action = new GameAction
                    {
                        Type = GameActionType.File
                    };
                    if (paths.Path.IsNullOrEmpty())
                    {
                        action.Path = paths.CompletePath;
                        action.WorkingDir = Path.GetDirectoryName(paths.CompletePath);
                    }
                    else
                    {
                        action.Path = paths.CompletePath;
                        action.WorkingDir = paths.Root;
                    }

                    return action;
                }
            }
        }

        public GameAction GetGamePlayTask(GameLocalDataResponse manifest)
        {
            var platform = manifest.publishing.softwareList.software.FirstOrDefault(a => a.softwarePlatform == "PCWIN");
            var playAction = new GameAction();
            if (string.IsNullOrEmpty(platform.fulfillmentAttributes.executePathOverride))
            {
                return null;
            }

            if (platform.fulfillmentAttributes.executePathOverride.Contains(@"://"))
            {
                playAction.Type = GameActionType.URL;
                playAction.Path = platform.fulfillmentAttributes.executePathOverride;
            }
            else
            {
                var executePath = GetPathFromPlatformPath(platform.fulfillmentAttributes.executePathOverride);
                if (executePath != null)
                {
                    if (executePath.CompletePath.EndsWith("installerdata.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        return GetGamePlayTask(executePath.CompletePath);
                    }
                    else if (executePath.CompletePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        playAction.WorkingDir = executePath.Root;
                        playAction.Path = executePath.CompletePath;
                    }
                    else
                    {
                        // This happens in case of Sims 4 for example, where executable path points to generic ClientFullBuild0.package file
                        // so the actual executable needs to be forced to installerdata.xaml resolution.
                        return GetGamePlayTask(Path.GetDirectoryName(executePath.CompletePath));
                    }
                }
            }

            return playAction;
        }

        public GameAction GetGamePlayTaskForGameId(string gameId)
        {
            var installManifest = GetLocalInstallerManifest(gameId);
            if (installManifest.offerType == fakeOfferType)
            {
                return new GameAction
                {
                    Type = GameActionType.URL,
                    Path = Origin.LibraryOpenUri
                };
            }
            else
            {
                return GetGamePlayTask(installManifest);
            }
        }

        public string GetInstallDirectory(GameLocalDataResponse localData)
        {
            var platform = localData.publishing.softwareList.software.FirstOrDefault(a => a.softwarePlatform == "PCWIN");
            if (platform == null)
            {
                return null;
            }

            var installPath = GetPathFromPlatformPath(platform.fulfillmentAttributes.installCheckOverride);
            if (installPath == null ||
                installPath.CompletePath.IsNullOrEmpty() ||
                !File.Exists(installPath.CompletePath))
            {
                return null;
            }

            var action = GetGamePlayTask(localData);
            if (action?.Type == GameActionType.File)
            {
                return action.WorkingDir;
            }
            else
            {
                return Path.GetDirectoryName(installPath.CompletePath);
            }
        }

        public Dictionary<string, GameMetadata> GetInstalledGames(CancellationToken cancelToken, List<GameMetadata> userGames)
        {
            var games = new Dictionary<string, GameMetadata>();
            foreach (var userGame in userGames)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var newGame = new GameMetadata()
                    {
                        Source = new MetadataNameProperty("EA app"),
                        GameId = userGame.GameId,
                        IsInstalled = true,
                        Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                    };

                    var localData = GetLocalInstallerManifest(userGame.GameId);
                    if (localData.offerType == fakeOfferType)
                    {
                        continue;
                    }

                    if (localData.offerType != "Base Game" && localData.offerType != "DEMO")
                    {
                        continue;
                    }

                    newGame.Name = StringExtensions.NormalizeGameName(localData.localizableAttributes.displayName);
                    var installDir = GetInstallDirectory(localData);
                    if (installDir.IsNullOrEmpty())
                    {
                        continue;
                    }

                    newGame.InstallDirectory = installDir;
                    // Games can be duplicated if user has EA Play sub and also bought the game.
                    if (!games.TryGetValue(newGame.GameId, out var _))
                    {
                        games.Add(newGame.GameId, newGame);
                    }
                }
                catch (Exception e) when (!Environment.IsDebugBuild)
                {
                    logger.Error(e, $"Failed to import installed EA game {userGame.GameId}.");
                }
            }

            return games;
        }

        public List<GameMetadata> GetLibraryGames(CancellationToken cancelToken)
        {
            using (var view = PlayniteApi.WebViews.CreateOffscreenView())
            {
                var api = new OriginAccountClient(view);

                if (!api.GetIsUserLoggedIn())
                {
                    throw new Exception("User is not logged in.");
                }

                var token = api.GetAccessToken();
                if (token == null)
                {
                    throw new Exception("Failed to get access to user account.");
                }

                if (!string.IsNullOrEmpty(token.error))
                {
                    throw new Exception("Access error: " + token.error);
                }

                var info = api.GetAccountInfo(token);
                if (!string.IsNullOrEmpty(info.error))
                {
                    throw new Exception("Access error: " + info.error);
                }

                var games = new List<GameMetadata>();

                foreach (var game in api.GetOwnedGames(info.pid.pidId, token).Where(a => a.offerType == "basegame"))
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    UsageResponse usage = null;
                    try
                    {
                        usage = api.GetUsage(info.pid.pidId, game.offerId, token);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Failed to get usage data for {game.offerId}");
                    }

                    var gameName = game.offerId;
                    try
                    {
                        var localData = GetLocalInstallerManifest(game.offerId);
                        if (localData != null)
                        {
                            gameName = StringExtensions.NormalizeGameName(localData.localizableAttributes.displayName);
                        }
                    }
                    catch (Exception e) when (!Environment.IsDebugBuild)
                    {
                        Logger.Error(e, $"Failed to get Origin manifest for a {game.offerId}");
                        continue;
                    }

                    games.Add(new GameMetadata()
                    {
                        Source = new MetadataNameProperty("EA app"),
                        GameId = game.offerId,
                        Name = gameName,
                        LastActivity = usage?.lastSessionEndTimeStamp,
                        Playtime = (ulong)(usage?.total ?? 0),
                        Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                    });
                }

                return games;
            }
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            if (!SettingsViewModel.Settings.ConnectAccount)
            {
                return null;
            }

            var allGames = new List<GameMetadata>();
            var installedGames = new Dictionary<string, GameMetadata>();
            Exception importError = null;

            try
            {
                allGames = GetLibraryGames(args.CancelToken);
                Logger.Debug($"Found {allGames.Count} library EA games.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to import linked account EA games details.");
                importError = e;
            }

            if (importError == null)
            {
                if (SettingsViewModel.Settings.ImportInstalledGames)
                {
                    try
                    {
                        installedGames = GetInstalledGames(args.CancelToken, allGames);
                        Logger.Debug($"Found {installedGames.Count} installed EA games.");
                        foreach (var installedGame in installedGames.Values)
                        {
                            var libraryGame = allGames.First(a => a.GameId == installedGame.GameId);
                            allGames.Remove(libraryGame);
                            installedGame.Playtime = libraryGame.Playtime;
                            installedGame.LastActivity = libraryGame.LastActivity;
                            allGames.Add(installedGame);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Failed to import installed EA games.");
                        importError = e;
                    }
                }

                if (!SettingsViewModel.Settings.ImportUninstalledGames)
                {
                    allGames.RemoveAll(a => !a.IsInstalled);
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

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new OriginInstallController(args.Game, this);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new OriginUninstallController(args.Game, this);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new OriginPlayController(args.Game, this);
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new OriginMetadataProvider(PlayniteApi);
        }
    }
}
