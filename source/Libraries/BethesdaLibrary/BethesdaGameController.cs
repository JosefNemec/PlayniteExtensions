using Playnite;
using Playnite.Common;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BethesdaLibrary
{
    public class BethesdaInstallController : InstallController
    {
        private CancellationTokenSource watcherToken;

        public BethesdaInstallController(Game game) : base(game)
        {
            Name = "Install using Bethesda client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            Dispose();
            ProcessStarter.StartUrl(Bethesda.ClientExecPath);
            StartInstallWatcher();
        }

        public async void StartInstallWatcher()
        {
            watcherToken = new CancellationTokenSource();

            while (true)
            {
                if (watcherToken.IsCancellationRequested)
                {
                    return;
                }

                var installedGame = BethesdaLibrary.GetInstalledGames().FirstOrDefault(a => a.GameId == Game.GameId);
                if (installedGame != null)
                {
                    var installInfo = new GameInfo()
                    {
                        InstallDirectory = installedGame.InstallDirectory
                    };

                    InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                    return;
                }

                await Task.Delay(10000);
            }
        }
    }

    public class BethesdaUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;

        public BethesdaUninstallController(Game game) : base(game)
        {
            Name = "Uninstall using Bethesda client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            Dispose();
            ProcessStarter.StartUrl("bethesdanet://uninstall/" + Game.GameId);
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

                if (BethesdaLibrary.GetInstalledGames().FirstOrDefault(a => a.GameId == Game.GameId) == null)
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(2000);
            }
        }
    }

    public class BethesdaPlayController : PlayController
    {
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;

        public BethesdaPlayController(Game game) : base(game)
        {
        }

        public override void Dispose()
        {
            procMon?.Dispose();
        }

        public override void Play(PlayActionArgs args)
        {
            Dispose();
            InvokeOnStarting(new GameStartingEventArgs());
            GameActionActivator.ActivateAction(new GameAction()
            {
                Type = GameActionType.URL,
                Path = @"bethesdanet://run/" + Game.GameId
            });

            if (Directory.Exists(Game.InstallDirectory))
            {
                stopWatch = Stopwatch.StartNew();
                procMon = new ProcessMonitor();
                procMon.TreeStarted += ProcMon_TreeStarted;
                procMon.TreeDestroyed += Monitor_TreeDestroyed;
                procMon.WatchDirectoryProcesses(Game.InstallDirectory, false);
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
            stopWatch.Stop();
            InvokeOnStopped(new GameStoppedEventArgs(Convert.ToInt64(stopWatch.Elapsed.TotalSeconds)));
        }
    }
}
