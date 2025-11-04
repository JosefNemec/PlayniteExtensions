using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Models;
using Steam;
using SteamLibrary.SteamShared;
using System;

namespace SteamLibrary
{
    public class SteamMetadataProvider : LibraryMetadataProvider
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly SteamLibrary library;
        private readonly SteamApiClient apiClient;
        private readonly WebApiClient webApiClient;

        public SteamMetadataProvider(SteamLibrary library)
        {
            this.library = library;
            apiClient = new SteamApiClient(library.SettingsViewModel.Settings);
            webApiClient = new WebApiClient();
        }

        public override void Dispose()
        {
            try
            {
                apiClient.Logout();
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to logout Steam client.");
            }

            webApiClient.Dispose();
        }

        public override GameMetadata GetMetadata(Game game)
        {
            var gameId = game.ToSteamGameID();
            if (gameId.IsMod)
            {
                return SteamLibrary.GetInstalledModFromFolder(game.InstallDirectory, ModInfo.GetModTypeOfGameID(gameId));
            }
            else
            {
                return new MetadataProvider(apiClient, webApiClient, new SteamTagNamer(library, library.SettingsViewModel.Settings, new Downloader()), library.SettingsViewModel.Settings).GetGameMetadata(
                    gameId.AppID,
                    library.SettingsViewModel.Settings.BackgroundSource,
                    library.SettingsViewModel.Settings.DownloadVerticalCovers);
            }
        }
    }
}