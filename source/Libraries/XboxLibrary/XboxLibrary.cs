using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using XboxLibrary.Models;
using XboxLibrary.Services;

namespace XboxLibrary
{
    [LoadPlugin]
    public class XboxLibrary : LibraryPluginBase<XboxLibrarySettingsViewModel>
    {
        private readonly string pfnInfoCacheDir;

        public override LibraryClient Client => new XboxLibraryClient(SettingsViewModel);

        public XboxLibrary(IPlayniteAPI api) : base(
            "Xbox",
            Guid.Parse("7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287"),
            new LibraryPluginProperties { HasSettings = true },
            null,
            Xbox.Icon,
            (_) => new XboxLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new XboxLibrarySettingsViewModel(this, api);
            pfnInfoCacheDir = Path.Combine(GetPluginUserDataPath(), "PfnInfoCache");
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            SettingsViewModel.IsFirstRunUse = firstRunSettings;
            return SettingsViewModel;
        }

        internal GameMetadata GetGameMetadataFromTitle(TitleHistoryResponse.Title title)
        {
            var newGame = new GameMetadata
            {
                GameId = title.pfn,
                Name = title.name.
                Replace("(PC)", "").
                Replace("(Windows)", "").
                Replace("for Windows 10", "").
                Replace("- Windows 10", "").
                RemoveTrademarks().
                Trim(),
                Source = new MetadataNameProperty("Xbox"),
                Platforms = GetPlatforms(title.devices, out _, out _),
            };

            if (title.detail != null)
            {
                if (title.detail.releaseDate != null)
                {
                    newGame.ReleaseDate = new ReleaseDate(title.detail.releaseDate.Value);
                }

                if (!title.detail.publisherName.IsNullOrEmpty())
                {
                    newGame.Publishers = title.detail.publisherName.Split(new char[] { '|' })
                        .Select(a => new MetadataNameProperty(a.Trim()))
                        .Cast<MetadataProperty>()
                        .ToHashSet();
                }

                if (!title.detail.developerName.IsNullOrEmpty())
                {
                    newGame.Developers = title.detail.developerName.Split(new char[] { '|' })
                        .Select(a => new MetadataNameProperty(a.Trim()))
                        .Cast<MetadataProperty>().
                        ToHashSet();
                }
            }

            return newGame;
        }

        public List<TitleHistoryResponse.Title> GetAppDataCache()
        {
            var items = new List<TitleHistoryResponse.Title>();
            if (Directory.Exists(pfnInfoCacheDir))
            {
                foreach (var file in Directory.GetFiles(pfnInfoCacheDir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var cachCont = Serialization.FromJsonFile<TitleHistoryResponse.Title>(file);
                        if (cachCont == null)
                        {
                            Logger.Error($"Failed to get app info from cache {file}, cache is empty.");
                            File.Delete(file);
                        }
                        else
                        {
                            items.Add(cachCont);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Failed to get app info from cache {file}.");
                    }
                }
            }

            return items;
        }

        public void WriteAppDataCache(TitleHistoryResponse.Title data)
        {
            var filePath = Path.Combine(pfnInfoCacheDir, data.pfn + ".json");
            FileSystem.PrepareSaveFile(filePath);
            File.WriteAllText(filePath, Serialization.ToJson(data));
        }

        private static HashSet<MetadataProperty> GetPlatforms(List<string> devices, out bool containsPC, out bool containsConsole)
        {
            containsPC = false;
            containsConsole = false;
            var output = new HashSet<MetadataProperty>();
            if (devices == null)
            {
                return output;
            }

            foreach (var deviceName in devices)
            {
                switch (deviceName)
                {
                    case "PC":
                        containsPC = true;
                        output.Add(new MetadataSpecProperty("pc_windows"));
                        break;
                    case "Xbox360":
                        containsConsole = true;
                        output.Add(new MetadataSpecProperty("xbox360"));
                        break;
                    case "XboxOne":
                        containsConsole = true;
                        output.Add(new MetadataSpecProperty("xbox_one"));
                        break;
                    case "XboxSeries":
                        containsConsole = true;
                        output.Add(new MetadataSpecProperty("xbox_series"));
                        break;
                }
            }
            return output;
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var installedGames = new Dictionary<string, GameMetadata>();
            Exception importError = null;
            var allGames = new List<GameMetadata>();
            if (!SettingsViewModel.Settings.ConnectAccount)
            {
                return allGames;
            }

            if (Computer.WindowsVersion != WindowsVersion.Win10)
            {
                throw new Exception("Xbox game library is only supported on Windows 10.");
            }

            var titles = new List<TitleHistoryResponse.Title>();
            var client = new XboxAccountClient(this);

            try
            {
                titles = client.GetLibraryTitles().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to Xbox profile titles.");
                importError = e;
            }

            var appDataCache = GetAppDataCache();
            var pcTitles = titles.Where(title => !title.pfn.IsNullOrEmpty() &&
                    title.type == "Game" &&
                    title.devices?.Contains("PC") == true).ToList();

            if (SettingsViewModel.Settings.ImportInstalledGames)
            {
                try
                {
                    var installedApps = Programs.GetUWPApps();
                    foreach (var installedApp in installedApps)
                    {
                        var import = false;
                        var libTitle = pcTitles.FirstOrDefault(a => a.pfn == installedApp.AppId);
                        if (libTitle != null)
                        {
                            import = true;
                        }
                        else // Check if it's a game that was not started at least once (won't appear in user API data)
                        {
                            try
                            {
                                libTitle = appDataCache.FirstOrDefault(a => a.pfn == installedApp.AppId);
                                if (libTitle == null)
                                {
                                    libTitle = client.GetTitleInfo(installedApp.AppId).GetAwaiter().GetResult();
                                    WriteAppDataCache(libTitle);
                                }

                                if (libTitle.type == "Game")
                                {
                                    import = true;
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e, $"Failed to get info about installed UWP package {installedApp.AppId}.");
                            }
                        }

                        if (import)
                        {
                            var game = GetGameMetadataFromTitle(libTitle);
                            game.IsInstalled = true;
                            game.InstallDirectory = installedApp.WorkDir;
                            game.Icon = installedApp.Icon.IsNullOrEmpty() ? null : new MetadataFile(installedApp.Icon);
                            installedGames.Add(libTitle.pfn, game);
                        }
                    }

                    Logger.Debug($"Found {installedGames.Count} installed Xbox games.");
                    allGames.AddRange(installedGames.Values.ToList());
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import installed Xbox games.");
                    importError = e;
                }
            }

            if (SettingsViewModel.Settings.ImportUninstalledGames)
            {
                try
                {
                    Logger.Debug($"Found {pcTitles.Count} Xbox PC games.");
                    foreach (var libTitle in pcTitles)
                    {
                        if (!installedGames.TryGetValue(libTitle.pfn, out var installed))
                        {
                            allGames.Add(GetGameMetadataFromTitle(libTitle));
                        }
                    }

                    foreach (var title in titles)
                    {
                        GetPlatforms(title.devices, out bool containsPC, out bool containsConsole);
                        if (containsConsole && !containsPC && SettingsViewModel.Settings.ImportConsoleGames)
                        {
                            var newGame = GetGameMetadataFromTitle(title);
                            newGame.GameId = $"CONSOLE_{title.titleId}_{title.mediaItemType}";

                            allGames.Add(newGame);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import linked account Xbox games details.");
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
            if (args.Game.PluginId != Id || args.Game.GameId.StartsWith("CONSOLE"))
            {
                yield break;
            }

            yield return new XboxInstallController(args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id || args.Game.GameId.StartsWith("CONSOLE"))
            {
                yield break;
            }

            yield return new XboxUninstallController(args.Game);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id || args.Game.GameId.StartsWith("CONSOLE"))
            {
                yield break;
            }

            yield return new XboxPlayController(args.Game);
        }
    }
}