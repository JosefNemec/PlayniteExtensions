using Playnite.SDK;
using Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniversalSteamMetadata
{
    public class UniversalSteamMetadataSettings
    {
        public bool DownloadVerticalCovers { get; set; } = true;
        public BackgroundSource BackgroundSource { get; set; } = BackgroundSource.Image;
    }

    public class UniversalSteamMetadataSettingsViewModel : PluginSettingsViewModel<UniversalSteamMetadataSettings, UniversalSteamMetadata>
    {
        public UniversalSteamMetadataSettingsViewModel(UniversalSteamMetadata plugin, IPlayniteAPI api) : base(plugin, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new UniversalSteamMetadataSettings();
            }
        }
    }
}