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
using Playnite.SDK.Data;
using PlayniteExtensions.Common;
using System.Security.Principal;
using Playnite.Common;

namespace SteamLibrary
{
    public class AdditionalSteamAcccount
    {
        public string AccountId { get; set; }
        public bool ImportPlayTime { get; set; }
        [Obsolete] public string ApiKey { get; set; }
        [DontSerialize] public string RutnimeApiKey { get; set; }
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
        public ObservableCollection<AdditionalSteamAcccount> AdditionalAccounts { get; set; } = new ObservableCollection<AdditionalSteamAcccount>();
        public bool ShowSteamLaunchMenuInDesktopMode { get; set; } = true;
        public bool ShowSteamLaunchMenuInFullscreenMode { get; set; } = false;
        [Obsolete] public string ApiKey { get; set; }
        [DontSerialize] public string RutnimeApiKey { get => apiKey; set => SetValue(ref apiKey, value); }

        public bool IsPrivateAccount
        {
            get => isPrivateAccount;
            set
            {
                if (isPrivateAccount != value)
                {
                    isPrivateAccount = value;
                    OnPropertyChanged(nameof(IsPrivateAccount));
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
                        Logger.Warn("-------- private");
                        var res = Plugin.GetPrivateOwnedGames(ulong.Parse(Settings.UserId), Settings.RutnimeApiKey, true);
                        return res.response?.games.HasItems() == true;
                    }
                    else
                    {
                        Logger.Warn("-------- public");
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
                Settings.RutnimeApiKey = Settings.ApiKey;
                Settings.AdditionalAccounts.ForEach(a => a.RutnimeApiKey = a.ApiKey);
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
                Settings.RutnimeApiKey = keys.MainAccount;
                Settings.AdditionalAccounts.ForEach(a =>
                {
                    if (keys.Accounts.TryGetValue(a.AccountId, out var key))
                    {
                        a.RutnimeApiKey = key;
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
                MainAccount = Settings.RutnimeApiKey
            };

            if (Settings.AdditionalAccounts.HasItems())
            {
                foreach (var account in Settings.AdditionalAccounts.Where(a => !a.AccountId.IsNullOrWhiteSpace() && !a.RutnimeApiKey.IsNullOrWhiteSpace()))
                {
                    if (!keys.Accounts.ContainsKey(account.AccountId))
                    {
                        keys.Accounts.Add(account.AccountId, account.RutnimeApiKey);
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
                e.PropertyName == nameof(SteamLibrarySettings.RutnimeApiKey) ||
                e.PropertyName == nameof(SteamLibrarySettings.UserId))
            {
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
        }

        public override bool VerifySettings(out List<string> errors)
        {
            if (Settings.IsPrivateAccount && Settings.RutnimeApiKey.IsNullOrEmpty())
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
            EditingClone.RutnimeApiKey = Settings.RutnimeApiKey;
            for (int i = 0; i < Settings.AdditionalAccounts.Count; i++)
            {
                EditingClone.AdditionalAccounts[i].RutnimeApiKey = Settings.AdditionalAccounts[i].RutnimeApiKey;
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