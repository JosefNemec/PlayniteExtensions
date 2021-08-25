using GogLibrary.Models;
using GogLibrary.Services;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace GogLibrary
{
    [LoadPlugin]
    public class GogLibrary : LibraryPluginBase<GogLibrarySettingsViewModel>
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public GogLibrary(IPlayniteAPI api) : base(
            "GOG",
            Guid.Parse("AEBE8B7C-6DC3-4A66-AF31-E7375C6B5E9E"),
            new LibraryPluginProperties { CanShutdownClient = true, HasSettings = true },
            new GogClient(),
            Gog.Icon,
            (_) => new GogLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new GogLibrarySettingsViewModel(this, api);
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new GogInstallController(args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new GogUninstallController(args.Game);
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new GogMetadataProvider(PlayniteApi);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            if (!GetInstalledEntries().TryGetValue(args.Game.GameId, out var installEntry))
            {
                throw new DirectoryNotFoundException("Game installation not found.");
            }

            if (SettingsViewModel.Settings.StartGamesUsingGalaxy)
            {
                yield return new AutomaticPlayController(args.Game)
                {
                    Type = AutomaticPlayActionType.File,
                    TrackingMode = TrackingMode.Directory,
                    Name = "Start using Galaxy",
                    TrackingPath = installEntry.InstallDirectory,
                    Arguments = string.Format(@"/gameId={0} /command=runGame /path=""{1}""", args.Game.GameId, installEntry.InstallDirectory),
                    Path = Path.Combine(Gog.InstallationPath, "GalaxyClient.exe")
                };
            }
            else
            {
                foreach (var task in GetPlayTasks(args.Game.GameId, installEntry.InstallDirectory))
                {
                    yield return new AutomaticPlayController(args.Game)
                    {
                        Type = task.Type == GameActionType.URL ? AutomaticPlayActionType.Url : AutomaticPlayActionType.File,
                        TrackingMode = TrackingMode.Process,
                        Arguments = task.Arguments,
                        Path = task.Path,
                        WorkingDir = task.WorkingDir,
                        Name = task.Name
                    };
                }
            }
        }

        internal static List<GameAction> GetPlayTasks(string gameId, string installDir)
        {
            var gameInfoPath = Path.Combine(installDir, string.Format("goggame-{0}.info", gameId));
            if (!File.Exists(gameInfoPath))
            {
                return new List<GameAction>();
            }

            GogGameActionInfo gameTaskData = null;
            try
            {
                gameTaskData = Serialization.FromJsonFile<GogGameActionInfo>(gameInfoPath);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to read install gog game manifest: {gameInfoPath}.");
                return new List<GameAction>();
            }

            var playTasks = gameTaskData.playTasks?.Where(a => a.isPrimary).Select(a => a.ConvertToGenericTask(installDir)).ToList();
            return playTasks ?? new List<GameAction>();
        }

        internal static List<GameAction> GetOtherTasks(string gameId, string installDir)
        {
            var gameInfoPath = Path.Combine(installDir, string.Format("goggame-{0}.info", gameId));
            if (!File.Exists(gameInfoPath))
            {
                return new List<GameAction>();
            }

            GogGameActionInfo gameTaskData = null;
            try
            {
                gameTaskData = Serialization.FromJsonFile<GogGameActionInfo>(gameInfoPath);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to read install gog game manifest: {gameInfoPath}.");
                return new List<GameAction>();
            }

            var otherTasks = new List<GameAction>();
            foreach (var task in gameTaskData.playTasks.Where(a => !a.isPrimary))
            {
                otherTasks.Add(task.ConvertToGenericTask(installDir));
            }

            if (gameTaskData.supportTasks != null)
            {
                foreach (var task in gameTaskData.supportTasks)
                {
                    otherTasks.Add(task.ConvertToGenericTask(installDir));
                }
            }

            return otherTasks;
        }

        internal static Dictionary<string, GameInfo> GetInstalledEntries()
        {
            var games = new Dictionary<string, GameInfo>();
            var programs = Programs.GetUnistallProgramsList();
            foreach (var program in programs)
            {
                var match = Regex.Match(program.RegistryKeyName, @"(\d+)_is1");
                if (!match.Success || program.Publisher != "GOG.com" || program.RegistryKeyName.StartsWith("GOGPACK"))
                {
                    continue;
                }

                if (!Directory.Exists(program.InstallLocation))
                {
                    continue;
                }

                var gameId = match.Groups[1].Value;
                var game = new GameInfo()
                {
                    InstallDirectory = Paths.FixSeparators(program.InstallLocation),
                    GameId = gameId,
                    Source = "GOG",
                    Name = program.DisplayName.RemoveTrademarks(),
                    IsInstalled = true,
                    Platforms = new List<string> { "PC" }
                };

                games.Add(game.GameId, game);
            }

            return games;
        }

        internal static Dictionary<string, GameInfo> GetInstalledGames()
        {
            var games = new Dictionary<string, GameInfo>();
            foreach (var entry in GetInstalledEntries())
            {
                var game = entry.Value;
                if (!GetPlayTasks(game.GameId, game.InstallDirectory).HasItems())
                {
                    continue; // Empty play task = DLC
                }

                game.GameActions = GetOtherTasks(game.GameId, game.InstallDirectory);
                games.Add(game.GameId, game);
            }

            return games;
        }

        internal List<GameInfo> GetLibraryGames()
        {
            using (var view = PlayniteApi.WebViews.CreateOffscreenView())
            {
                var api = new GogAccountClient(view);
                if (!api.GetIsUserLoggedIn())
                {
                    throw new Exception("User is not logged in to GOG account.");
                }

                var libGames = api.GetOwnedGames();
                if (libGames == null)
                {
                    throw new Exception("Failed to obtain libary data.");
                }

                return LibraryGamesToGames(libGames).ToList();
            }
        }

        internal List<GameInfo> GetLibraryGames(string accountName)
        {
            var api = new GogAccountClient(null);
            var games = new List<GameInfo>();
            var libGames = api.GetOwnedGamesFromPublicAccount(accountName);
            if (libGames == null)
            {
                throw new Exception("Failed to obtain libary data.");
            }

            return LibraryGamesToGames(libGames).ToList();
        }

        internal IEnumerable<GameInfo> LibraryGamesToGames(List<LibraryGameResponse> libGames)
        {
            foreach (var game in libGames)
            {
                var newGame = new GameInfo()
                {
                    Source = "GOG",
                    GameId = game.game.id,
                    Name = game.game.title.RemoveTrademarks(),
                    Links = new List<Link>()
                    {
                        new Link("Store", @"https://www.gog.com" + game.game.url)
                    },
                    Platforms = new List<string> { "PC" }
                };

                if (game.stats?.Keys?.Any() == true)
                {
                    var acc = game.stats.Keys.First();
                    newGame.Playtime = Convert.ToUInt64(game.stats[acc].playtime * 60);
                    newGame.LastActivity = game.stats[acc].lastSession;
                }

                yield return newGame;
            }
        }

        public override IEnumerable<GameInfo> GetGames(LibraryGetGamesArgs args)
        {
            var allGames = new List<GameInfo>();
            var installedGames = new Dictionary<string, GameInfo>();
            Exception importError = null;

            if (SettingsViewModel.Settings.ImportInstalledGames)
            {
                try
                {
                    installedGames = GetInstalledGames();
                    Logger.Debug($"Found {installedGames.Count} installed GOG games.");
                    allGames.AddRange(installedGames.Values.ToList());
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import installed GOG games.");
                    importError = e;
                }
            }

            if (SettingsViewModel.Settings.ConnectAccount)
            {
                try
                {
                    var libraryGames = GetLibraryGames();
                    Logger.Debug($"Found {libraryGames.Count} library GOG games.");

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
                    Logger.Error(e, "Failed to import linked account GOG games details.");
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
    }
}
