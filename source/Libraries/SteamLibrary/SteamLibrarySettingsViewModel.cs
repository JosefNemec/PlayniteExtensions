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

    public class SteamLibrarySettings : ObservableObject
    {
        public int Version { get; set; }
        public bool DownloadVerticalCovers { get; set; } = true;
        public bool ImportInstalledGames { get; set; } = true;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
        public BackgroundSource BackgroundSource { get; set; } = BackgroundSource.Image;
        public string UserId { get; set; } = string.Empty;
        public bool IncludeFreeSubGames { get; set; } = false;
        public bool ShowFriendsButton { get; set; } = true;

        private bool isPrivateAccount;
        public bool IsPrivateAccount
        {
            get => isPrivateAccount;
            set
            {
                isPrivateAccount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AuthStatus));
            }
        }

        private string apiKey = string.Empty;
        public string ApiKey
        {
            get => apiKey;
            set
            {
                apiKey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AuthStatus));
            }
        }
    }

    public class SteamLibrarySettingsViewModel : PluginSettingsViewModel<SteamLibrarySettings, SteamLibrary>
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

        public SteamLibrarySettingsViewModel(SteamLibrary library, IPlayniteAPI api) : base(library, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                if (savedSettings.Version == 0)
                {
                    Logger.Debug("Updating Steam settings from version 0.");
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
                Settings = new SteamLibrarySettings() { Version = 1 };
            }
        }

        public override bool VerifySettings(out List<string> errors)
        {
            if (Settings.IsPrivateAccount && Settings.ApiKey.IsNullOrEmpty())
            {
                errors = new List<string>{ "Steam API key must be specified when using private accounts!" };
                return false;
            }

            errors = null;
            return true;
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