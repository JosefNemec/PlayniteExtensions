using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.Commands;
using SteamLibrary.Models;
using Playnite.SDK;
using System.Windows.Media;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Steam;
using System.Collections.ObjectModel;
using SteamLibrary.SteamShared;

namespace SteamLibrary
{
    public enum AuthStatus
    {
        Ok,
        Checking,
        AuthRequired,
        PrivateAccount,
        Failed
    }

    public class AdditionalSteamAcccount
    {
        public string AccountId { get; set; }
        public string ApiKey { get; set; }
        public bool ImportPlayTime { get; set; }
    }

    public class SteamLibrarySettings : SharedSteamSettings
    {
        private bool isPrivateAccount;
        private string apiKey = string.Empty;

        public int Version { get; set; }
        public bool ImportInstalledGames { get; set; } = true;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
        public string UserId { get; set; } = string.Empty;
        public bool IncludeFreeSubGames { get; set; } = false;
        public bool ShowFriendsButton { get; set; } = true;
        public bool IsPrivateAccount { get => isPrivateAccount; set => SetValue(ref isPrivateAccount, value); }
        public string ApiKey { get => apiKey; set => SetValue(ref apiKey, value); }
        public bool IgnoreOtherInstalled { get; set; }
        public ObservableCollection<AdditionalSteamAcccount> AdditionalAccounts { get; set; } = new ObservableCollection<AdditionalSteamAcccount>();
        public bool ShowGameLaunchMenu { get; set; } = false;
        public bool ShowGameLaunchMenuInFullscreen { get; set; } = false;
    }

    public class SteamLibrarySettingsViewModel : SharedSteamSettingsViewModel<SteamLibrarySettings, SteamLibrary>
    {
        public AuthStatus AuthStatus
        {
            get
            {
                if (Settings.UserId.IsNullOrEmpty())
                {
                    return AuthStatus.AuthRequired;
                }

                try
                {
                    if (Settings.IsPrivateAccount)
                    {
                        if (Settings.UserId.IsNullOrEmpty() || Settings.ApiKey.IsNullOrEmpty())
                        {
                            return AuthStatus.PrivateAccount;
                        }

                        try
                        {
                            var games = Plugin.GetPrivateOwnedGames(ulong.Parse(Settings.UserId), Settings.ApiKey, false);
                            if (games?.response?.games.HasItems() == true)
                            {
                                return AuthStatus.Ok;
                            }
                        }
                        catch (System.Net.WebException e)
                        {
                            if (e.Status == System.Net.WebExceptionStatus.ProtocolError)
                            {
                                return AuthStatus.PrivateAccount;
                            }
                        }
                    }
                    else
                    {
                        var games = Plugin.ServicesClient.GetSteamLibrary(ulong.Parse(Settings.UserId));
                        if (games.HasItems())
                        {
                            return AuthStatus.Ok;
                        }
                        else
                        {
                            return AuthStatus.PrivateAccount;
                        }
                    }
                }
                catch (Exception e) when (!Debugger.IsAttached)
                {
                    Logger.Error(e, "Failed to check Steam auth status.");
                    return AuthStatus.Failed;
                }

                return AuthStatus.AuthRequired;
            }
        }

        public RelayCommand<object> LoginCommand
        {
            get => new RelayCommand<object>((a) =>
            {
                Login();
            });
        }

        public bool IsFirstRunUse { get; set; }

        public List<LocalSteamUser> SteamUsers { get; set; }

        public RelayCommand<LocalSteamUser> ImportSteamCategoriesCommand
        {
            get => new RelayCommand<LocalSteamUser>((a) =>
            {
                ImportSteamCategories(a);
            });
        }

        public RelayCommand<LocalSteamUser> ImportSteamLastActivityCommand
        {
            get => new RelayCommand<LocalSteamUser>((a) =>
            {
                ImportSteamLastActivity(a);
            });
        }

        public RelayCommand AddAccountCommand
        {
            get => new RelayCommand(() =>
            {
                Settings.AdditionalAccounts.Add(new AdditionalSteamAcccount());
            });
        }

        public RelayCommand<AdditionalSteamAcccount> RemoveAccountCommand
        {
            get => new RelayCommand<AdditionalSteamAcccount>((a) =>
            {
                Settings.AdditionalAccounts.Remove(a);
            });
        }

        public SteamLibrarySettingsViewModel(SteamLibrary library, IPlayniteAPI api) : base(library, api)
        {
            Settings.PropertyChanged += Settings_PropertyChanged;
        }

        protected override void OnLoadSettings()
        {
            if (Settings.Version == 0)
            {
                Logger.Debug("Updating Steam settings from version 0.");
                if (Settings.ImportUninstalledGames)
                {
                    Settings.ConnectAccount = true;
                }
            }

            Settings.Version = 1;
        }

        protected override void OnInitSettings()
        {
            Settings.Version = 1;
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SteamLibrarySettings.IsPrivateAccount) ||
                e.PropertyName == nameof(SteamLibrarySettings.ApiKey))
            {
                OnPropertyChanged(nameof(AuthStatus));
            }
        }

        public override bool VerifySettings(out List<string> errors)
        {
            if (Settings.IsPrivateAccount && Settings.ApiKey.IsNullOrEmpty())
            {
                errors = new List<string>{ "Steam API key must be specified when using private accounts!" };
                return false;
            }

            return base.VerifySettings(out errors);
        }

        public void ImportSteamCategories(LocalSteamUser user)
        {
            var accId = user == null ? 0 : user.Id;
            Plugin.ImportSteamCategories(accId);
        }

        public void ImportSteamLastActivity(LocalSteamUser user)
        {
            var accId = user == null ? 0 : user.Id;
            Plugin.ImportSteamLastActivity(accId);
        }

        private void Login()
        {
            try
            {
                var steamId = string.Empty;
                using (var view = PlayniteApi.WebViews.CreateView(675, 440, Colors.Black))
                {
                    view.LoadingChanged += async (s, e) =>
                    {
                        var address = view.GetCurrentAddress();
                        if (address.Contains(@"steamcommunity.com"))
                        {
                            var source = await view.GetPageSourceAsync();
                            var idMatch = Regex.Match(source, @"g_steamID = ""(\d+)""");
                            if (idMatch.Success)
                            {
                                steamId = idMatch.Groups[1].Value;
                            }
                            else
                            {
                                idMatch = Regex.Match(source, @"steamid"":""(\d+)""");
                                if (idMatch.Success)
                                {
                                    steamId = idMatch.Groups[1].Value;
                                }
                            }

                            if (idMatch.Success)
                            {
                                view.Close();
                            }
                        }
                    };

                    view.DeleteDomainCookies(".steamcommunity.com");
                    view.DeleteDomainCookies("steamcommunity.com");
                    view.Navigate(@"https://steamcommunity.com/login/home/?goto=");
                    view.OpenDialog();
                }

                if (!steamId.IsNullOrEmpty())
                {
                    Settings.UserId = steamId;
                }

                OnPropertyChanged(nameof(AuthStatus));
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(PlayniteApi.Resources.GetString(LOC.SteamNotLoggedInError), "");
                Logger.Error(e, "Failed to authenticate user.");
            }
        }

        public override void EndEdit()
        {
            base.EndEdit();
            Plugin.TopPanelFriendsButton.Visible = Settings.ShowFriendsButton;
        }
    }
}