using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Playnite;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace GogLibrary
{
    public class GogInstallController : InstallController
    {
        private CancellationTokenSource watcherToken;
        private readonly GogLibrary gogLibrary;

        public GogInstallController(Game game, GogLibrary gogLibrary) : base(game)
        {
            this.gogLibrary = gogLibrary;
            if (Gog.IsInstalled)
            {
                Name = "Install using Galaxy";
            }
            else
            {
                Name = "Dowload offline installer";
            }
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            if (Gog.IsInstalled)
            {
                var startedAutomaticInstall = false;
                if (gogLibrary.SettingsViewModel.Settings.UseAutomaticGameInstalls)
                {
                    var clientPath = Gog.ClientInstallationPath;
                    if (FileSystem.FileExists(clientPath))
                    {
                        var arguments = string.Format(@"/gameId={0} /command=installGame", Game.GameId);
                        ProcessStarter.StartProcess(clientPath, arguments);

                        // Starting a game install via command will add the game to a queue but Galaxy itself
                        // won't be started to initiate the download process so we need to start it if it's
                        // not running already
                        if (!Gog.IsRunning)
                        {
                            ProcessStarter.StartProcess(clientPath);
                        }

                        startedAutomaticInstall = true;
                    }
                }

                if (!startedAutomaticInstall)
                {
                    ProcessStarter.StartUrl(@"goggalaxy://openGameView/" + Game.GameId);
                }
            }
            else
            {
                ProcessStarter.StartUrl(@"https://www.gog.com/account");
            }

            StartInstallWatcher();
        }

        public async void StartInstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            await Task.Run(async () =>
            {
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var games = GogLibrary.GetInstalledGames();
                    if (games.ContainsKey(Game.GameId))
                    {
                        var game = games[Game.GameId];
                        var installInfo = new GameInstallationData()
                        {
                            InstallDirectory = game.InstallDirectory
                        };

                        InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                        return;
                    }

                    await Task.Delay(10000);
                }
            });
        }
    }

    public class GogUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;

        public GogUninstallController(Game game) : base(game)
        {
            Name = "Uninstall";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            Dispose();
            var uninstaller = Path.Combine(Game.InstallDirectory, "unins000.exe");
            if (!File.Exists(uninstaller))
            {
                throw new FileNotFoundException("Uninstaller not found.");
            }

            Process.Start(uninstaller);
            StartUninstallWatcher();
        }

        public async void StartUninstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            while (true)
            {
                if (watcherToken.IsCancellationRequested)
                {
                    return;
                }

                var games = GogLibrary.GetInstalledGames();
                if (!games.ContainsKey(Game.GameId))
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(5000);
            }
        }
    }
}
