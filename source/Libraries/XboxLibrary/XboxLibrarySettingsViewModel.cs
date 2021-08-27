using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XboxLibrary.Services;

namespace XboxLibrary
{
    public class XboxLibrarySettings
    {
        public bool ConnectAccount { get; set; } = false;
        public bool ImportInstalledGames { get; set; } = true;
        public bool ImportUninstalledGames { get; set; } = false;
        public bool XboxAppClientPriorityLaunch { get; set; } = false;
        public bool ImportConsoleGames { get; set; } = false;
    }

    public class XboxLibrarySettingsViewModel : PluginSettingsViewModel<XboxLibrarySettings, XboxLibrary>
    {
        public bool IsFirstRunUse { get; set; }

        public bool IsUserLoggedIn
        {
            get
            {
                return new XboxAccountClient(Plugin).GetIsUserLoggedIn().GetAwaiter().GetResult();
            }
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>(async (a) =>
            {
                await Login();
            });
        }

        public XboxLibrarySettingsViewModel(XboxLibrary plugin, IPlayniteAPI api) : base(plugin, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new XboxLibrarySettings();
            }
        }

        private async Task Login()
        {
            try
            {
                var client = new XboxAccountClient(Plugin);
                await client.Login();
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                Logger.Error(e, "Failed to authenticate user.");
            }
        }
    }
}