using HumbleLibrary.Models;
using HumbleLibrary.Services;
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
using System.Threading.Tasks;
using System.Windows.Controls;

namespace HumbleLibrary
{
    public class ImportableTroveGame : GameMetadata
    {
        public TroveGame TroveData { get; set; }
    }

    public class InstalledTroveGame : Game
    {
        public string Executable { get; set; }
    }

    public static class Extensions
    {
        public static bool SupportsHumbleApp(this Game game)
        {
            return game.GameId.EndsWith("_trove", StringComparison.OrdinalIgnoreCase) || game.GameId.EndsWith("_collection", StringComparison.OrdinalIgnoreCase);
        }
    }

    [LoadPlugin]
    public class HumbleLibrary : LibraryPluginBase<HumbleLibrarySettingsViewModel>
    {
        public string UserAgent { get; }

        public HumbleLibrary(IPlayniteAPI api) : base(
            "Humble",
            Guid.Parse("96e8c4bc-ec5c-4c8b-87e7-18ee5a690626"),
            new LibraryPluginProperties { CanShutdownClient = false, HasCustomizedGameImport = true, HasSettings = true },
            new HumbleClient(),
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Resources", "icon.png"),
            (_) => new HumbleLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new HumbleLibrarySettingsViewModel(this, PlayniteApi);
            UserAgent = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36 Playnite/{api.ApplicationInfo.ApplicationVersion.ToString(2)}";
        }

        public List<InstalledTroveGame> GetInstalledGames()
        {
            if (!Client.IsInstalled)
            {
                throw new Exception("Humble App installation not found.");
            }

            var configPath = HumbleClient.HumbleConfigPath;
            if (!File.Exists(configPath))
            {
                throw new Exception("Humble config file not found.");
            }

            HumbleAppConfig appConfig = null;
            try
            {
                appConfig = Serialization.FromJsonFile<HumbleAppConfig>(configPath);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed read Humble App config.");
                throw new Exception("Failed read Humble App config.");
            }

            var games = new List<InstalledTroveGame>();
            foreach (var entry in appConfig.GameCollection4.Where(a => a.status == "downloaded" || a.status == "installed"))
            {
                string exePath = string.Empty;
                if (!entry.filePath.IsNullOrEmpty())
                {
                    exePath = Paths.FixSeparators(Path.Combine(entry.filePath, entry.executablePath));
                }
                else
                {
                    // This is for installs created by older Humble App versions
                    exePath = Paths.FixSeparators(Path.Combine(entry.downloadFilePath, entry.machineName, entry.executablePath));
                }

                var installDir = Path.GetDirectoryName(exePath);
                if (!Directory.Exists(installDir))
                {
                    continue;
                }

                games.Add(new InstalledTroveGame
                {
                    Name = entry.gameName,
                    GameId = entry.machineName,
                    InstallDirectory = installDir,
                    Executable = exePath,
                    IsInstalled = true
                });
            }

            return games;
        }

        private static string GetGameId(Order.SubProduct product)
        {
            return $"{product.machine_name}_{product.human_name}";
        }

        private static string GetOldTroveGameId(TroveGame troveGame)
        {
            return $"{troveGame.machine_name}_{troveGame.human_name}_TROVE";
        }

        public List<ImportableTroveGame> GetTroveGames()
        {
            var catalogUrlBase = @"https://humblebundle.com/client/catalog?index=";
            var games = new List<ImportableTroveGame>();
            using (var webClient = new WebClient { Encoding = Encoding.UTF8 })
            {
                webClient.Headers["User-Agent"] = UserAgent;
                var index = 0;
                var pageData = webClient.DownloadString(catalogUrlBase + index);
                while (!pageData.IsNullOrEmpty())
                {
                    if (Serialization.TryFromJson<List<TroveGame>>(pageData, out var troveGames))
                    {
                        if (!troveGames.HasItems())
                        {
                            break;
                        }

                        foreach (var troveGame in troveGames)
                        {
                            var game = new ImportableTroveGame
                            {
                                TroveData = troveGame,
                                Name = troveGame.human_name.RemoveTrademarks(),
                                GameId = troveGame.machine_name,
                                Description = troveGame.description_text,
                                Publishers = troveGame.publishers?.Select(a => new MetadataNameProperty(a.publisher_name)).Cast<MetadataProperty>().ToHashSet(),
                                Developers = troveGame.developers?.Select(a => new MetadataNameProperty(a.developer_name)).Cast<MetadataProperty>().ToHashSet(),
                                Source = new MetadataNameProperty("Humble"),
                                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                            };

                            games.Add(game);
                        }
                    }
                    else
                    {
                        Logger.Error("Failed to parse trove games page data:");
                        Logger.Debug(pageData);
                        break;
                    }

                    index++;
                    pageData = webClient.DownloadString(catalogUrlBase + index);
                }
            }

            return games.OrderBy(a => a.Name).ToList();
        }

        public List<Order.SubProduct> GetLibraryGames(List<Order> orders)
        {
            var selectedProducts = new List<Order.SubProduct>();
            var allTpks = orders.SelectMany(a => a.tpkd_dict?.all_tpks).ToList();

            foreach (var order in orders)
            {
                if (order.subproducts.HasItems())
                {
                    foreach (var product in order.subproducts)
                    {
                        var alreadyAdded = selectedProducts.FirstOrDefault(a => a.human_name == product.human_name);
                        if (alreadyAdded != null)
                        {
                            continue;
                        }

                        if (product.downloads?.Any(a => a.platform == "windows") == true)
                        {
                            if (SettingsViewModel.Settings.IgnoreThirdPartyStoreGames && order.tpkd_dict?.all_tpks.HasItems() == true)
                            {
                                var exst = allTpks.FirstOrDefault(a =>
                                    !a.human_name.IsNullOrEmpty() &&
                                    (a.human_name.Equals(product.human_name, StringComparison.OrdinalIgnoreCase) ||
                                    Regex.IsMatch(a.human_name, Regex.Escape(product.human_name) + @".+\sKey$", RegexOptions.IgnoreCase) ||
                                    Regex.IsMatch(a.human_name, Regex.Escape(product.human_name) + @".*\s\(?Steam\)?$", RegexOptions.IgnoreCase) ||
                                    Regex.IsMatch(a.human_name + @"\s*+$", Regex.Escape(product.human_name), RegexOptions.IgnoreCase)));

                                if (exst != null && !SettingsViewModel.Settings.ImportThirdPartyDrmFree)
                                {
                                    continue;
                                }
                            }

                            selectedProducts.Add(product);
                        }
                    }
                }
            }

            return selectedProducts;
        }

        public List<GameMetadata> GetLibraryExtras(List<Order> orders)
        {
            var extras = new List<GameMetadata>();
            foreach (var order in orders)
            {
                var gameKey = order.gamekey;
                if (!order.subproducts.HasItems())
                {
                    continue;
                }

                foreach (var product in order.subproducts)
                {
                    var productName = product.human_name.RemoveTrademarks();
                    if (!product.downloads.HasItems())
                    {
                        continue;
                    }

                    foreach (var download in product.downloads)
                    {
                        var downloadName = download.machine_name;
                        if (!download.download_struct.HasItems())
                        {
                            continue;
                        }

                        switch (download.platform)
                        {
                            case "windows":
                            case "mac":
                            case "linux":
                                continue;
                            case "asmjs":
                                var extraAsGame = new GameMetadata()
                                {
                                    GameId = $"{productName}_{downloadName}",
                                    Source = new MetadataNameProperty("Humble"),
                                    Name = $"{productName} asm.js version",
                                    Categories = new HashSet<MetadataProperty>(){new MetadataNameProperty("extras")},
                                    Links = new List<Link>(){new Link("shell:open", $"https://www.humblebundle.com/play/asmjs/{downloadName}/{gameKey}")}
                                };
                                extras.Add(extraAsGame);
                                continue;
                        }

                        foreach (var actualDownload in download.download_struct)
                        {
                            var url = actualDownload.url.web;
                            if (string.IsNullOrWhiteSpace(url))
                            {
                                continue;
                            }
                            var extraAsGame = new GameMetadata()
                            {
                                GameId = $"{productName}_{downloadName}_{actualDownload.name}",
                                Source = new MetadataNameProperty("Humble"),
                                Name = $"{productName} {download.platform} {actualDownload.name}",
                                Categories = new HashSet<MetadataProperty>(){new MetadataNameProperty("extras")},
                                Links = new List<Link>(){new Link("shell:open", url)}
                            };
                            extras.Add(extraAsGame);
                        }


                    }
                }
            }

            return extras;
        }

        private List<Order> GetAllOrders()
        {
            using (var view = PlayniteApi.WebViews.CreateOffscreenView(
                       new WebViewSettings
                       {
                           JavaScriptEnabled = false,
                           UserAgent = UserAgent
                       }))
            {
                var api = new HumbleAccountClient(view);
                var keys = api.GetLibraryKeys();
                var orders = api.GetOrders(keys);
                // Humble will return null items in library response on some accounts
                return orders.Where(a => a != null).ToList();
            }
        }

        public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args)
        {
            var importedGames = new List<Game>();
            Exception importError = null;
            if (!SettingsViewModel.Settings.ConnectAccount)
            {
                return importedGames;
            }

            try
            {
                using (PlayniteApi.Database.BufferedUpdate())
                {
                    if (SettingsViewModel.Settings.ImportGeneralLibrary)
                    {
                        var orders = GetAllOrders();
                        var gameProducts = GetLibraryGames(orders);
                        foreach (var product in gameProducts)
                        {
                            var gameId = GetGameId(product);
                            if (PlayniteApi.ApplicationSettings.GetGameExcludedFromImport(gameId, Id))
                            {
                                continue;
                            }

                            var alreadyImported = PlayniteApi.Database.Games.FirstOrDefault(a => a.PluginId == Id && a.GameId == gameId);
                            if (alreadyImported == null)
                            {
                                importedGames.Add(PlayniteApi.Database.ImportGame(new GameMetadata()
                                {
                                    Name = product.human_name.RemoveTrademarks(),
                                    GameId = gameId,
                                    Icon = product.icon.IsNullOrEmpty() ? null : new MetadataFile(product.icon),
                                    Source = new MetadataNameProperty("Humble")
                                }, this));
                            }
                        }

                        if (SettingsViewModel.Settings.ImportGameExtras)
                        {
                            var extras = GetLibraryExtras(orders);
                            foreach (var extra in extras)
                            {
                                var alreadyImported = PlayniteApi.Database.Games.FirstOrDefault(a => a.PluginId == Id && a.GameId == extra.GameId);
                                if (alreadyImported == null)
                                {
                                    importedGames.Add(PlayniteApi.Database.ImportGame(extra, this));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to import general library Humble games.");
                importError = e;
            }

            try
            {
                if (SettingsViewModel.Settings.ImportTroveGames)
                {
                    using (PlayniteApi.Database.BufferedUpdate())
                    {
                        var installedGames = GetInstalledGames();
                        foreach (var troveGame in GetTroveGames())
                        {
                            var installed = installedGames.FirstOrDefault(a => a.GameId == troveGame.GameId);
                            if (installed != null)
                            {
                                troveGame.IsInstalled = installed.IsInstalled;
                                troveGame.InstallDirectory = installed.InstallDirectory;
                            }

                            var alreadyImported = PlayniteApi.Database.Games.FirstOrDefault(a => a.PluginId == Id && a.GameId == troveGame.GameId);
                            if (alreadyImported == null)
                            {
                                // Need to check for old pre-Humble App trove IDs and fix them
                                var oldId = GetOldTroveGameId(troveGame.TroveData);
                                alreadyImported = PlayniteApi.Database.Games.FirstOrDefault(a => a.PluginId == Id && a.GameId == oldId);
                                if (alreadyImported != null)
                                {
                                    Logger.Warn($"{alreadyImported.GameId} -> {troveGame.GameId}");
                                    alreadyImported.GameId = troveGame.GameId;
                                    PlayniteApi.Database.Games.Update(alreadyImported);
                                }
                            }

                            if (alreadyImported == null)
                            {
                                if (PlayniteApi.ApplicationSettings.GetGameExcludedFromImport(troveGame.GameId, Id))
                                {
                                    continue;
                                }

                                importedGames.Add(PlayniteApi.Database.ImportGame(troveGame, this));
                            }
                            else
                            {
                                if (installed != null && (alreadyImported.IsInstalled != troveGame.IsInstalled || alreadyImported.InstallDirectory != troveGame.InstallDirectory))
                                {
                                    alreadyImported.IsInstalled = troveGame.IsInstalled;
                                    alreadyImported.InstallDirectory = troveGame.InstallDirectory;
                                    PlayniteApi.Database.Games.Update(alreadyImported);
                                }
                                else if (installed == null && alreadyImported.IsInstalled)
                                {
                                    alreadyImported.IsInstalled = false;
                                    alreadyImported.InstallDirectory = null;
                                    PlayniteApi.Database.Games.Update(alreadyImported);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to import Humble Trove games.");
                importError = e;
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

            return importedGames;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return SettingsViewModel;
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            if (args.Game.SupportsHumbleApp())
            {
                if (SettingsViewModel.Settings.LaunchViaHumbleApp)
                {
                    yield return new AutomaticPlayController(args.Game)
                    {
                        Type = AutomaticPlayActionType.Url,
                        TrackingMode = TrackingMode.Directory,
                        TrackingPath = args.Game.InstallDirectory,
                        Name = ResourceProvider.GetString(LOC.HumbleStartUsingClient).Format("Humble"),
                        Path = $"humble://launch/{args.Game.GameId}"
                    };
                }
                else
                {
                    var installed = GetInstalledGames().FirstOrDefault(a => a.GameId == args.Game.GameId);
                    if (installed != null)
                    {
                        yield return new AutomaticPlayController(args.Game)
                        {
                            Type = AutomaticPlayActionType.File,
                            TrackingMode = TrackingMode.Directory,
                            TrackingPath = installed.InstallDirectory,
                            Name = $"Start {args.Game.Name}",
                            WorkingDir = installed.InstallDirectory,
                            Path = installed.Executable
                        };
                    }
                }
            }
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new HumbleInstallController(args.Game, this);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new HumbleUninstallController(args.Game, this);
        }
    }
}
