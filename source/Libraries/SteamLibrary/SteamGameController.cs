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
using System.Runtime.InteropServices;

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
        private readonly SteamLibrarySettings settings;
        private readonly IPlayniteAPI playniteAPI;
        private ILogger logger = LogManager.GetLogger();

        public SteamPlayController(Game game, SteamLibrarySettings settings, IPlayniteAPI playniteAPI) : base(game)
        {
            gameId = game.ToSteamGameID();
            Name = string.Format(ResourceProvider.GetString(LOC.SteamStartUsingClient), "Steam");
            this.settings = settings;
            this.playniteAPI = playniteAPI;
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

            if (!gameId.IsMod && !gameId.IsShortcut
                && (
                    (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen && settings.ShowSteamLaunchMenuInFullscreenMode)
                    || (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Desktop && settings.ShowSteamLaunchMenuInDesktopMode)
                ))
            {
                ProcessStarter.StartUrl($"steam://launch/{Game.GameId}/Dialog");
                FocusDialogWindow();
            }
            else
            {
                ProcessStarter.StartUrl($"steam://rungameid/{Game.GameId}");
            }

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

        private void ProcMon_TreeStarted(object sender, ProcessMonitor.TreeStartedEventArgs args)
        {
            stopWatch = Stopwatch.StartNew();
            InvokeOnStarted(new GameStartedEventArgs());
        }

        private void Monitor_TreeDestroyed(object sender, EventArgs args)
        {
            stopWatch?.Stop();
            InvokeOnStopped(new GameStoppedEventArgs(Convert.ToUInt64(stopWatch?.Elapsed.TotalSeconds ?? 0)));
        }

        private void FocusDialogWindow()
        {
            // Wait for dialog window to be created
            Thread.Sleep(500);

            // Retrieve all launch windows (default and dialog) created by Steam (may contain other windows with Steam at the end of their title)
            var windows = FindWindowsEndingWithText(" Steam");

            // Get Steam's process ID for comparison with child process parent IDs
            var steamProcess = Process.GetProcessesByName("steam")?.FirstOrDefault();
            uint steamProcessID = (uint)steamProcess?.Id;
            uint currentWindowID;

            // Returned order is always front/top (dialog) - higher middle (default) - lower middle (dialog) - back/bottom (default)
            List<IntPtr> windowList = windows.ToList();

            // If Steam wasn't open do nothing -> the launch dialog will be focused by default
            if (windowList.Count == 0)
            {
                return;
            }

            // Remove windows not belonging to Steam
            List<IntPtr> filteredWindowList = new List<IntPtr>();
            foreach (var window in windowList)
            {
                GetWindowThreadProcessId(window, out currentWindowID);

                if (currentWindowID == steamProcessID)
                {
                    filteredWindowList.Add(window);
                }
            }

            // Consider users opening more than one Steam launch dialog
            if (filteredWindowList.Count > 2)
            {
                // Reversed order is always back/bottom (default) - lower middle (dialog) - higher middle (default) - front/top (dialog)
                filteredWindowList.Reverse();
                // Start from end to bring the most recently launched dialog to top
                for (int i = 0; i < filteredWindowList.Count; i++)
                {
                    if (i % 2 == 0)
                    {
                        // Even entries = 0 back/bottom (default), 2 higher middle (default)

                        // Skip default launch windows
                    }
                    else
                    {
                        // Odd entries = 1 lower middle (dialog), 3 front/top (dialog)

                        // Multiple games launched = focus every dialog with the most recently launched being top most
                        logger.Trace($"Setting foreground window: {filteredWindowList[i]}");
                        SetForegroundWindow(filteredWindowList[i]);
                    }
                }
            }
            else
            {
                // Only one game launched = focus single dialog
                logger.Trace($"Setting foreground window: {filteredWindowList[0]}");
                SetForegroundWindow(filteredWindowList[0]);
            }
        }

        #region FindWindowByTitle

        // Source: https://stackoverflow.com/questions/19867402/how-can-i-use-enumwindows-to-find-windows-with-a-specific-caption-title

        /// <summary> Find all windows that end with the given title text </summary>
        /// <param name="titleText"> The text that the window title must end with. </param>
        private static IEnumerable<IntPtr> FindWindowsEndingWithText(string titleText)
        {
            return FindWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                return GetWindowText(hWnd).EndsWith(titleText, StringComparison.InvariantCulture);
            });
        }

        /// <summary> Find all windows that match the given filter </summary>
        /// <param name="filter"> A delegate that returns true for windows
        ///    that should be returned and false for windows that should
        ///    not be returned </param>
        private static IEnumerable<IntPtr> FindWindows(EnumWindowsProc filter)
        {
            List<IntPtr> windows = new List<IntPtr>();

            EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                if (filter(hWnd, lParam))
                {
                    // Only add the windows that pass the filter
                    windows.Add(hWnd);
                }

                // But return true here so that we iterate all windows
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        /// <summary> Get the text for the window pointed to by hWnd </summary>
        private static string GetWindowText(IntPtr hWnd)
        {
            int size = GetWindowTextLength(hWnd);
            if (size > 0)
            {
                var builder = new StringBuilder(size + 1);
                GetWindowText(hWnd, builder, builder.Capacity);
                return builder.ToString();
            }

            return string.Empty;
        }

        #endregion FindWindowByTitle

        #region PrivateImports

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        #endregion
    }
}
