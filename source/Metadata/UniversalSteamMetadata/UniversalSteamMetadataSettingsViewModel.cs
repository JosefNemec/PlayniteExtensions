﻿using Playnite.SDK;
using Steam;
using SteamLibrary.SteamShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniversalSteamMetadata
{
    public class UniversalSteamMetadataSettings : SharedSteamSettings
    {
    }


    public class UniversalSteamMetadataSettingsViewModel : SharedSteamSettingsViewModel<UniversalSteamMetadataSettings, UniversalSteamMetadata>
    {
        public UniversalSteamMetadataSettingsViewModel(UniversalSteamMetadata plugin, IPlayniteAPI api) : base(plugin, api)
        {
        }
    }
}