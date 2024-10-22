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

namespace XboxLibrary
{
    public class XboxInstallController : InstallController
    {
        private readonly bool userXboxApp;
        private CancellationTokenSource watcherToken;
        private readonly string productId;
        private readonly string logsDirectoryWatcherPath;
        private FileSystemWatcher fileSystemWatcher;

        public XboxInstallController(Game game, bool useXboxApp, string productId) : base(game)
        {
            this.userXboxApp = useXboxApp;
            this.productId = productId;
            logsDirectoryWatcherPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages",
                "Microsoft.GamingApp_8wekyb3d8bbwe",
                "AC",
                "Temp"
            );

            Name = useXboxApp ? "Install using Xbox app" : "Install using MS Store";
        }

        public override void Install(InstallActionArgs args)
        {
            Dispose();
            if (userXboxApp)
            {
                if (productId.IsNullOrEmpty())
                {
                    Xbox.OpenXboxPassApp();
                }
                else if (Xbox.IsRunning)
                {
                    // If the Xbox app is already running, it's possible to open the game page without issues.
                    Xbox.OpenGamePage(productId);
                }
                else
                {
                    // Note: A recent update to the Xbox app introduced a bug where the URI to open the product page fails 
                    // until the app has fully initialized. This issue did not occur in earlier versions of the app.
                    //
                    // To determine when the app has fully initialized, we monitor for changes in a specific log file, 
                    // which is created or modified only after the initialization is complete.
                    // Alternative approaches, such as monitoring running processes, tracking changes in application windows, 
                    // or detecting locked files, were ineffective for addressing this scenario.
                    Xbox.OpenXboxPassApp();
                    InitializeFileSystemWatcher();
                }
            }
            else
            {
                ProcessStarter.StartUrl($"ms-windows-store://pdp/?PFN={Game.GameId}");
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
                        DisableFileSystemWatcher();
                        return;
                    }

                    var app = Programs.GetUWPApps().FirstOrDefault(a => a.AppId == Game.GameId);
                    if (app != null)
                    {
                        DisableFileSystemWatcher();
                        var installInfo = new GameInstallationData
                        {
                            InstallDirectory = app.WorkDir
                        };

                        InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                        return;
                    };

                    await Task.Delay(10000);
                }
            });
        }

        private void DisableFileSystemWatcher()
        {
            if (fileSystemWatcher != null)
            {
                fileSystemWatcher.EnableRaisingEvents = false;
            }
        }

        private void InitializeFileSystemWatcher()
        {
            if (!FileSystem.DirectoryExists(logsDirectoryWatcherPath))
            {
                return;
            }
            
            fileSystemWatcher = new FileSystemWatcher(logsDirectoryWatcherPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = "*.*",
                EnableRaisingEvents = true
            };

            fileSystemWatcher.Changed += OnFileChanged;
            fileSystemWatcher.Created += OnFileChanged;
            fileSystemWatcher.Renamed += OnFileChanged;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            DisableFileSystemWatcher();
            Xbox.OpenGamePage(productId);
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
            if (fileSystemWatcher != null)
            {
                fileSystemWatcher.EnableRaisingEvents = false;
                fileSystemWatcher.Changed -= OnFileChanged;
                fileSystemWatcher.Created -= OnFileChanged;
                fileSystemWatcher.Renamed -= OnFileChanged;
                fileSystemWatcher.Dispose();
                fileSystemWatcher = null;
            }
        }
    }

    public class XboxUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;

        public XboxUninstallController(Game game) : base(game)
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
            ProcessStarter.StartUrl("ms-settings:appsfeatures");
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

                var app = Programs.GetUWPApps().FirstOrDefault(a => a.AppId == Game.GameId);
                if (app == null)
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(10000);
            }
        }
    }

    public class XboxPlayController : PlayController
    {
        private static ILogger logger = LogManager.GetLogger();
        private ProcessMonitor procMon;
        private Stopwatch stopWatch;

        public XboxPlayController(Game game) : base(game)
        {
            Name = game.Name;
        }

        public override void Dispose()
        {
            procMon?.Dispose();
        }

        public override void Play(PlayActionArgs args)
        {
            Dispose();
            if (Game.GameId.StartsWith("CONSOLE"))
            {
                throw new Exception("We can't start console only games, the technology is not there yet.");
            }

            var prg = Programs.GetUWPApps().FirstOrDefault(a => a.AppId == Game.GameId);
            if (prg == null)
            {
                throw new Exception("Cannot start UWP game, installation not found.");
            }

            ProcessStarter.StartProcess(prg.Path, prg.Arguments);
            procMon = new ProcessMonitor();
            procMon.TreeDestroyed += Monitor_TreeDestroyed;
            procMon.TreeStarted += ProcMon_TreeStarted;

            // TODO switch to WatchUwpApp once we are building as 64bit app
            //procMon.WatchUwpApp(uwpMatch.Groups[1].Value, false);
            if (Directory.Exists(prg.WorkDir) && ProcessMonitor.IsWatchableByProcessNames(prg.WorkDir))
            {
                procMon.WatchDirectoryProcesses(prg.WorkDir, false, true);
            }
            else
            {
                InvokeOnStopped(new GameStoppedEventArgs());
            }
        }

        private void ProcMon_TreeStarted(object sender, ProcessMonitor.TreeStartedEventArgs args)
        {
            stopWatch = Stopwatch.StartNew();
            InvokeOnStarted(new GameStartedEventArgs() { StartedProcessId = args.StartedId });
        }

        private void Monitor_TreeDestroyed(object sender, EventArgs args)
        {
            stopWatch?.Stop();
            InvokeOnStopped(new GameStoppedEventArgs(Convert.ToUInt64(stopWatch?.Elapsed.TotalSeconds ?? 0)));
        }
    }
}
