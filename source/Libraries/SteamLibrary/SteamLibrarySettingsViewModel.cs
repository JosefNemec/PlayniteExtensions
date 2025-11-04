using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using PlayniteExtensions.Common;
using SteamLibrary.SteamShared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace SteamLibrary
{
    public class AdditionalSteamAccount
    {
        public string AccountId { get; set; }
        public bool ImportPlayTime { get; set; }
        [Obsolete] public string ApiKey { get; set; }
        [DontSerialize] public string RuntimeApiKey { get; set; }
    }

    public class SteamLibrarySettings : SharedSteamSettings
    {
        private bool isPrivateAccount;
        private string apiKey = string.Empty;
        private string userId = string.Empty;

        public int Version { get; set; }
        public bool ImportInstalledGames { get; set; } = true;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
        public string UserId { get => userId; set => SetValue(ref userId, value); }
        public bool IncludeFreeSubGames { get; set; } = false;
        public bool ShowFriendsButton { get; set; } = true;
        public bool IgnoreOtherInstalled { get; set; }
        public ObservableCollection<AdditionalSteamAccount> AdditionalAccounts { get; set; } = new ObservableCollection<AdditionalSteamAccount>();
        public bool ShowSteamLaunchMenuInDesktopMode { get; set; } = true;
        public bool ShowSteamLaunchMenuInFullscreenMode { get; set; } = false;
        public List<string> ExtraIDsToImport { get; set; }
        [Obsolete] public string ApiKey { get; set; }
        [DontSerialize] public string RuntimeApiKey { get => apiKey; set => SetValue(ref apiKey, value); }

        public bool IsPrivateAccount
        {
            get => isPrivateAccount;
            set
            {
                if (isPrivateAccount != value)
                {
                    isPrivateAccount = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    public class ApiKeyInfo
    {
        public string MainAccount { get; set; }
        public Dictionary<string, string> Accounts { get; set; } = new Dictionary<string, string>();
    }

    public class SteamLibrarySettingsViewModel : SharedSteamSettingsViewModel<SteamLibrarySettings, SteamLibrary>
    {
        public bool IsUserLoggedIn
        {
            get
            {
                try
                {
                    if (Settings.IsPrivateAccount)
                    {
                        var res = Plugin.GetPrivateOwnedGames(ulong.Parse(Settings.UserId), Settings.RuntimeApiKey, true);
                        return res.response?.games.HasItems() == true;
                    }
                    else
                    {
                        var res = Plugin.GetLibraryGamesViaProfilePage(Settings);
                        return res.bViewingOwnProfile;
                    }
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
            get => new RelayCommand<object>((a) =>
            {
                Login();
            });
        }

        public bool IsFirstRunUse { get; set; }

        public RelayCommand AddAccountCommand
        {
            get => new RelayCommand(() =>
            {
                Settings.AdditionalAccounts.Add(new AdditionalSteamAccount());
            });
        }

        public RelayCommand<AdditionalSteamAccount> RemoveAccountCommand
        {
            get => new RelayCommand<AdditionalSteamAccount>((a) =>
            {
                Settings.AdditionalAccounts.Remove(a);
            });
        }

        public SteamLibrarySettingsViewModel(SteamLibrary library, IPlayniteAPI api) : base(library, api)
        {
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
            else if (Settings.Version == 1)
            {
#pragma warning disable CS0612 // Type or member is obsolete
                Settings.RuntimeApiKey = Settings.ApiKey;
                Settings.AdditionalAccounts.ForEach(a => a.RuntimeApiKey = a.ApiKey);
                Settings.ApiKey = null;
                Settings.AdditionalAccounts.ForEach(a => a.ApiKey = null);
#pragma warning restore CS0612 // Type or member is obsolete

                SaveKeys();
                Settings.Version = 2;
                Plugin.SavePluginSettings(Settings);
            }

            Settings.Version = 2;
            LoadKeys();
        }

        private void LoadKeys()
        {
            if (!FileSystem.FileExists(ApiKeysPath))
            {
                return;
            }

            try
            {
                var str = Encryption.DecryptFromFile(
                    ApiKeysPath,
                    Encoding.UTF8,
                    WindowsIdentity.GetCurrent().User.Value);
                var keys = Serialization.FromJson<ApiKeyInfo>(str);
                Settings.RuntimeApiKey = keys.MainAccount;
                Settings.AdditionalAccounts.ForEach(a =>
                {
                    if (keys.Accounts.TryGetValue(a.AccountId, out var key))
                    {
                        a.RuntimeApiKey = key;
                    }
                });
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to load Steam API keys.");
            }
        }

        private void SaveKeys()
        {
            var keys = new ApiKeyInfo
            {
                MainAccount = Settings.RuntimeApiKey
            };

            if (Settings.AdditionalAccounts.HasItems())
            {
                foreach (var account in Settings.AdditionalAccounts.Where(a => !a.AccountId.IsNullOrWhiteSpace() && !a.RuntimeApiKey.IsNullOrWhiteSpace()))
                {
                    if (!keys.Accounts.ContainsKey(account.AccountId))
                    {
                        keys.Accounts.Add(account.AccountId, account.RuntimeApiKey);
                    }
                }
            }

            FileSystem.PrepareSaveFile(ApiKeysPath);
            Encryption.EncryptToFile(
                ApiKeysPath,
                Serialization.ToJson(keys),
                Encoding.UTF8,
                WindowsIdentity.GetCurrent().User.Value);
        }

        protected override void OnInitSettings()
        {
            Settings.Version = 2;
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SteamLibrarySettings.IsPrivateAccount) ||
                e.PropertyName == nameof(SteamLibrarySettings.RuntimeApiKey) ||
                e.PropertyName == nameof(SteamLibrarySettings.UserId))
            {
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
        }

        public override bool VerifySettings(out List<string> errors)
        {
            if (Settings.IsPrivateAccount && Settings.RuntimeApiKey.IsNullOrEmpty())
            {
                errors = new List<string>{ "Steam API key must be specified when using private accounts!" };
                return false;
            }

            return base.VerifySettings(out errors);
        }

        private void Login()
        {
            try
            {
                var steamId = string.Empty;
                using (var view = PlayniteApi.WebViews.CreateView(675, 640, Colors.Black))
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
                    view.DeleteDomainCookies("steampowered.com");
                    view.DeleteDomainCookies("store.steampowered.com");
                    view.DeleteDomainCookies("help.steampowered.com");
                    view.DeleteDomainCookies("login.steampowered.com");
                    view.Navigate(@"https://steamcommunity.com/login/home/?goto=");
                    view.OpenDialog();
                }

                if (!steamId.IsNullOrEmpty())
                {
                    Settings.UserId = steamId;
                }
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(PlayniteApi.Resources.GetString(LOC.SteamNotLoggedInError), "");
                Logger.Error(e, "Failed to authenticate user.");
            }
        }

        public override void BeginEdit()
        {
            base.BeginEdit();
            EditingClone.RuntimeApiKey = Settings.RuntimeApiKey;
            for (int i = 0; i < Settings.AdditionalAccounts.Count; i++)
            {
                EditingClone.AdditionalAccounts[i].RuntimeApiKey = Settings.AdditionalAccounts[i].RuntimeApiKey;
            }

            Settings.PropertyChanged += Settings_PropertyChanged;
        }

        public override void CancelEdit()
        {
            Settings.PropertyChanged -= Settings_PropertyChanged;
            base.CancelEdit();
        }

        public override void EndEdit()
        {
            Settings.PropertyChanged -= Settings_PropertyChanged;
            SaveKeys();
            base.EndEdit();
            Plugin.TopPanelFriendsButton.Visible = Settings.ShowFriendsButton;
        }
    }
}