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
    public class SteamFriendsButton : Plugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("bc14d3d7-85ba-4a97-8a2d-00a73efa2a6a");

        public SteamFriendsButton(IPlayniteAPI api) : base(api)
        {
        }

        public override List<TopPanelItem> GetTopPanelItems()
        {
            var icon = new TextBlock
            {
                Text = char.ConvertFromUtf32(0xecf9),
                FontSize = 20
            };

            icon.SetResourceReference(TextBlock.FontFamilyProperty, "FontIcoFont");
            return new List<TopPanelItem>
            {
                new TopPanelItem()
                {
                    Icon = icon,
                    ToolTip = "Steam Friends",
                    Action = ()=> Process.Start(@"steam://open/friends")
                }
            };
        }
    }
}