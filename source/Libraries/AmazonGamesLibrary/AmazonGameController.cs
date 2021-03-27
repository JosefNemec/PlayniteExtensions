using AmazonGamesLibrary.Models;
using Playnite;
using Playnite.Common;
using Playnite.SDK;
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

namespace AmazonGamesLibrary
{
    public class AmazonInstallController : InstallController
    {
        private CancellationTokenSource watcherToken;

        public AmazonInstallController(Game game) : base(game)
        {
            Name = "Install using Amazon client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            if (AmazonGames.IsInstalled)
            {
                AmazonGames.StartClient();
            }
            else
            {
                ProcessStarter.StartUrl(@"https://www.amazongames.com/en-us/support/prime-gaming/articles/download-and-install-the-amazon-games-app");
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
                var program = AmazonGames.GetUninstallRecord(Game.GameId);
                if (program != null)
                {
                    var installInfo = new GameInfo()
                    {
                        InstallDirectory = Paths.FixSeparators(program.InstallLocation)
                    };

                    InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                    return;
                }

                await Task.Delay(2000);
            }
        }
    }

    public class AmazonUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;

        public AmazonUninstallController(Game game) : base(game)
        {
            Name = "Uninstall using Amazon client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            var uninstallInfo = AmazonGames.GetUninstallRecord(Game.GameId);
            if (uninstallInfo != null)
            {
                var uninstallString = uninstallInfo.UninstallString.Replace(@"\\", @"\");
                var removerFileName = "Amazon Game Remover.exe";
                var split = uninstallString.Split(new string[] { removerFileName }, StringSplitOptions.RemoveEmptyEntries);
                var path = (split[0] + removerFileName).Trim('"');
                var startArgs = split[1].Trim('"');
                ProcessStarter.StartProcess(path, startArgs);
                StartUninstallWatcher();
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

                var program = AmazonGames.GetUninstallRecord(Game.GameId);
                if (program == null)
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(2000);
            }
        }
    }

    public class AmazonPlayController : PlayController
    {
        private static ILogger logger = LogManager.GetLogger();
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;
        private bool useLauncher;

        public AmazonPlayController(Game game, bool useLauncher) : base(game)
        {
            this.useLauncher = useLauncher;
        }

        public override void Dispose()
        {
            procMon?.Dispose();
        }

        public override void Play(PlayActionArgs args)
        {
            InvokeOnStarting(new GameStartingEventArgs());
            var startViaLauncher = true;
            GameConfiguration gameConfig = null;
            if (!useLauncher)
            {
                try
                {
                    gameConfig = AmazonGames.GetGameConfiguration(Game.InstallDirectory);
                    if (AmazonGames.GetGameRequiresClient(gameConfig))
                    {
                        startViaLauncher = true;
                    }
                    else
                    {
                        startViaLauncher = false;
                    }
                }
                catch (Exception e) when (!Debugger.IsAttached)
                {
                    logger.Error(e, "Failed to get local game configuration.");
                }
            }

            if (startViaLauncher)
            {
                ProcessStarter.StartUrl($"amazon-games://play/{Game.GameId}");
            }
            else
            {
                var exePath = Path.Combine(Game.InstallDirectory, gameConfig.Main.Command);
                var workDir = Game.InstallDirectory;
                if (!gameConfig.Main.WorkingSubdirOverride.IsNullOrEmpty())
                {
                    workDir = Path.Combine(Game.InstallDirectory, gameConfig.Main.WorkingSubdirOverride);
                }

                string startArgs = null;
                if (gameConfig.Main.Args.HasNonEmptyItems())
                {
                    startArgs = string.Join(" ", gameConfig.Main.Args);
                }

                ProcessStarter.StartProcess(exePath, startArgs, workDir);
            }

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
