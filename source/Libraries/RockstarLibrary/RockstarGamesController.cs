using Playnite;
using Playnite.Common;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RockstarGamesLibrary
{
    public class RockstarGamesController : BaseGameController
    {
        private CancellationTokenSource watcherToken;
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;
        private RockstarGamesLibrary library;

        public RockstarGamesController(RockstarGamesLibrary library, Game game) : base(game)
        {
            this.library = library;
        }

        public override void Dispose()
        {
            ReleaseResources();
        }

        public void ReleaseResources()
        {
            procMon?.Dispose();
        }

        public override void Play()
        {
            ReleaseResources();
            OnStarting(this, new GameControllerEventArgs(this, 0));
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
                OnStopped(this, new GameControllerEventArgs(this, 0));
            }
        }

        public override void Install()
        {
            ReleaseResources();
            RockstarGames.StartClient();
            StartInstallWatcher();
        }

        public override void Uninstall()
        {
            ReleaseResources();
            ProcessStarter.StartProcess(RockstarGames.ClientExecPath, $"-enableFullMode -uninstall={Game.GameId}");
            StartUninstallWatcher();
        }

        private void ProcMon_TreeStarted(object sender, EventArgs args)
        {
            OnStarted(this, new GameControllerEventArgs(this, 0));
        }

        private void Monitor_TreeDestroyed(object sender, EventArgs args)
        {
            stopWatch.Stop();
            OnStopped(this, new GameControllerEventArgs(this, stopWatch.Elapsed.TotalSeconds));
        }

        public async void StartRunningWatcher()
        {
            // Give original process some time to start RS launcher and then kill itself
            await Task.Delay(10000);
            procMon.WatchDirectoryProcesses(Game.InstallDirectory, false);
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

                var installedGame = library.GetInstalledGames().FirstOrDefault(a => a.GameId == Game.GameId);
                if (installedGame != null)
                {
                    var installInfo = new GameInfo()
                    {
                        PlayAction = installedGame.PlayAction,
                        InstallDirectory = installedGame.InstallDirectory
                    };

                    OnInstalled(this, new GameInstalledEventArgs(installInfo, this, 0));
                    return;
                }

                await Task.Delay(5000);
            }
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

                if (library.GetInstalledGames().FirstOrDefault(a => a.GameId == Game.GameId) == null)
                {
                    OnUninstalled(this, new GameControllerEventArgs(this, 0));
                    return;
                }

                await Task.Delay(2000);
            }
        }
    }
}
