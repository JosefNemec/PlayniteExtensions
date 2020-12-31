using Playnite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.Commands;
using Playnite.SDK;
using GogLibrary.Services;
using Playnite.SDK.Data;

namespace GogLibrary
{
    public class GogLibrarySettings
    {
        public int Version { get; set; }
        public bool ImportInstalledGames { get; set; } = true;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
        public bool StartGamesUsingGalaxy { get; set; } = false;
    }
    public class GogLibrarySettingsViewModel : PluginSettingsViewModel<GogLibrarySettings, GogLibrary>
    {
        public bool IsUserLoggedIn
        {
            get
            {
                using (var view = PlayniteApi.WebViews.CreateOffscreenView())
                {
                    var api = new GogAccountClient(view);
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

        public GogLibrarySettingsViewModel(GogLibrary library, IPlayniteAPI api) : base(library, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                if (savedSettings.Version == 0)
                {
                    Logger.Debug("Updating GOG settings from version 0.");
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
                Settings = new GogLibrarySettings { Version = 1 };
            }
        }

        private void Login()
        {
            try
            {
                using (var view = PlayniteApi.WebViews.CreateView(400, 445))
                {
                    var api = new GogAccountClient(view);
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
