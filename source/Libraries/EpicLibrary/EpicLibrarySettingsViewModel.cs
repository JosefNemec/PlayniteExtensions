using EpicLibrary.Services;
using Playnite;
using Playnite.SDK;
using Playnite.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using Playnite.SDK.Data;

namespace EpicLibrary
{
    public class EpicLibrarySettings
    {
        public int Version { get; set; }
        public bool ImportInstalledGames { get; set; } = EpicLauncher.IsInstalled;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
    }

    public class EpicLibrarySettingsViewModel : PluginSettingsViewModel<EpicLibrarySettings, EpicLibrary>
    {
        public bool IsUserLoggedIn
        {
            get
            {
                return new EpicAccountClient(PlayniteApi, Plugin.TokensPath).GetIsUserLoggedIn();
            }
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                Login();
            });
        }

        public EpicLibrarySettingsViewModel(EpicLibrary library, IPlayniteAPI api) : base(library, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                if (savedSettings.Version == 0)
                {
                    Logger.Debug("Updating Epic settings from version 0.");
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
                Settings = new EpicLibrarySettings { Version = 1 };
            }
        }

        private void Login()
        {
            try
            {
                var clientApi = new EpicAccountClient(PlayniteApi, Plugin.TokensPath);
                clientApi.Login();
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(PlayniteApi.Resources.GetString(LOC.EpicNotLoggedInError), "");
                Logger.Error(e, "Failed to authenticate user.");
            }
        }
    }
}
