using ItchioLibrary.Models;
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
using System.Windows;
using System.Windows.Media;

namespace ItchioLibrary
{
    public class ItchInstallController : InstallController
    {
        private CancellationTokenSource watcherToken;

        public ItchInstallController(Game game) : base(game)
        {
            Name = "Install using Itch client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Install(InstallActionArgs args)
        {
            if (!Itch.IsInstalled)
            {
                throw new Exception(ResourceProvider.GetString(LOC.ItchioClientNotInstalledError));
            }

            Dispose();
            ProcessStarter.StartUrl("itch://install?game_id=" + Game.GameId);
            StartInstallWatcher();
        }

        public async void StartInstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            await Task.Run(async () =>
            {
                using (var butler = new Butler())
                {
                    while (true)
                    {
                        if (watcherToken.IsCancellationRequested)
                        {
                            return;
                        }

                        var installed = butler.GetCaves();
                        var cave = installed?.FirstOrDefault(a => a.game.id.ToString() == Game.GameId);
                        if (cave != null)
                        {
                            var installInfo = new GameInstallationData
                            {
                                InstallDirectory = cave.installInfo.installFolder
                            };

                            InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                            return;
                        }

                        await Task.Delay(10000);
                    }
                }
            });
        }
    }

    public class ItchUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;

        public ItchUninstallController(Game game) : base(game)
        {
            Name = "Uninstall using Itch client";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            if (!Itch.IsInstalled)
            {
                throw new Exception(ResourceProvider.GetString(LOC.ItchioClientNotInstalledError));
            }

            Dispose();
            ProcessStarter.StartUrl("itch://library/installed");
            StartUninstallWatcher();
        }

        public async void StartUninstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            using (var butler = new Butler())
            {
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var installed = butler.GetCaves();
                    var cave = installed?.FirstOrDefault(a => a.game.id.ToString() == Game.GameId);
                    if (cave == null)
                    {
                        InvokeOnUninstalled(new GameUninstalledEventArgs());
                        return;
                    }

                    await Task.Delay(2000);
                }
            }
        }
    }

    public class ItchPlayController : PlayController
    {
        private static ILogger logger = LogManager.GetLogger();
        private readonly IPlayniteAPI api;
        private Stopwatch stopWatch;
        private Butler butler;

        public ItchPlayController(Game game, IPlayniteAPI api) : base(game)
        {
            Name = ResourceProvider.GetString(LOC.itchioStartUsingClient).Format("itch.io");
            this.api = api;
        }

        public override void Dispose()
        {
            ReleaseResources();
        }

        public void ReleaseResources()
        {
            if (butler != null)
            {
                butler.RequestReceived -= Butler_RequestReceived;
                butler.NotificationReceived -= Butler_NotificationReceived;
                butler.Dispose();
            }
        }

        public override void Play(PlayActionArgs args)
        {
            if (!Itch.IsInstalled)
            {
                throw new Exception(ResourceProvider.GetString(LOC.ItchioClientNotInstalledError));
            }

            ReleaseResources();
            butler = new Butler();
            var cave = butler.GetCaves().FirstOrDefault(a => a.game.id == long.Parse(Game.GameId));
            if (cave != null)
            {
                butler.RequestReceived += Butler_RequestReceived;
                butler.NotificationReceived += Butler_NotificationReceived;
                butler.LaunchAsync(cave.id);
            }
            else
            {
                throw new Exception("Game installation not found.");
            }
        }

        private void Butler_NotificationReceived(object sender, JsonRpcNotificationEventArgs e)
        {
            if (e.Notification.Method == Butler.Methods.LaunchRunning)
            {
                InvokeOnStarted(new GameStartedEventArgs());
                stopWatch = Stopwatch.StartNew();
            }
            else if (e.Notification.Method == Butler.Methods.LaunchExited)
            {
                stopWatch.Stop();
                InvokeOnStopped(new GameStoppedEventArgs(Convert.ToUInt64(stopWatch.Elapsed.TotalSeconds)));
            }
        }

        private void Butler_RequestReceived(object sender, JsonRpcRequestEventArgs e)
        {
            switch (e.Request.Method)
            {
                case Butler.Methods.PickManifestAction:
                    var pick = e.Request.GetParams<PickManifestAction>();
                    var selectedIndex = -1;
                    if (pick.actions.HasItems())
                    {
                        if (pick.actions.Count == 1)
                        {
                            selectedIndex = 0;
                        }
                        else
                        {
                            var i = 0;
                            pick.actions.ForEach(a => a.actionIndex = i++);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Window window = null;
                                if (api.ApplicationInfo.Mode == ApplicationMode.Desktop)
                                {
                                    window = api.Dialogs.CreateWindow(new WindowCreationOptions());
                                }
                                else
                                {
                                    window = new Window();
                                    window.Background = Brushes.Black;
                                }

                                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                                window.MinWidth = 300;
                                window.SizeToContent = SizeToContent.WidthAndHeight;
                                var startupModel = new StartupSelectionViewModel(window, pick.actions);
                                window.Content = new StartupSelectionView() { DataContext = startupModel };
                                window.Owner = api.Dialogs.GetCurrentAppWindow();
                                window.ShowDialog();
                                selectedIndex = startupModel.SelectedActionIndex;
                            });
                        }
                    }

                    butler.SendResponse(e.Request, new Dictionary<string, int> { { "index", selectedIndex } });
                    if (selectedIndex == -1)
                    {
                        InvokeOnStopped(new GameStoppedEventArgs(0));
                    }
                    break;

                case Butler.Methods.HTMLLaunch:
                    var html = e.Request.GetParams<HTMLLaunch>();
                    ProcessStarter.StartProcess(Path.Combine(html.rootFolder, html.indexPath));
                    butler.SendResponse(e.Request);
                    break;

                case Butler.Methods.URLLaunch:
                    var url = e.Request.GetParams<URLLaunch>();
                    ProcessStarter.StartUrl(url.url);
                    butler.SendResponse(e.Request);
                    break;

                case Butler.Methods.ShellLaunch:
                    var shell = e.Request.GetParams<ShellLaunch>();
                    ProcessStarter.StartProcess(shell.itemPath);
                    butler.SendResponse(e.Request);
                    break;

                case Butler.Methods.PrereqsFailed:
                    var error = e.Request.GetParams<PrereqsFailed>();
                    butler.SendResponse(e.Request, new Dictionary<string, bool>
                    {
                        { "continue",  true }
                    });
                    break;
            }
        }
    }
}
