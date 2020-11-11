using Playnite.Common;
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
    public class RockstarGamesLibrary : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string dbImportMessageId = "bnetlibImportError";

        public override Guid Id { get; } = Guid.Parse("88409022-088a-4de8-805a-fdbac291f00a");

        public override string Name => "Rockstar Games";

        public override LibraryClient Client { get; } = new RockstarGamesLibraryClient();

        public override LibraryPluginCapabilities Capabilities { get; } = new LibraryPluginCapabilities
        {
            CanShutdownClient = true
        };

        public RockstarGamesLibrary(IPlayniteAPI api) : base(api)
        {
        }

        internal IEnumerable<GameInfo> GetInstalledGames()
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
                        GameId = titleId,
                        PlayAction = new GameAction
                        {
                            Type = GameActionType.File,
                            Path = rsGame.Executable,
                            WorkingDir = ExpandableVariables.InstallationDirectory,
                            IsHandledByPlugin = true
                        }
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
                logger.Error(e, "Failed to import rockstar games.");
                importError = e;
            }

            if (importError != null)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    dbImportMessageId,
                    string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                    Environment.NewLine + importError.Message,
                    NotificationType.Error));
            }
            else
            {
                PlayniteApi.Notifications.Remove(dbImportMessageId);
            }

            return games;
        }

        public override IGameController GetGameController(Game game)
        {
            return new RockstarGamesController(this, game);
        }
    }
}