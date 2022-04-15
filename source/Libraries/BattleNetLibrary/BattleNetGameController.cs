using BattleNetLibrary.Models;
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

namespace BattleNetLibrary
{
    public class BnetInstallController : InstallController
    {
        private CancellationTokenSource watcherToken;

        public BnetInstallController(Game game) : base(game)
        {
            Name = "Install using Battle.net client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            Dispose();
            var product = BattleNetGames.GetAppDefinition(Game.GameId);
            if (product.Type == BNetAppType.Classic)
            {
                ProcessStarter.StartUrl(@"https://battle.net/account/management/download/");
            }
            else
            {
                if (!BattleNet.IsInstalled)
                {
                    throw new Exception("Cannot install game, Battle.net launcher is not installed properly.");
                }

                ProcessStarter.StartProcess(BattleNet.ClientExecPath, $"--game={product.InternalId}");
            }

            StartInstallWatcher();
        }

        public async void StartInstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            var app = BattleNetGames.GetAppDefinition(Game.GameId);
            await Task.Run(async () =>
            {
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var install = BattleNetLibrary.GetUninstallEntry(app);
                    if (install == null)
                    {
                        await Task.Delay(10000);
                        continue;
                    }
                    else
                    {
                        var installInfo = new GameInstallationData()
                        {
                            InstallDirectory = install.InstallLocation
                        };

                        InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                        return;
                    }
                }
            });
        }
    }

    public class BnetUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;

        public BnetUninstallController(Game game) : base(game)
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
            var product = BattleNetGames.GetAppDefinition(Game.GameId);
            var entry = BattleNetLibrary.GetUninstallEntry(product);
            if (entry != null)
            {
                var startArgs = string.Format("/C \"{0}\"", entry.UninstallString);
                ProcessStarter.StartProcess("cmd", startArgs);
                StartUninstallWatcher();
            }
            else
            {
                InvokeOnUninstalled(new GameUninstalledEventArgs());
            }
        }

        public async void StartUninstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            var app = BattleNetGames.GetAppDefinition(Game.GameId);
            while (true)
            {
                if (watcherToken.IsCancellationRequested)
                {
                    return;
                }

                var entry = BattleNetLibrary.GetUninstallEntry(app);
                if (entry == null)
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(2000);
            }
        }
    }

    public class BnetPlayController : PlayController
    {
        private static ILogger logger = LogManager.GetLogger();
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;

        public BnetPlayController(Game game) : base(game)
        {
            Name = string.Format(ResourceProvider.GetString(LOC.BattleNetStartUsingClient), "Battle.net");
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
            stopWatch = Stopwatch.StartNew();
            procMon = new ProcessMonitor();
            procMon.TreeDestroyed += Monitor_TreeDestroyed;
            var app = BattleNetGames.GetAppDefinition(Game.GameId);

            if (app.Type == BNetAppType.Default)
            {
                if (!BattleNet.IsInstalled)
                {
                    throw new Exception("Cannot start game, Battle.net launcher is not installed properly.");
                }

                StartBnetRunningWatcher();
            }
            else
            {
                ProcessStarter.StartProcess(app.ClassicExecutable, null, Game.InstallDirectory);
                InvokeOnStarted(new GameStartedEventArgs());
                if (Directory.Exists(Game.InstallDirectory))
                {
                    procMon.WatchDirectoryProcesses(Game.InstallDirectory, true, true);
                }
                else
                {
                    InvokeOnStopped(new GameStoppedEventArgs());
                }
            }
        }

        public async void StartBnetRunningWatcher()
        {
            if (!BattleNet.IsRunning)
            {
                logger.Info("Battle.net is not running, starting it first.");
                BattleNet.StartClient();
                while (BattleNet.RunningProcessesCount < 4)
                {
                    await Task.Delay(500);
                }

                await Task.Delay(4000);
            }

            try
            {
                ProcessStarter.StartProcess(BattleNet.ClientExecPath, string.Format("--exec=\"launch {0}\"", Game.GameId));
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                logger.Error(e, "Failed to start battle.net game.");
                InvokeOnStopped(new GameStoppedEventArgs());
                return;
            }

            if (Directory.Exists(Game.InstallDirectory))
            {
                procMon.TreeStarted += ProcMon_TreeStarted;
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
            InvokeOnStopped(new GameStoppedEventArgs(Convert.ToUInt64(stopWatch.Elapsed.TotalSeconds)));
        }
    }
}
