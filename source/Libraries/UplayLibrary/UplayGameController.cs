using Playnite;
using Playnite.Common;
using Playnite.SDK;
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

namespace UplayLibrary
{
    public class UplayInstallController : InstallController
    {
        private CancellationTokenSource watcherToken;

        public UplayInstallController(Game game) : base(game)
        {
            Name = "Install using Ubisoft Connect client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            Dispose();
            ProcessStarter.StartUrl("uplay://install/" + Game.GameId);
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

                    var installedGame = UplayLibrary.GetInstalledGames().FirstOrDefault(a => a.GameId == Game.GameId);
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

    public class UplayUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;

        public UplayUninstallController(Game game) : base(game)
        {
            Name = "Uninstall using Ubisoft Connect client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            Dispose();
            ProcessStarter.StartUrl("uplay://uninstall/" + Game.GameId);
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

                if (UplayLibrary.GetInstalledGames().FirstOrDefault(a => a.GameId == Game.GameId) == null)
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(2000);
            }
        }
    }

    public class UplayPlayController : PlayController
    {
        private static ILogger logger = LogManager.GetLogger();
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;
        private CancellationTokenSource watcherToken;

        public UplayPlayController(Game game) : base(game)
        {
            Name = string.Format(ResourceProvider.GetString(LOC.UbisoftStartUsingClient), "Ubisoft Connect");
        }

        public override void Dispose()
        {
            procMon?.Dispose();
            watcherToken?.Dispose();
        }

        public override void Play(PlayActionArgs args)
        {
            Dispose();
            if (Directory.Exists(Game.InstallDirectory))
            {
                procMon = new ProcessMonitor();
                procMon.TreeStarted += ProcMon_TreeStarted;
                procMon.TreeDestroyed += Monitor_TreeDestroyed;
                ProcessStarter.StartUrl(Uplay.GetLaunchString(Game.GameId));
                StartRunningWatcher(Uplay.GetGameRequiresUplay(Game));
            }
            else
            {
                InvokeOnStopped(new GameStoppedEventArgs());
            }
        }

        public async void StartRunningWatcher(bool waitForUplay)
        {
            if (waitForUplay)
            {
                logger.Debug("Game requires UbisoftGameLauncher to run, waiting for it to start properly.");
                // Solves issues with game process being started/shutdown multiple times during startup via Uplay
                watcherToken = new CancellationTokenSource();
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (ProcessExtensions.IsRunning("UbisoftGameLauncher"))
                    {
                        procMon.WatchDirectoryProcesses(Game.InstallDirectory, false);
                        return;
                    }

                    await Task.Delay(5000);
                }
            }
            else
            {
                procMon.WatchDirectoryProcesses(Game.InstallDirectory, false);
            }
        }

        private void ProcMon_TreeStarted(object sender, EventArgs args)
        {
            stopWatch = Stopwatch.StartNew();
            InvokeOnStarted(new GameStartedEventArgs());
        }

        private void Monitor_TreeDestroyed(object sender, EventArgs args)
        {
            stopWatch?.Stop();
            InvokeOnStopped(new GameStoppedEventArgs(Convert.ToUInt64(stopWatch?.Elapsed.TotalSeconds ?? 0)));
        }
    }
}
