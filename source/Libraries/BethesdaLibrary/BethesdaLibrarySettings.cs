using Playnite;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BethesdaLibrary
{
    public class BethesdaLibrarySettings
    {
        public bool ImportInstalledGames { get; set; } = true;
    }

    public class BethesdaLibrarySettingsViewModel : PluginSettingsViewModel<BethesdaLibrarySettings, BethesdaLibrary>
    {
        public BethesdaLibrarySettingsViewModel(BethesdaLibrary library, IPlayniteAPI api) : base(library, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new BethesdaLibrarySettings();
            }
        }
    }
}
