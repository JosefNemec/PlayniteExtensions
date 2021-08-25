using BattleNetLibrary.Models;
using BattleNetLibrary.Services;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace BattleNetLibrary
{
    [LoadPlugin]
    public class BattleNetLibrary : LibraryPluginBase<BattleNetLibrarySettingsViewModel>
    {
        public BattleNetLibrary(IPlayniteAPI api) : base(
            "Battle.net",
            Guid.Parse("E3C26A3D-D695-4CB7-A769-5FF7612C7EDD"),
            new LibraryPluginProperties { CanShutdownClient = true, HasSettings = true },
            new BattleNetClient(),
            BattleNet.Icon,
            (_) => new BattleNetLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new BattleNetLibrarySettingsViewModel(this, PlayniteApi);
        }

        public static UninstallProgram GetUninstallEntry(BNetApp app)
        {
            foreach (var prog in Programs.GetUnistallProgramsList())
            {
                if (app.Type == BNetAppType.Classic)
                {
                    if (prog.DisplayName == app.InternalId)
                    {
                        return prog;
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(prog.UninstallString))
                    {
                        continue;
                    }

                    var match = Regex.Match(prog.UninstallString, string.Format(@"Battle\.net.*--uid={0}.*\s", app.InternalId));
                    if (match.Success)
                    {
                        return prog;
                    }
                }
            }

            return null;
        }

        public Dictionary<string, GameInfo> GetInstalledGames()
        {
            var games = new Dictionary<string, GameInfo>();
            foreach (var prog in Programs.GetUnistallProgramsList())
            {
                if (string.IsNullOrEmpty(prog.UninstallString))
                {
                    continue;
                }

                if (prog.Publisher == "Blizzard Entertainment" && BattleNetGames.Games.Any(a => a.Type == BNetAppType.Classic && prog.DisplayName == a.InternalId))
                {
                    var products = BattleNetGames.Games.Where(a => a.Type == BNetAppType.Classic && prog.DisplayName == a.InternalId);
                    foreach (var product in products)
                    {
                        if (!Directory.Exists(prog.InstallLocation))
                        {
                            continue;
                        }

                        var game = new GameInfo()
                        {
                            GameId = product.ProductId,
                            Source = "Battle.net",
                            Name = product.Name.RemoveTrademarks(),
                            InstallDirectory = prog.InstallLocation,
                            IsInstalled = true
                        };

                        // Check in case there are more versions of single games installed.
                        if (!games.TryGetValue(game.GameId, out var _))
                        {
                            games.Add(game.GameId, game);
                        }
                    }
                }
                else
                {
                    var match = Regex.Match(prog.UninstallString, @"Battle\.net.*--uid=(.*?)\s");
                    if (!match.Success)
                    {
                        continue;
                    }

                    if (prog.DisplayName.EndsWith("Test") || prog.DisplayName.EndsWith("Beta"))
                    {
                        continue;
                    }

                    var iId = match.Groups[1].Value;
                    var product = BattleNetGames.Games.FirstOrDefault(a => a.Type == BNetAppType.Default && iId.StartsWith(a.InternalId));
                    if (product == null)
                    {
                        continue;
                    }

                    if (!Directory.Exists(prog.InstallLocation))
                    {
                        continue;
                    }

                    var game = new GameInfo()
                    {
                        GameId = product.ProductId,
                        Source = "Battle.net",
                        Name = product.Name.RemoveTrademarks(),
                        InstallDirectory = prog.InstallLocation,
                        IsInstalled = true
                    };

                    // Check in case there are more versions of single games installed.
                    if (!games.TryGetValue(game.GameId, out var _))
                    {
                        games.Add(game.GameId, game);
                    }
                }
            }

            return games;
        }

        public List<GameInfo> GetLibraryGames()
        {
            using (var view = PlayniteApi.WebViews.CreateOffscreenView())
            {
                var api = new BattleNetAccountClient(view);
                var games = new List<GameInfo>();
                if (!api.GetIsUserLoggedIn())
                {
                    throw new Exception("User is not logged in.");
                }

                var accountGames = api.GetOwnedGames();
                if (accountGames?.Any() == true)
                {
                    foreach (var product in accountGames)
                    {
                        var gameInfo = BattleNetGames.Games.FirstOrDefault(a => a.ApiId == product.titleId);
                        if (gameInfo == null)
                        {
                            Logger.Warn($"Unknown game found on the account: {product.localizedGameName}/{product.titleId}, skipping import.");
                            continue;
                        }

                        // To avoid duplicates like multiple WoW accounts
                        if (!games.Any(a => a.GameId == gameInfo.ProductId))
                        {
                            games.Add(new GameInfo()
                            {
                                Source = "Battle.net",
                                GameId = gameInfo.ProductId,
                                Name = gameInfo.Name.RemoveTrademarks()
                            });
                        }
                    }
                }

                var classicGames = api.GetOwnedClassicGames();
                if (classicGames?.Any() == true)
                {
                    // W3
                    var w3Games = classicGames.Where(a => a.regionalGameFranchiseIconFilename.Contains("warcraft-iii"));
                    if (w3Games.Any())
                    {
                        var w3 = BattleNetGames.Games.FirstOrDefault(a => a.ProductId == "W3");
                        games.Add(new GameInfo()
                        {
                            Source = "Battle.net",
                            GameId = w3.ProductId,
                            Name = w3.Name
                        });

                        if (w3Games.Count() == 2)
                        {
                            var w3x = BattleNetGames.Games.FirstOrDefault(a => a.ProductId == "W3X");
                            games.Add(new GameInfo()
                            {
                                Source = "Battle.net",
                                GameId = w3x.ProductId,
                                Name = w3x.Name
                            });
                        }
                    }

                    // D2
                    var d2Games = classicGames.Where(a => a.regionalGameFranchiseIconFilename.Contains("diablo-ii"));
                    if (d2Games.Any())
                    {
                        var d2 = BattleNetGames.Games.FirstOrDefault(a => a.ProductId == "D2");
                        games.Add(new GameInfo()
                        {
                            Source = "Battle.net",
                            GameId = d2.ProductId,
                            Name = d2.Name
                        });

                        if (d2Games.Count() == 2)
                        {
                            var d2x = BattleNetGames.Games.FirstOrDefault(a => a.ProductId == "D2X");
                            games.Add(new GameInfo()
                            {
                                Source = "Battle.net",
                                GameId = d2x.ProductId,
                                Name = d2x.Name
                            });
                        }
                    }
                }

                return games;
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
                    Logger.Debug($"Found {installedGames.Count} installed Battle.net games.");
                    allGames.AddRange(installedGames.Values.ToList());
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import installed Battle.net games.");
                    importError = e;
                }
            }

            if (SettingsViewModel.Settings.ConnectAccount)
            {
                try
                {
                    var libraryGames = GetLibraryGames();
                    Logger.Debug($"Found {libraryGames.Count} library Battle.net games.");

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
                    Logger.Error(e, "Failed to import linked account Battle.net games details.");
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

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new BnetInstallController(args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new BnetUninstallController(args.Game);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new BnetPlayController(args.Game);
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new BattleNetMetadataProvider();
        }
    }
}
