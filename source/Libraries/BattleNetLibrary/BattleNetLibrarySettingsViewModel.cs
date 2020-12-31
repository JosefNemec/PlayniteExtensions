using BattleNetLibrary.Services;
using Playnite;
using Playnite.SDK;
using Playnite.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.SDK.Data;

namespace BattleNetLibrary
{
    public class BattleNetLibrarySettings
    {
        public int Version { get; set; }
        public bool ImportInstalledGames { get; set; } = true;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
    }

    public class BattleNetLibrarySettingsViewModel : PluginSettingsViewModel<BattleNetLibrarySettings, BattleNetLibrary>
    {
        public bool IsUserLoggedIn
        {
            get
            {
                using (var view = PlayniteApi.WebViews.CreateOffscreenView())
                {
                    var api = new BattleNetAccountClient(view);
                    return api.GetIsUserLoggedIn();
                }
            }
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                Login();
            });
        }

        public BattleNetLibrarySettingsViewModel(BattleNetLibrary library, IPlayniteAPI api) : base(library, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                if (savedSettings.Version == 0)
                {
                    Logger.Debug("Updating battle.net settings from version 0.");
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
                Settings = new BattleNetLibrarySettings { Version = 1 };
            }
        }

        private void Login()
        {
            try
            {
                using (var view = PlayniteApi.WebViews.CreateView(400, 500))
                {
                    var api = new BattleNetAccountClient(view);
                    api.Login();
                }

                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            catch (Exception e) when (!Environment.IsDebugBuild)
            {
                Logger.Error(e, "Failed to authenticate user.");
            }
        }
    }
}
