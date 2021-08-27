using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PSNLibrary.Models;
using PSNLibrary.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PSNLibrary
{
    [LoadPlugin]
    public class PSNLibrary : LibraryPluginBase<PSNLibrarySettingsViewModel>
    {
        public PSNLibrary(IPlayniteAPI api) : base(
            "PlayStation",
            Guid.Parse("e4ac81cb-1b1a-4ec9-8639-9a9633989a71"),
            new LibraryPluginProperties { CanShutdownClient = false, HasCustomizedGameImport = true, HasSettings = true },
            null,
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png"),
            (_) => new PSNLibrarySettingsView(),
            api)
        {
            SettingsViewModel = new PSNLibrarySettingsViewModel(this, api);
        }

        private List<string> ParseOldPlatform(PlatformHash ids)
        {
            if (ids.HasFlag(PlatformHash.PS3))
            {
                return new List<string> { "sony_playstation3" };
            }
            else if (ids.HasFlag(PlatformHash.PSVITA))
            {
                return new List<string> { "sony_vita" };
            }
            else if (ids.HasFlag(PlatformHash.PSP))
            {
                return new List<string> { "sony_psp" };
            }

            return null;
        }

        private string FixGameName(string name)
        {
            var gameName = name.
                RemoveTrademarks(" ").
                NormalizeGameName().
                Replace("full game", "", StringComparison.OrdinalIgnoreCase).
                Trim();
            return Regex.Replace(gameName, @"\s+", " ");
        }

        private List<GameInfo> ParseAccountList(PSNAccountClient clientApi)
        {
            var parsedGames = new List<GameInfo>();
            foreach (var title in clientApi.GetAccountTitles().GetAwaiter().GetResult())
            {
                var gameName = FixGameName(title.name);
                parsedGames.Add(new GameInfo
                {
                    GameId = "#ACCOUNT#" + title.titleId,
                    Name = gameName
                });
            }

            return parsedGames;
        }

        private List<GameInfo> ParseThrophies(PSNAccountClient clientApi)
        {
            var parsedGames = new List<GameInfo>();
            foreach (var title in clientApi.GetThropyTitles().GetAwaiter().GetResult())
            {
                var gameName = FixGameName(title.trophyTitleName);
                gameName = gameName.
                    TrimEndString("Trophies", StringComparison.OrdinalIgnoreCase).
                    TrimEndString("Trophy", StringComparison.OrdinalIgnoreCase).
                    Trim();
                var newGame = new GameInfo
                {
                    GameId = "#TROPHY#" + title.npCommunicationId,
                    Name = gameName
                };

                if (title.trophyTitlePlatfrom?.Contains("PS4") == true)
                {
                    newGame.Platforms = new List<string> { "sony_playstation4" };
                }
                else if (title.trophyTitlePlatfrom?.Contains("PS3") == true)
                {
                    newGame.Platforms = new List<string> { "sony_playstation3" };
                }
                else if (title.trophyTitlePlatfrom?.Contains("PSVITA") == true)
                {
                    newGame.Platforms = new List<string> { "sony_vita" };
                }
                else if (title.trophyTitlePlatfrom?.Contains("PSP") == true)
                {
                    newGame.Platforms = new List<string> { "sony_psp" };
                }

                parsedGames.Add(newGame);
            }

            return parsedGames;
        }

        private List<GameInfo> ParseDownloadList(PSNAccountClient clientApi)
        {
            var parsedGames = new List<GameInfo>();
            foreach (var item in clientApi.GetDownloadList().GetAwaiter().GetResult())
            {
                if (item.drm_def?.contentType == "TV")
                {
                    continue;
                }
                else if (item.entitlement_type == 1 || item.entitlement_type == 4) // Not games
                {
                    continue;
                }
                else if (item.game_meta?.package_sub_type == "MISC_THEME" ||
                         item.game_meta?.package_sub_type == "MISC_AVATAR")
                {
                    continue;
                }

                var newGame = new GameInfo();
                newGame.GameId = "#DLIST#" + item.id;

                if (item.entitlement_attributes != null) // PS4
                {
                    newGame.Name = item.game_meta.name;
                    newGame.Platforms = new List<string> { "sony_playstation4" };
                }
                else if (item.drm_def != null) //PS3, PSP, or Vita
                {
                    newGame.Name = item.drm_def.contentName;
                    if (item.drm_def.drmContents.HasItems())
                    {
                        newGame.Platforms = ParseOldPlatform(item.drm_def.drmContents[0].platformIds);
                    }
                }
                else
                {
                    continue;
                }

                if (newGame.Name.IsNullOrEmpty())
                {
                    continue;
                }

                if (newGame.Name.Contains("demo", StringComparison.OrdinalIgnoreCase) ||
                    newGame.Name.Contains("trial", StringComparison.OrdinalIgnoreCase) ||
                    newGame.Name.Contains("language pack", StringComparison.OrdinalIgnoreCase) ||
                    newGame.Name.Contains("skin pack", StringComparison.OrdinalIgnoreCase) ||
                    newGame.Name.Contains("multiplayer pack", StringComparison.OrdinalIgnoreCase) ||
                    newGame.Name.Contains("compatibility pack", StringComparison.OrdinalIgnoreCase) ||
                    newGame.Name.EndsWith("theme", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                newGame.Name = FixGameName(newGame.Name);
                parsedGames.Add(newGame);
            }

            return parsedGames;
        }

        public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args)
        {
            var importedGames = new List<Game>();
            Exception importError = null;
            if (!SettingsViewModel.Settings.ConnectAccount)
            {
                return importedGames;
            }

            try
            {
                var clientApi = new PSNAccountClient(this);
                var allGames = new List<GameInfo>();
                allGames.AddRange(ParseAccountList(clientApi));
                allGames.AddRange(ParseThrophies(clientApi));

                foreach (var group in allGames.GroupBy(a => a.Name.ToLower().Replace(":", "")))
                {
                    var game = group.First();
                    if (PlayniteApi.ApplicationSettings.GetGameExcludedFromImport(game.GameId, Id))
                    {
                        continue;
                    }

                    var alreadyImported = PlayniteApi.Database.Games.FirstOrDefault(a => a.GameId == game.GameId && a.PluginId == Id);
                    if (alreadyImported == null)
                    {
                        game.Source = "PlayStation";
                        importedGames.Add(PlayniteApi.Database.ImportGame(game, this));
                    }
                }
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                Logger.Error(e, "Failed to import PSN games.");
                importError = e;
            }

            if (importError != null)
            {
                PlayniteApi.Notifications.Add(new NotificationMessage(
                    ImportErrorMessageId,
                    string.Format(PlayniteApi.Resources.GetString("LOCLibraryImportError"), Name) +
                    System.Environment.NewLine + importError.Message,
                    NotificationType.Error,
                    () => OpenSettingsView()));
            }
            else
            {
                PlayniteApi.Notifications.Remove(ImportErrorMessageId);
            }

            return importedGames;
        }
    }
}