using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using SteamKit2;
using SteamLibrary.Models;
using SteamLibrary.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamLibrary
{
    [LoadPlugin]
    public class SteamLibrary : LibraryPluginBase<SteamLibrarySettingsViewModel>
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly Configuration config;
        internal SteamServicesClient ServicesClient;
        internal TopPanelItem TopPanelFriendsButton;

        public SteamLibrary(IPlayniteAPI api) : base(
            "Steam",
            Guid.Parse("CB91DFC9-B977-43BF-8E70-55F46E410FAB"),
            new LibraryPluginProperties { CanShutdownClient = true, HasSettings = true },
            new SteamClient(),
            Steam.Icon,
            (_) => new SteamLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new SteamLibrarySettingsViewModel(this, PlayniteApi);
            config = GetPluginConfiguration<Configuration>();
            ServicesClient = new SteamServicesClient(config.ServicesEndpoint);

            TopPanelFriendsButton = new TopPanelItem()
            {
                Icon = new TextBlock
                {
                    Text = char.ConvertFromUtf32(0xecf9),
                    FontSize = 20,
                    FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily
                },
                Title = ResourceProvider.GetString(LOC.SteamFriendsTooltip),
                Activated = () =>
                {
                    try
                    {
                        Process.Start(@"steam://open/friends");
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "Failed to open Steam friends.");
                        PlayniteApi.Dialogs.ShowErrorMessage(e.Message, "");
                    }
                },
                Visible = SettingsViewModel.Settings.ShowFriendsButton
            };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            SettingsViewModel.IsFirstRunUse = firstRunSettings;
            return SettingsViewModel;
        }

        public override LibraryMetadataProvider GetMetadataDownloader()
        {
            return new SteamMetadataProvider(this);
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new SteamInstallController(args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new SteamUninstallController(args.Game);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                yield break;
            }

            yield return new SteamPlayController(args.Game, SettingsViewModel.Settings, PlayniteApi);
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var aggregator = new SteamServiceAggregator(new PlayerService(), new SteamStoreService(PlayniteApi), new ClientCommService(), new FamilyGroupsService(), ServicesClient, this);
            return aggregator.GetGamesAsync(SettingsViewModel.Settings).GetAwaiter().GetResult();
        }

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return TopPanelFriendsButton;
        }
    }
}
