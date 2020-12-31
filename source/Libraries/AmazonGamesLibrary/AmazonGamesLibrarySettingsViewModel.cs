using AmazonGamesLibrary.Services;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmazonGamesLibrary
{
    public class AmazonGamesLibrarySettings
    {
        public int Version { get; set; }
        public bool ImportInstalledGames { get; set; } = true;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
        public bool StartGamesWithoutLauncher { get; set; } = false;
    }

    public class AmazonGamesLibrarySettingsViewModel : PluginSettingsViewModel<AmazonGamesLibrarySettings, AmazonGamesLibrary>
    {
        public bool IsFirstRunUse { get; set; }

        public bool IsUserLoggedIn
        {
            get
            {
                try
                {
                    var client = new AmazonAccountClient(Plugin);
                    return client.GetIsUserLoggedIn().GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "");
                    return false;
                }
            }
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>(async (a) =>
            {
                await Login();
            });
        }

        public AmazonGamesLibrarySettingsViewModel(AmazonGamesLibrary library, IPlayniteAPI api) : base(library, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                if (savedSettings.Version == 0)
                {
                    Logger.Debug("Updating Amazon settings from version 0.");
                    if (savedSettings.ImportUninstalledGames)
                    {
                        savedSettings.ConnectAccount = true;
                    }
                }

                savedSettings.Version = 1;
                Settings = savedSettings;
            }
            else
            {
                Settings = new AmazonGamesLibrarySettings() { Version = 1 };
            }
        }

        private async Task Login()
        {
            try
            {
                var client = new AmazonAccountClient(Plugin);
                await client.Login();
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                Logger.Error(e, "Failed to authenticate Amazon user.");
            }
        }
    }
}