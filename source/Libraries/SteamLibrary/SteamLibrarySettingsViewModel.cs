using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.SDK;
using System.Collections.ObjectModel;
using SteamLibrary.SteamShared;
using Playnite.SDK.Data;
using PlayniteExtensions.Common;
using System.Security.Principal;
using Playnite.Common;
using SteamLibrary.Services;

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
        private bool useApiLogin;
        private bool importGamesOwn = true;
        private bool importFreeOwn = true;
        private bool importToolsOwn;
        private bool importToolsFamily;
        private string apiKey = string.Empty;
        private string userId = string.Empty;

        public int Version { get; set; }
        [Obsolete]public bool ImportInstalledGames { get; set; } = true;
        [Obsolete]public bool ImportFamilySharedGames { get; set; } = true;
        public bool ConnectAccount { get; set; } = false;
        [Obsolete]public bool ImportUninstalledGames { get; set; } = false;
        public string UserId { get => userId; set => SetValue(ref userId, value); }
        [Obsolete]public bool IncludeFreeSubGames { get; set; } = false;
        public bool ShowFriendsButton { get; set; } = true;
        [Obsolete]public bool IgnoreOtherInstalled { get; set; }
        public ObservableCollection<AdditionalSteamAccount> AdditionalAccounts { get; set; } = new ObservableCollection<AdditionalSteamAccount>();
        public bool ShowSteamLaunchMenuInDesktopMode { get; set; } = true;
        public bool ShowSteamLaunchMenuInFullscreenMode { get; set; } = false;
        public List<string> ExtraIDsToImport { get; set; }
        [Obsolete] public string ApiKey { get; set; }
        [DontSerialize] public string RuntimeApiKey { get => apiKey; set => SetValue(ref apiKey, value); }
        [Obsolete] public bool IsPrivateAccount { get; set; }

        public bool ImportInstalled { get; set; } = true;
        public bool ImportInstalledMods { get; set; } = true;
        public bool ImportInstalledIgnoreOthers { get; set; }
        public bool ImportGamesOwn { get => importGamesOwn; set => SetValue(ref importGamesOwn, value); }
        public bool ImportGamesFamily { get; set; } = true;
        public bool ImportAppsOwn { get; set; } = true;
        public bool ImportMediaOwn { get; set; }
        public bool ImportFreeOwn { get => importFreeOwn; set => SetValue(ref importFreeOwn, value); }
        public bool ImportFreeFamily { get; set; }
        public bool ImportToolsOwn { get => importToolsOwn; set => SetValue(ref importToolsOwn, value); }
        public bool ImportToolsFamily { get => importToolsFamily; set => SetValue(ref importToolsFamily, value); }

        public bool UseApiLogin
        {
            get => useApiLogin;
            set => SetValue(ref useApiLogin, value);
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
                    if (Settings.UseApiLogin)
                    {
                        var res = new PlayerService().GetOwnedGamesApiKey(Settings, ulong.Parse(Settings.UserId), Settings.RuntimeApiKey);
                        return res?.Any() == true;
                    }
                    else
                    {
                        var userToken = Task.Run(async () => await new SteamStoreService(PlayniteApi).GetAccessTokenAsync()).GetAwaiter().GetResult();
                        return userToken.AccessToken != null;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "");
                    return false;
                }
            }
        }

        public bool ToolsWarning => Settings.ImportToolsOwn || Settings.ImportToolsFamily;

        public RelayCommand<object> LoginCommand => new RelayCommand<object>(_ =>
        {
            try
            {
                var token = new SteamStoreService(PlayniteApi).Login();
                if (token != null)
                    Settings.UserId = token.Value.UserId.ToString();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error logging in");
            }
        });

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
#pragma warning disable CS0612 // Type or member is obsolete
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
                Logger.Debug("Updating Steam settings from version 1.");
                Settings.RuntimeApiKey = Settings.ApiKey;
                Settings.AdditionalAccounts.ForEach(a => a.RuntimeApiKey = a.ApiKey);
                Settings.ApiKey = null;
                Settings.AdditionalAccounts.ForEach(a => a.ApiKey = null);

                SaveKeys();
                Settings.Version = 2;
                Plugin.SavePluginSettings(Settings);
            }
            else if (Settings.Version == 2)
            {
                Logger.Debug("Updating Steam settings from version 2.");
                Settings.ImportInstalled = Settings.ImportInstalledGames;
                Settings.ImportInstalledMods = Settings.ImportInstalledGames;
                Settings.ImportInstalledIgnoreOthers = Settings.IgnoreOtherInstalled;
                Settings.ImportGamesOwn = Settings.ImportUninstalledGames;
                Settings.ImportGamesFamily = Settings.ImportFamilySharedGames;
                Settings.ImportFreeOwn = Settings.IncludeFreeSubGames;
                Settings.UseApiLogin = Settings.IsPrivateAccount;
            }
#pragma warning restore CS0612 // Type or member is obsolete

            Settings.Version = 3;
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
            if (e.PropertyName == nameof(SteamLibrarySettings.UseApiLogin) ||
                e.PropertyName == nameof(SteamLibrarySettings.RuntimeApiKey) ||
                e.PropertyName == nameof(SteamLibrarySettings.UserId))
            {
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            else if(e.PropertyName == nameof(SteamLibrarySettings.ImportToolsOwn) ||
                    e.PropertyName == nameof(SteamLibrarySettings.ImportToolsFamily)
                    )
            {
                OnPropertyChanged(nameof(ToolsWarning));
            }
        }

        public override bool VerifySettings(out List<string> errors)
        {
            if (Settings.UseApiLogin && Settings.RuntimeApiKey.IsNullOrEmpty())
            {
                errors = new List<string> { "Steam API key must be specified when using private accounts!" };
                return false;
            }

            return base.VerifySettings(out errors);
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
