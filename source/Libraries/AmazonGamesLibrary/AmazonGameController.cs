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
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly AmazonGamesLibrary library;
        private CancellationTokenSource watcherToken;

        public AmazonInstallController(Game game, AmazonGamesLibrary library) : base(game)
        {
            Name = "Install using Amazon client";
            this.library = library;
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
            await Task.Run(async () =>
            {
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var isInitialized = Process.GetProcessesByName("Amazon Games Services").Length > 0;
                    if (isInitialized)
                    {
                        // The install URI only works when this service is running, otherwise
                        // it will just start the launcher without any further action
                        ProcessStarter.StartUrl($"amazon-games://install/{Game.GameId}");
                        break;
                    }

                    await Task.Delay(1000);
                }

                while (true)
                {
                    Dictionary<string, GameMetadata> installedGames = null;
                    try
                    {
                        installedGames = library.GetInstalledGames();
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "Failed to get info about installed Amazon games.");
                    }

                    if (installedGames != null)
                    {
                        if (installedGames.TryGetValue(Game.GameId, out var installData))
                        {
                            var installInfo = new GameInstallationData()
                            {
                                InstallDirectory = installData.InstallDirectory
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

    public class AmazonUninstallController : UninstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly AmazonGamesLibrary library;
        private CancellationTokenSource watcherToken;

        public AmazonUninstallController(Game game, AmazonGamesLibrary library) : base(game)
        {
            Name = "Uninstall using Amazon client";
            this.library = library;
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            if (AmazonGames.IsInstalled)
            {
                AmazonGames.StartClient();
                StartUninstallWatcher();
            }
            else
            {
                throw new Exception("Can't uninstall game. Amazon Games client not found.");
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

                Dictionary<string, GameMetadata> installedGames = null;
                try
                {
                    installedGames = library.GetInstalledGames();
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to get info about installed Amazon games.");
                }

                if (installedGames != null)
                {
                    if (!installedGames.TryGetValue(Game.GameId, out var installData))
                    {
                        InvokeOnUninstalled(new GameUninstalledEventArgs());
                        return;
                    }
                }

                await Task.Delay(2000);
            }
        }
    }
}
