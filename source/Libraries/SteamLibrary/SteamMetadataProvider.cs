using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Steam;

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
            apiClient = new SteamApiClient();
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
            var gameData = new Game("SteamGame")
            {
                GameId = game.GameId
            };

            var gameId = game.ToSteamGameID();
            if (gameId.IsMod)
            {
                return SteamLibrary.GetInstalledModFromFolder(game.InstallDirectory, ModInfo.GetModTypeOfGameID(gameId));
            }
            else
            {
                return new MetadataProvider(apiClient, webApiClient).GetGameMetadata(
                    gameId.AppID,
                    library.SettingsViewModel.Settings.BackgroundSource,
                    library.SettingsViewModel.Settings.DownloadVerticalCovers);
            }
        }
    }
}