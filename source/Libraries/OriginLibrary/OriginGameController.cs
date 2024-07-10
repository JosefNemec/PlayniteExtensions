using OriginLibrary.Models;
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

namespace OriginLibrary
{
    public class OriginInstallController : InstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private CancellationTokenSource watcherToken;
        private OriginLibrary origin;

        public OriginInstallController(Game game, OriginLibrary library) : base(game)
        {
            origin = library;
            Name = "Install using EA app";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            Dispose();
            ProcessStarter.StartUrl(Origin.LibraryOpenUri);
            StartInstallWatcher();
        }

        public async void StartInstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            var manifest = origin.GetLocalInstallerManifest(Game.GameId);
            if (manifest?.publishing == null)
            {
                logger.Error($"No publishing manifest found for Origin game {Game.GameId}, stopping installation check.");
                return;
            }

            var platform = manifest.publishing.softwareList.software.FirstOrDefault(a => a.softwarePlatform == "PCWIN");
            await Task.Run(async () =>
            {
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var executablePath = origin.GetPathFromPlatformPath(platform.fulfillmentAttributes.installCheckOverride);
                    if (!executablePath?.CompletePath.IsNullOrEmpty() != null)
                    {
                        if (File.Exists(executablePath.CompletePath))
                        {
                            var installInfo = new GameInstallationData
                            {
                                InstallDirectory = origin.GetInstallDirectory(manifest)
                            };

                            InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                            return;
                        }
                    }

                    await Task.Delay(10000);
                }
            });
        }
    }

    public class OriginUninstallController : UninstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private CancellationTokenSource watcherToken;
        private OriginLibrary origin;

        public OriginUninstallController(Game game, OriginLibrary library) : base(game)
        {
            origin = library;
            Name = "Uninstall using EA app";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            Dispose();
            ProcessStarter.StartUrl(Origin.LibraryOpenUri);
            StartUninstallWatcher();
        }

        public async void StartUninstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            var manifest = origin.GetLocalInstallerManifest(Game.GameId);
            if (manifest?.publishing == null)
            {
                logger.Error($"No publishing manifest found for EA game {Game.GameId}, stopping uninstallation check.");
                InvokeOnUninstalled(new GameUninstalledEventArgs());
                return;
            }

            var platform = manifest.publishing.softwareList.software.FirstOrDefault(a => a.softwarePlatform == "PCWIN");
            var executablePath = origin.GetPathFromPlatformPath(platform.fulfillmentAttributes.installCheckOverride);

            while (true)
            {
                if (watcherToken.IsCancellationRequested)
                {
                    return;
                }

                if (executablePath?.CompletePath == null)
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }
                else
                {
                    if (!File.Exists(executablePath.CompletePath))
                    {
                        InvokeOnUninstalled(new GameUninstalledEventArgs());
                        return;
                    }
                }

                await Task.Delay(2000);
            }
        }
    }

    public class OriginPlayController : PlayController
    {
        private static ILogger logger = LogManager.GetLogger();
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;
        private readonly OriginLibrary originLibrary;

        public OriginPlayController(Game game, OriginLibrary library) : base(game)
        {
            Name = string.Format(ResourceProvider.GetString(LOC.OriginStartUsingClient), "EA app");
            originLibrary = library;
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
                procMon = new ProcessMonitor();
                procMon.TreeDestroyed += ProcMon_TreeDestroyed;
                procMon.TreeStarted += ProcMon_TreeStarted;
                var startAction = originLibrary.GetGamePlayTaskForGameId(Game.GameId);
                if (Origin.GetGameUsesEasyAntiCheat(Game.InstallDirectory))
                {
                    var eac = EasyAntiCheat.GetLauncherSettings(Game.InstallDirectory);
                    if (!eac.parameters.IsNullOrEmpty() && eac.use_cmdline_parameters == "1")
                    {
                        startAction.Arguments = eac.parameters;
                    }

                    if (!eac.working_directory.IsNullOrEmpty())
                    {
                        startAction.WorkingDir = Path.Combine(Game.InstallDirectory, eac.working_directory);
                    }
                    else
                    {
                        startAction.WorkingDir = Game.InstallDirectory;
                    }

                    startAction.Path = eac.executable;
                    ProcessStarter.StartProcess(startAction.Path, startAction.Arguments, startAction.WorkingDir);
                }
                else
                {
                    if (startAction.Type == GameActionType.URL)
                    {
                        ProcessStarter.StartUrl(startAction.Path);
                    }
                    else
                    {
                        ProcessStarter.StartProcess(startAction.Path, startAction.Arguments, startAction.WorkingDir);
                    }
                }

                StartRunningWatcher();
            }
            else
            {
                InvokeOnStopped(new GameStoppedEventArgs());
            }
        }

        public async void StartRunningWatcher()
        {
            // Solves issues with game process being started/shutdown multiple times during startup via Origin
            if (Origin.GetGameRequiresOrigin(Game.InstallDirectory) || Origin.GetGameUsesEasyAntiCheat(Game.InstallDirectory))
            {
                await Task.Delay(40_000);
            }

            procMon.WatchDirectoryProcesses(Game.InstallDirectory, false);
        }

        private void ProcMon_TreeStarted(object sender, ProcessMonitor.TreeStartedEventArgs args)
        {
            stopWatch = Stopwatch.StartNew();
            InvokeOnStarted(new GameStartedEventArgs() { StartedProcessId = args.StartedId });
        }

        private void ProcMon_TreeDestroyed(object sender, EventArgs args)
        {
            stopWatch?.Stop();
            InvokeOnStopped(new GameStoppedEventArgs(Convert.ToUInt64(stopWatch?.Elapsed.TotalSeconds ?? 0)));
        }
    }
}
