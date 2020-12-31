using Playnite;
using Playnite.SDK;
using Playnite.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLibrary.Services;
using Playnite.Common;

namespace TwitchLibrary
{
    public class TwitchLibrarySettings
    {
        public int Version { get; set; }
        public bool ImportInstalledGames { get; set; } = true;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
        public bool StartGamesWithoutLauncher { get; set; } = false;
    }

    public class TwitchLibrarySettingsViewModel : PluginSettingsViewModel<TwitchLibrarySettings, TwitchLibrary>
    {
        public bool IsFirstRunUse { get; set; }

        public bool IsUserLoggedIn
        {
            get
            {
                var token = Plugin.GetAuthToken();
                if (token.IsNullOrEmpty())
                {
                    return false;
                }
                else
                {
                    try
                    {
                        AmazonEntitlementClient.GetAccountEntitlements(token);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
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

        public TwitchLibrarySettingsViewModel(TwitchLibrary library, IPlayniteAPI api) : base(library, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                if (savedSettings.Version == 0)
                {
                    Logger.Debug("Updating Twitch settings from version 0.");
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
                Settings = new TwitchLibrarySettings() { Version = 1 };
            }
        }

        public class Cookie
        {
            public string value { get; set; }
        }

        private void Login()
        {
            try
            {
                if (!Twitch.IsInstalled)
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        string.Format(PlayniteApi.Resources.GetString("LOCClientNotInstalledError"), "Twitch"),
                        "");
                    return;
                }

                PlayniteApi.Dialogs.ShowMessage(string.Format(PlayniteApi.Resources.GetString("LOCSignInExternalNotif"), "Twitch"));
                Twitch.StartClient();
                PlayniteApi.Dialogs.ShowMessage(PlayniteApi.Resources.GetString("LOCSignInExternalWaitMessage"));
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            catch (Exception e) when (!Environment.IsDebugBuild)
            {
                Logger.Error(e, "Failed to authenticate user.");
            }
        }
    }
}
