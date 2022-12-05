﻿using Playnite;
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
                Task.Run(FocusDialogWindow); //Don't block the main thread with the startup wait
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
            const int timeoutSeconds = 4;
            try
            {
                // Get Steam's process ID for comparison with child process parent IDs
                var steamProcess = Process.GetProcessesByName("steam")?.FirstOrDefault();
                if (steamProcess == null)
                {
                    logger.Trace("Steam isn't running. The Steam dialog window will focus without our help once Steam starts up.");
                    return;
                }

                var windows = new List<WindowInfo>();
                var stopwatch = Stopwatch.StartNew();

                do
                {
                    // Wait for dialog window to be created
                    Thread.Sleep(200);

                    windows = GetSteamDialogWindows(steamProcess);
                }
                while (windows.Count == 0 && stopwatch.ElapsedMilliseconds < timeoutSeconds * 1000);

                if (windows.Count == 0)
                {
                    logger.Trace($"No Steam dialog windows found to focus within {timeoutSeconds} seconds");
                    return;
                }

                // The list has the foremost items first, and we want to focus everything ending with the window that is at the front (the startup choice dialog)
                // So run through it in reverse
                for (int i = windows.Count - 1; i >= 0; i--)
                {
                    // Wait for the previous focus call to resolve
                    // Otherwise this one might be ignored
                    Thread.Sleep(10);
                    var window = windows[i];
                    logger.Debug($"Setting foreground window: {window.Handle} {window.Title}");
                    User32.SetForegroundWindow(window.Handle);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Error focusing Steam game startup dialog");
            }
        }

        #region FindWindows

        private class WindowInfo
        {
            public WindowInfo(IntPtr handle, string title)
            {
                Handle = handle;
                Title = title;
            }

            public IntPtr Handle { get; set; }
            public string Title { get; set; }
        }

        private static List<WindowInfo> GetSteamDialogWindows(Process steamProcess)
        {
            var windows = new List<WindowInfo>();

            User32.EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
            {
                User32.GetWindowThreadProcessId(hWnd, out uint processId);
                if (steamProcess.Id != processId)
                {
                    // Return true here so that we iterate all windows
                    return true;
                }

                var text = GetWindowText(hWnd);
                if (!text.EndsWith(" Steam"))
                {
                    return true;
                }

                windows.Add(new WindowInfo(hWnd, text));
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        /// <summary> Get the text for the window pointed to by hWnd </summary>
        private static string GetWindowText(IntPtr hWnd)
        {
            int size = User32.GetWindowTextLength(hWnd);
            if (size > 0)
            {
                var builder = new StringBuilder(size + 1);
                User32.GetWindowText(hWnd, builder, builder.Capacity);
                return builder.ToString();
            }

            return string.Empty;
        }

        #endregion FindWindows
    }
}
