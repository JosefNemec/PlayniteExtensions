using Playnite.SDK;
using Steam;
using SteamLibrary.SteamShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniversalSteamMetadata
{
    public class UniversalSteamMetadataSettings : UniversalSteamSettings
    {
    }


    public class UniversalSteamMetadataSettingsViewModel : UniversalSteamSettingsViewModel<UniversalSteamMetadataSettings, UniversalSteamMetadata>
    {
        public UniversalSteamMetadataSettingsViewModel(UniversalSteamMetadata plugin, IPlayniteAPI api) : base(plugin, api)
        {
        }
    }
}