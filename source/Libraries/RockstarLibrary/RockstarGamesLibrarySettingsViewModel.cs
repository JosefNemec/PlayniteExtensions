using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RockstarGamesLibrary
{
    public class RockstarGamesLibrarySettings
    {
    }

    public class RockstarGamesLibrarySettingsViewModel : PluginSettingsViewModel<RockstarGamesLibrarySettings, RockstarGamesLibrary>
    {
        public RockstarGamesLibrarySettingsViewModel(RockstarGamesLibrary library, IPlayniteAPI api) : base(library, api)
        {
        }
    }
}