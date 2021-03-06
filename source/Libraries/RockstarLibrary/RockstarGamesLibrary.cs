﻿using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RockstarGamesLibrary
{
    [LoadPlugin]
    public class RockstarGamesLibrary : LibraryPluginBase<RockstarGamesLibrarySettingsViewModel>
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public RockstarGamesLibrary(IPlayniteAPI api) : base(
            "Rockstar Games",
            Guid.Parse("88409022-088a-4de8-805a-fdbac291f00a"),
            new LibraryPluginCapabilities { CanShutdownClient = true },
            new RockstarGamesLibraryClient(),
            RockstarGames.Icon,
            null,
            api)
        {
        }

        internal static IEnumerable<GameInfo> GetInstalledGames()
        {
            var games = new List<GameInfo>();
            foreach (var app in Programs.GetUnistallProgramsList())
            {
                if (string.IsNullOrEmpty(app.UninstallString))
                {
                    continue;
                }

                var match = Regex.Match(app.UninstallString, @"Launcher\.exe.+uninstall=(.+)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var titleId = match.Groups[1].Value;
                    var rsGame = RockstarGames.Games.FirstOrDefault(a => a.TitleId == titleId);
                    if (rsGame == null)
                    {
                        logger.Warn($"Uknown Rockstar game with titleid {titleId}");
                        continue;
                    }

                    var newGame = new GameInfo
                    {
                        IsInstalled = true,
                        InstallDirectory = app.InstallLocation,
                        Source = "Rockstar Games",
                        Name = rsGame.Name,
                        GameId = titleId
                    };

                    if (!string.IsNullOrEmpty(app.DisplayIcon))
                    {
                        var iconPath = app.DisplayIcon.Trim(new char[] { '"' });
                        if (File.Exists(iconPath))
                        {
                            newGame.Icon = iconPath;
                        }
                    }

                    games.Add(newGame);
                }
            }

            return games;
        }

        public override IEnumerable<GameInfo> GetGames()
        {
            var games = new List<GameInfo>();
            Exception importError = null;

            try
            {
                var installed = GetInstalledGames();
                if (installed.Any())
                {
                    games.AddRange(installed);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to import rockstar games.");
                importError = e;
            }

            if (importError != null)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    ImportErrorMessageId,
                    string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                    Environment.NewLine + importError.Message,
                    NotificationType.Error));
            }
            else
            {
                PlayniteApi.Notifications.Remove(ImportErrorMessageId);
            }

            return games;
        }

        public override List<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                return null;
            }

            return new List<InstallController> { new RockstarInstallController(args.Game) };
        }

        public override List<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                return null;
            }

            return new List<UninstallController> { new RockstarUninstallController(args.Game) };
        }

        public override List<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (args.Game.PluginId != Id)
            {
                return null;
            }

            return new List<PlayController> { new RockstarPlayController(args.Game) };
        }
    }
}