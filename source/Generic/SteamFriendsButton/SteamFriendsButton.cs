using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SteamFriendsButton
{
    public class SteamFriendsButton : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("bc14d3d7-85ba-4a97-8a2d-00a73efa2a6a");

        public SteamFriendsButton(IPlayniteAPI api) : base(api)
        {
        }

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            var icon = new TextBlock
            {
                Text = char.ConvertFromUtf32(0xecf9),
                FontSize = 20
            };

            icon.SetResourceReference(TextBlock.FontFamilyProperty, "FontIcoFont");
            yield return new TopPanelItem()
            {
                Icon = icon,
                Title = "Steam Friends",
                Activated = () => Process.Start(@"steam://open/friends")
            };
        }
    }
}