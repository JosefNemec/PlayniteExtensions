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
using System.Threading.Tasks;
using System.Windows.Controls;

namespace BethesdaLibrary
{
    [LoadPlugin]
    public class BethesdaLibrary : LibraryPluginBase<BethesdaLibrarySettingsViewModel>
    {
        public BethesdaLibrary(IPlayniteAPI api) : base(
            "Bethesda",
            Guid.Parse("0E2E793E-E0DD-4447-835C-C44A1FD506EC"),
            new LibraryPluginProperties { CanShutdownClient = true, HasSettings = true },
            new BethesdaClient(),
            Bethesda.Icon,
            (a) => a ? null : new BethesdaLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new BethesdaLibrarySettingsViewModel(this, PlayniteApi);
        }

        public static List<GameMetadata> GetInstalledGames()
        {
            var games = new List<GameMetadata>();

            foreach (var program in Bethesda.GetBethesdaInstallEntried())
            {
                var installDir = program.Path.Trim('"');
                if (!Directory.Exists(installDir))
                {
                    continue;
                }

                var match = Regex.Match(program.UninstallString, @"uninstall\/(\d+)");
                var gameId = match.Groups[1].Value;
                var newGame = new GameMetadata()
                {
                    GameId = gameId,
                    Source = new MetadataNameProperty("Bethesda"),
                    InstallDirectory = installDir,
                    Name = program.DisplayName.RemoveTrademarks(),
                    IsInstalled = true,
                    Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
                };

                games.Add(newGame);
            }

            return games;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return firstRunSettings ? null : SettingsViewModel;
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var allGames = new List<GameMetadata>();
            if (SettingsViewModel.Settings.ImportInstalledGames)
            {
                try
                {
                    var installed = GetInstalledGames();
                    Logger.Debug($"Found {installed.Count} installed Bethesda games.");
                    PlayniteApi.Notifications.Remove(ImportErrorMessageId);
                    return installed;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to import uninstalled Bethesda games.");
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        ImportErrorMessageId,
                        string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                        System.Environment.NewLine + e.Message,
                        NotificationType.Error,
                        () => OpenSettingsView()));
                }
            }

            return allGames;
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new BethesdaInstallController(args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new BethesdaUninstallController(args.Game);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new AutomaticPlayController(args.Game)
            {
                Type = AutomaticPlayActionType.Url,
                TrackingMode = TrackingMode.Directory,
                Name = "Start using Bethesda client",
                TrackingPath = args.Game.InstallDirectory,
                Path = @"bethesdanet://run/" + args.Game.GameId
            };
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new BethesdaMetadataProvider();
        }
    }
}
