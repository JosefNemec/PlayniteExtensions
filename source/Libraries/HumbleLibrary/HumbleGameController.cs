using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HumbleLibrary.Models;
using HumbleLibrary.Services;
using Playnite.SDK.Data;
using PlayniteExtensions.Common;

namespace HumbleLibrary
{
    public class HumbleInstallController : InstallController
    {
        private static ILogger logger = LogManager.GetLogger();
        private CancellationTokenSource watcherToken;
        private readonly HumbleLibrary library;

        public HumbleInstallController(Game game, HumbleLibrary library) : base(game)
        {
            Name = "Install using Humble App";
            this.library = library;
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            if (HandleExtras())
            {
                return;
            }

            if (Game.SupportsHumbleApp())
            {
                Dispose();
                if (!HumbleClient.GetIsClientInstalled())
                {
                    throw new Exception("Humble App installation not found.");
                }

                if (HumbleClient.GetClientVersion() < new Version(0, 4, 8))
                {
                    ProcessStarter.StartUrl("humble://game/" + Game.GameId);
                }
                else
                {
                    ProcessStarter.StartUrl("humble://download/" + Game.GameId);
                }

                StartInstallWatcher();
            }
            else
            {
                Task.Run(async () =>
                {
                    try
                    {
                        using (var client = new HttpClient())
                        {
                            client.Timeout = new TimeSpan(0, 0, 10);
                            client.DefaultRequestHeaders.Add("User-Agent", library.UserAgent);
                            var machineName = Game.GameId.Substring(0, Game.GameId.LastIndexOf('_'));
                            await client.PostAsync("https://humblebundle.com/api/v1/analytics/humbleapp/request/" + machineName, null);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "Failed to send Humble game request info.");
                    }
                });

                throw new Exception(ResourceProvider.GetString(LOC.HumbleNonTroveInstallError));
            }
        }

        private bool HandleExtras()
        {
            if (!Game.GameId.StartsWith(HumbleLibrary.ExtrasPrefix))
            {
                return false;
            }

            var str = Encryption.DecryptFromFile(
                library.ExtrasFile,
                Encoding.UTF8,
                WindowsIdentity.GetCurrent().User.Value);
            var extra = Serialization.FromJson<Dictionary<string, Extra>>(str)[Game.GameId];
            var url = GetExtraUrl(extra);
            ProcessStarter.StartUrl(url);
            InvokeOnInstallationCancelled(new GameInstallationCancelledEventArgs());
            return true;
        }

        private string GetExtraUrl(Extra extra)
        {
            if (extra.PermanentUrl != null)
            {
                return extra.PermanentUrl;
            }

            using (var view = library.PlayniteApi.WebViews.CreateOffscreenView(
                       new WebViewSettings
                       {
                           JavaScriptEnabled = false,
                           UserAgent = library.UserAgent
                       }))
            {
                var api = new HumbleAccountClient(view);
                var order = api.GetOrders(new List<string>() {extra.GameKey}).Single();
                var actualDownload = order.subproducts
                    .SelectMany(x => x.downloads)
                    .SelectMany(x => x.download_struct)
                    .Single(x => x.sha1 == extra.Sha1);
                return actualDownload.url.web;
            }
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

                    InstalledTroveGame installedGame = null;
                    try
                    {
                        installedGame = library.GetInstalledGames().FirstOrDefault(a => a.GameId == Game.GameId);
                    }
                    catch (Exception exc)
                    {
                        logger.Error(exc, "Failed to get info about installed Humble games.");
                    }

                    if (installedGame != null)
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

    public class HumbleUninstallController : UninstallController
    {
        private static ILogger logger = LogManager.GetLogger();
        private CancellationTokenSource watcherToken;
        private readonly HumbleLibrary library;

        public HumbleUninstallController(Game game, HumbleLibrary library) : base(game)
        {
            Name = "Uninstall using Humble App";
            this.library = library;
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            if (Game.SupportsHumbleApp())
            {
                Dispose();
                if (!HumbleClient.GetIsClientInstalled())
                {
                    throw new Exception("Humble App installation not found.");
                }

                ProcessStarter.StartUrl("humble://uninstall/" + Game.GameId);
                StartUninstallWatcher();
            }
            else
            {
                throw new Exception(ResourceProvider.GetString(LOC.HumbleNonTroveInstallError));
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

                InstalledTroveGame installedGame = null;
                try
                {
                    installedGame = library.GetInstalledGames().FirstOrDefault(a => a.GameId == Game.GameId);
                }
                catch (Exception exc)
                {
                    logger.Error(exc, "Failed to get info about installed Humble games.");
                }

                if (installedGame == null)
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(2000);
            }
        }
    }
}
