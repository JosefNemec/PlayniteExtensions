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
            new LibraryPluginCapabilities { CanShutdownClient = true },
            new BethesdaClient(),
            Bethesda.Icon,
            (a) => a ? null : new BethesdaLibrarySettingsView(),
            (g) => new BethesdaGameController(g),
            () => new BethesdaMetadataProvider(),
            api)
        {
            SettingsViewModel = new BethesdaLibrarySettingsViewModel(this, PlayniteApi);
        }

        public static GameAction GetGamePlayTask(string id)
        {
            return new GameAction()
            {
                Type = GameActionType.URL,
                Path = @"bethesdanet://run/" + id,
                IsHandledByPlugin = true
            };
        }

        public static List<GameInfo> GetInstalledGames()
        {
            var games = new List<GameInfo>();

            foreach (var program in Bethesda.GetBethesdaInstallEntried())
            {
                var installDir = program.Path.Trim('"');
                if (!Directory.Exists(installDir))
                {
                    continue;
                }

                var match = Regex.Match(program.UninstallString, @"uninstall\/(\d+)");
                var gameId = match.Groups[1].Value;
                var newGame = new GameInfo()
                {
                    GameId = gameId,
                    Source = "Bethesda",
                    InstallDirectory = installDir,
                    PlayAction = GetGamePlayTask(gameId),
                    Name = program.DisplayName.RemoveTrademarks(),
                    IsInstalled = true,
                    Platform = "PC"
                };

                games.Add(newGame);
            }

            return games;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return firstRunSettings ? null : SettingsViewModel;
        }

        public override IEnumerable<GameInfo> GetGames()
        {
            var allGames = new List<GameInfo>();
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
    }
}
