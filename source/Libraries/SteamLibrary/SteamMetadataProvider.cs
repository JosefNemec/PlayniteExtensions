﻿using Playnite.SDK;
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
        private ILogger logger = LogManager.GetLogger();
        private SteamLibrary library;
        private SteamApiClient apiClient;

        public SteamMetadataProvider(SteamLibrary library)
        {
            this.library = library;
            apiClient = new SteamApiClient();
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
                return new MetadataProvider(apiClient).GetGameMetadata(
                    gameId.AppID,
                    library.SettingsViewModel.Settings.BackgroundSource,
                    library.SettingsViewModel.Settings.DownloadVerticalCovers,
                    library.SettingsViewModel.Settings.DownloadFallbackBannerCovers);
            }
        }
    }
}