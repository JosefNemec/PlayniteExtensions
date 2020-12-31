using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IGDBMetadata
{
    public enum MultiImagePriority
    {
        [Description("LOCFirst")]
        First,
        [Description("LOCRandom")]
        Random,
        [Description("LOCUserSelect")]
        Select
    }

    public class IgdbMetadataSettings
    {
        public bool UseScreenshotsIfNecessary { get; set; }
        public MultiImagePriority ImageSelectionPriority { get; set; }
    }

    public class IgdbMetadataSettingsViewModel : PluginSettingsViewModel<IgdbMetadataSettings, IgdbMetadataPlugin>
    {
        public IgdbMetadataSettingsViewModel(IgdbMetadataPlugin plugin, IPlayniteAPI api) : base(plugin, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new IgdbMetadataSettings();
            }
        }
    }
}
