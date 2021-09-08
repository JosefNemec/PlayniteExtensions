using Microsoft.Win32;
using Playnite.SDK;
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

namespace UplayLibrary
{
    [LoadPlugin]
    public class UplayLibrary : LibraryPluginBase<UplayLibrarySettingsViewModel>
    {
        public UplayLibrary(IPlayniteAPI api) : base(
            "Ubisoft Connect",
            Guid.Parse("C2F038E5-8B92-4877-91F1-DA9094155FC5"),
            new LibraryPluginProperties { CanShutdownClient = true, HasSettings = true },
            new UplayClient(),
            Uplay.Icon,
            (_) => new UplayLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new UplayLibrarySettingsViewModel(this, api);
        }

        public List<GameMetadata> GetLibraryGames()
        {
            var games = new List<GameMetadata>();
            var dlcsToIgnore = new List<uint>();
            foreach (var item in Uplay.GetLocalProductCache())
            {
                if (item.root.addons.HasItems())
                {
                    foreach (var dlc in item.root.addons.Select(a => a.id))
                    {
                        dlcsToIgnore.AddMissing(dlc);
                    }
                }

                if (item.root.third_party_platform != null)
                {
                    continue;
                }

                if (item.root.is_ulc)
                {
                    dlcsToIgnore.AddMissing(item.uplay_id);
                    continue;
                }

                if (dlcsToIgnore.Contains(item.uplay_id))
                {
                    continue;
                }

                if (item.root.start_game == null)
                {
                    continue;
                }

                var newGame = new GameMetadata
                {
                    Name = item.root.name.RemoveTrademarks(),
                    GameId = item.uplay_id.ToString(),
                    BackgroundImage = item.root.background_image.IsNullOrEmpty() ? null : new MetadataFile(item.root.background_image),
                    Icon = item.root.icon_image.IsNullOrEmpty() ? null : new MetadataFile(item.root.icon_image),
                    CoverImage = item.root.thumb_image.IsNullOrEmpty() ? null : new MetadataFile(item.root.thumb_image),
                    Source = new MetadataNameProperty("Ubisoft Connect"),
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                };

                games.Add(newGame);
            }

            return games;
        }

        public static List<GameMetadata> GetInstalledGames()
        {
            var games = new List<GameMetadata>();

            var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            var installsKey = root.OpenSubKey(@"SOFTWARE\ubisoft\Launcher\Installs\");
            if (installsKey == null)
            {
                root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                installsKey = root.OpenSubKey(@"SOFTWARE\ubisoft\Launcher\Installs\");
            }

            if (installsKey != null)
            {
                foreach (var install in installsKey.GetSubKeyNames())
                {
                    var gameData = installsKey.OpenSubKey(install);
                    var installDir = (gameData.GetValue("InstallDir") as string)?.Replace('/', Path.DirectorySeparatorChar);
                    if (!installDir.IsNullOrEmpty() && Directory.Exists(installDir))
                    {
                        var newGame = new GameMetadata()
                        {
                            GameId = install,
                            Source = new MetadataNameProperty("Ubisoft Connect"),
                            InstallDirectory = installDir,
                            Name = Path.GetFileName(installDir.TrimEnd(Path.DirectorySeparatorChar)),
                            IsInstalled = true,
                            Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                        };

                        games.Add(newGame);
                    }
                }
            }

            return games;
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var allGames = new List<GameMetadata>();
            var installedGames = new List<GameMetadata>();
            Exception importError = null;

            if (SettingsViewModel.Settings.ImportInstalledGames)
            {
                try
                {
                    installedGames = GetInstalledGames();
                    Logger.Debug($"Found {installedGames.Count} installed Uplay games.");
                    allGames.AddRange(installedGames);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import installed Uplay games.");
                    importError = e;
                }
            }

            if (SettingsViewModel.Settings.ImportUninstalledGames)
            {
                try
                {
                    var libraryGames = GetLibraryGames();
                    Logger.Debug($"Found {libraryGames.Count} library Uplay games.");
                    foreach (var libGame in libraryGames)
                    {
                        var installed = installedGames.FirstOrDefault(a => a.GameId == libGame.GameId);
                        if (installed != null)
                        {
                            installed.Icon = libGame.Icon;
                            installed.BackgroundImage = libGame.BackgroundImage;
                            installed.CoverImage = libGame.CoverImage;
                        }
                        else
                        {
                            allGames.Add(libGame);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import uninstalled Uplay games.");
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

            yield return new UplayInstallController(args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new UplayUninstallController(args.Game);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new UplayPlayController(args.Game);
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new UplayMetadataProvider();
        }
    }
}
