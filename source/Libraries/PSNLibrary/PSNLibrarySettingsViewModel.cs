using Playnite.SDK;
using PSNLibrary.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSNLibrary
{
    public class PSNLibrarySettings
    {
        public bool ConnectAccount { get; set; } = false;
    }

    public class PSNLibrarySettingsViewModel : PluginSettingsViewModel<PSNLibrarySettings, PSNLibrary>
    {
        private PSNAccountClient clientApi;

        public bool IsUserLoggedIn
        {
            get
            {
                return clientApi.GetIsUserLoggedIn().GetAwaiter().GetResult();
            }
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                Login();
            });
        }

        public PSNLibrarySettingsViewModel(PSNLibrary plugin, IPlayniteAPI api) : base(plugin, api)
        {
            clientApi = new PSNAccountClient(plugin);
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new PSNLibrarySettings();
            }
        }

        private void Login()
        {
            try
            {
                clientApi.Login();
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                Logger.Error(e, "Failed to authenticate user.");
            }
        }
    }
}