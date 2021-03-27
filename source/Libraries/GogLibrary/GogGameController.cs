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

        public GogInstallController(Game game) : base(game)
        {
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
                ProcessStarter.StartUrl(@"goggalaxy://openGameView/" + Game.GameId);
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
                    var installInfo = new GameInfo()
                    {
                        InstallDirectory = game.InstallDirectory
                    };

                    InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                    return;
                }

                await Task.Delay(2000);
            }
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

    public class GogPlayController : PlayController
    {
        private bool useGalaxy;
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;
        private IPlayniteAPI api;
        public GameAction SourceAction { get; set; }

        public GogPlayController(Game game, GameAction sourceAction, bool useGalaxy, IPlayniteAPI api) : base(game)
        {
            this.useGalaxy = useGalaxy;
            this.api = api;
            Name = sourceAction.Name;
            SourceAction = sourceAction;
        }

        public override void Dispose()
        {
            ReleaseResources();
        }

        public void ReleaseResources()
        {
            procMon?.Dispose();
        }

        public override void Play(PlayActionArgs args)
        {
            ReleaseResources();
            if (useGalaxy)
            {
                InvokeOnStarting(new GameStartingEventArgs());
                stopWatch = Stopwatch.StartNew();
                procMon = new ProcessMonitor();
                procMon.TreeStarted += ProcMon_TreeStarted;
                procMon.TreeDestroyed += Monitor_TreeDestroyed;
                var startArgs = string.Format(@"/gameId={0} /command=runGame /path=""{1}""", Game.GameId, Game.InstallDirectory);
                ProcessStarter.StartProcess(Path.Combine(Gog.InstallationPath, "GalaxyClient.exe"), startArgs);
                if (Directory.Exists(Game.InstallDirectory))
                {
                    procMon.WatchDirectoryProcesses(Game.InstallDirectory, false);
                }
                else
                {
                    InvokeOnStopped(new GameStoppedEventArgs());
                }
            }
            else
            {
                var playAction = api.ExpandGameVariables(Game, SourceAction);
                InvokeOnStarting(new GameStartingEventArgs());
                var proc = GameActionActivator.ActivateAction(playAction);
                InvokeOnStarted(new GameStartedEventArgs());

                if (playAction.Type != GameActionType.URL)
                {
                    stopWatch = Stopwatch.StartNew();
                    procMon = new ProcessMonitor();
                    procMon.TreeDestroyed += Monitor_TreeDestroyed;
                    procMon.WatchProcessTree(proc);
                }
                else
                {
                    InvokeOnStopped(new GameStoppedEventArgs());
                }
            }
        }

        private void ProcMon_TreeStarted(object sender, EventArgs args)
        {
            InvokeOnStarted(new GameStartedEventArgs());
        }

        private void Monitor_TreeDestroyed(object sender, EventArgs args)
        {
            stopWatch.Stop();
            InvokeOnStopped(new GameStoppedEventArgs() { SessionLength = Convert.ToInt64(stopWatch.Elapsed.TotalSeconds) });
        }
    }
}
