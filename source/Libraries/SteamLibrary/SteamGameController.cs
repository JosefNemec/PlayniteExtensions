using Playnite;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using System.IO;
using Playnite.SDK.Plugins;

namespace SteamLibrary
{
    public class SteamInstallController : InstallController
    {
        private CancellationTokenSource watcherToken;

        public SteamInstallController(Game game) : base(game)
        {
            Name = "Install using Steam";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            if (!Steam.IsInstalled)
            {
                throw new Exception("Steam installation not found.");
            }

            var gameId = Game.ToSteamGameID();
            if (gameId.IsMod)
            {
                throw new NotSupportedException("Installing mods is not supported.");
            }
            else
            {
                ProcessStarter.StartUrl($"steam://install/{gameId.AppID}");
                StartInstallWatcher();
            }
        }

        public async void StartInstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            var id = Game.ToSteamGameID();
            await Task.Run(async () =>
            {
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var installed = SteamLibrary.GetInstalledGames(false);
                    if (installed.TryGetValue(id, out var installedGame))
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

    public class SteamUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;

        public SteamUninstallController(Game game) : base(game)
        {
            Name = "Uninstall using Steam";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            if (!Steam.IsInstalled)
            {
                throw new Exception("Steam installation not found.");
            }

            var gameId = Game.ToSteamGameID();
            if (gameId.IsMod)
            {
                throw new NotSupportedException("Uninstalling mods is not supported.");
            }
            else
            {
                ProcessStarter.StartUrl($"steam://uninstall/{gameId.AppID}");
                StartUninstallWatcher();
            }
        }

        public async void StartUninstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            var id = Game.ToSteamGameID();

            while (true)
            {
                if (watcherToken.IsCancellationRequested)
                {
                    return;
                }

                var gameState = Steam.GetAppState(id);
                if (gameState.Installed == false)
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(5000);
            }
        }
    }

    public class SteamPlayController : PlayController
    {
        private GameID gameId;
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;

        public SteamPlayController(Game game) : base(game)
        {
            gameId = game.ToSteamGameID();
            Name = "Play on Steam";
        }

        public override void Dispose()
        {
            procMon?.Dispose();
        }

        public override void Play(PlayActionArgs args)
        {
            Dispose();

            var installDirectory = Game.InstallDirectory;
            if (gameId.IsMod)
            {
                var allGames = SteamLibrary.GetInstalledGames(false);
                if (allGames.TryGetValue(gameId.AppID.ToString(), out GameMetadata realGame))
                {
                    installDirectory = realGame.InstallDirectory;
                }
            }

            stopWatch = Stopwatch.StartNew();
            ProcessStarter.StartUrl($"steam://rungameid/{Game.GameId}");
            procMon = new ProcessMonitor();
            procMon.TreeStarted += ProcMon_TreeStarted;
            procMon.TreeDestroyed += Monitor_TreeDestroyed;
            if (Directory.Exists(installDirectory))
            {
                procMon.WatchDirectoryProcesses(installDirectory, false);
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
