using Playnite;
using Playnite.SDK;
using Playnite.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.SDK.Data;
using ItchioLibrary.Models;

namespace ItchioLibrary
{
    public class ItchioLibrarySettings
    {
        public int Version { get; set; }
        public bool ImportInstalledGames { get; set; } = Itch.IsInstalled;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
        public bool ImportFreeGamesFromCollections { get; set; } = false;
        public List<GameClassification> ImportGameClassification { get; set; } = new List<GameClassification>() { GameClassification.game, GameClassification.tool };
    }

    public class GameClassificationItem
    {
        public bool IsChecked { get; set; }
        public GameClassification Value { get; set; }
    }

    public class ItchioLibrarySettingsViewModel : PluginSettingsViewModel<ItchioLibrarySettings, ItchioLibrary>
    {
        public List<GameClassificationItem> GameClassificationList { get; private set; }
        
        public bool IsUserLoggedIn
        {
            get
            {
                if (!Itch.IsInstalled)
                {
                    return false;
                }

                using (var butler = new Butler())
                {
                    return butler.GetProfiles().Count > 0;
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

        public RelayCommand<GameClassificationItem> ToggleGameClassificationCommand
        {
            get => new RelayCommand<GameClassificationItem>((item) =>
			{
                ToggleGameClassification(item);
            });
        }

        private void ToggleGameClassification(GameClassificationItem item)
        {
			if (item.IsChecked)
			{
                Settings.ImportGameClassification.Add(item.Value);
			}
			else
			{
                Settings.ImportGameClassification.Remove(item.Value);
            }
        }

        public ItchioLibrarySettingsViewModel(ItchioLibrary library, IPlayniteAPI api) : base(library, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                if (savedSettings.Version == 0)
                {
                    Logger.Debug("Updating itch settings from version 0.");
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
                Settings = new ItchioLibrarySettings { Version = 1 };
            }

			GameClassificationList = ((GameClassification[])Enum.GetValues(typeof(GameClassification))).Select(a =>
            {
                return new GameClassificationItem()
                {
                    Value = a,
                    IsChecked = Settings.ImportGameClassification.Contains(a)
                };
            }).ToList();
        }

        private void Login()
        {
            try
            {
                if (!Itch.IsInstalled)
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        PlayniteApi.Resources.GetString(LOC.ItchioClientNotInstalledError), "");
                    return;
                }

                PlayniteApi.Dialogs.ShowMessage(PlayniteApi.Resources.GetString(LOC.ItchioSignInNotif));
                Itch.StartClient();
                PlayniteApi.Dialogs.ShowMessage(PlayniteApi.Resources.GetString(LOC.ItchioSignInWaitMessage));
                OnPropertyChanged(nameof(IsUserLoggedIn));
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(PlayniteApi.Resources.GetString(LOC.itchioNotLoggedInError), "");
                Logger.Error(e, "Failed to authenticate itch.io user.");
            }
        }
    }
}
