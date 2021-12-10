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

namespace EpicLibrary
{
    public class EpicInstallController : InstallController
    {
        private CancellationTokenSource watcherToken;

        public EpicInstallController(Game game) : base(game)
        {
            Name = "Install using Epic client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            if (!EpicLauncher.IsInstalled)
            {
                throw new Exception("Epic Launcher is not installed.");
            }

            Dispose();
            ProcessStarter.StartUrl(string.Format(EpicLauncher.GameInstallUrlMask, Game.GameId));
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
                        return;
                    }

                    var installed = EpicLauncher.GetInstalledAppList();
                    var app = installed?.FirstOrDefault(a => a.AppName == Game.GameId);
                    if (app != null)
                    {
                        var installInfo = new GameInstallationData
                        {
                            InstallDirectory = app.InstallLocation
                        };

                        InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                        return;
                    };

                    await Task.Delay(10000);
                }
            });
        }
    }

    public class EpicUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;

        public EpicUninstallController(Game game) : base(game)
        {
            Name = "Uninstall";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            if (!EpicLauncher.IsInstalled)
            {
                throw new Exception("Epic Launcher is not installed.");
            }

            Dispose();
            ProcessStarter.StartUrl(EpicLauncher.LibraryLaunchUrl);
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

                var installed = EpicLauncher.GetInstalledAppList();
                var app = installed?.FirstOrDefault(a => a.AppName == Game.GameId);
                if (app == null)
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(2000);
            }
        }
    }
}
