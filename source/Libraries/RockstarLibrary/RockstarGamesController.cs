using Playnite;
using Playnite.Common;
using Playnite.SDK.Plugins;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK;

namespace RockstarGamesLibrary
{
    public class RockstarInstallController : InstallController
    {
        private CancellationTokenSource watcherToken;

        public RockstarInstallController(Game game) : base(game)
        {
            Name = "Install using Rockstar client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            Dispose();
            RockstarGames.StartClient();
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

                    var installedGame = RockstarGamesLibrary.GetInstalledGames().FirstOrDefault(a => a.GameId == Game.GameId);
                    if (installedGame != null)
                    {
                        var installInfo = new GameInstallationData
                        {
                            InstallDirectory = installedGame.InstallDirectory
                        };

                        InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                        return;
                    }

                    await Task.Delay(10000);
                }
            });
        }
    }

    public class RockstarUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;

        public RockstarUninstallController(Game game) : base(game)
        {
            Name = "Uninstall using Rockstar client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            Dispose();
            ProcessStarter.StartProcess(RockstarGames.ClientExecPath, $"-enableFullMode -uninstall={Game.GameId}");
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

                if (RockstarGamesLibrary.GetInstalledGames().FirstOrDefault(a => a.GameId == Game.GameId) == null)
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(2000);
            }
        }
    }

    public class RockstarPlayController : PlayController
    {
        private static ILogger logger = LogManager.GetLogger();
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;

        public RockstarPlayController(Game game) : base(game)
        {
            Name = ResourceProvider.GetString(LOC.RockstarStartUsingClient).Format("Rockstar");
        }

        public override void Dispose()
        {
            procMon?.Dispose();
        }

        public override void Play(PlayActionArgs args)
        {
            Dispose();
            if (Directory.Exists(Game.InstallDirectory))
            {
                stopWatch = Stopwatch.StartNew();
                procMon = new ProcessMonitor();
                procMon.TreeStarted += ProcMon_TreeStarted;
                procMon.TreeDestroyed += Monitor_TreeDestroyed;
                var rsApp = RockstarGames.Games.First(a => a.TitleId == Game.GameId);
                ProcessStarter.StartProcess(Path.Combine(Game.InstallDirectory, rsApp.Executable));
                StartRunningWatcher();
            }
            else
            {
                InvokeOnStopped(new GameStoppedEventArgs());
            }
        }

        private void ProcMon_TreeStarted(object sender, EventArgs args)
        {
            InvokeOnStarted(new GameStartedEventArgs());
        }

        private void Monitor_TreeDestroyed(object sender, EventArgs args)
        {
            InvokeOnStopped(new GameStoppedEventArgs(Convert.ToUInt64(stopWatch.Elapsed.TotalSeconds)));
        }

        public async void StartRunningWatcher()
        {
            // Give original process some time to start RS launcher and then kill itself
            await Task.Delay(10000);
            procMon.WatchDirectoryProcesses(Game.InstallDirectory, false);
        }
    }
}
