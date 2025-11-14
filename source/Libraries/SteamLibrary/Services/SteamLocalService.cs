using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SteamLibrary.Services
{
    public static class SteamLocalService
    {
        private static readonly string[] firstPartyModPrefixes = { "bshift", "cstrike", "czero", "dmc", "dod", "gearbox", "ricochet", "tfc", "valve" };
        private static ILogger logger = LogManager.GetLogger();
        
        public static IDictionary<string, DateTime> GetGamesLastActivity(ulong steamId)
        {
            var id = new SteamID(steamId);
            var result = new Dictionary<string, DateTime>();
            var vdf = Path.Combine(Steam.InstallationPath, "userdata", id.AccountID.ToString(), "config", "localconfig.vdf");
            if (!FileSystem.FileExists(vdf))
            {
                return result;
            }

            var sharedConfig = new KeyValue();
            sharedConfig.ReadFileAsText(vdf);

            var apps = sharedConfig["Software"]["Valve"]["Steam"]["apps"];
            foreach (var app in apps.Children)
            {
                if (app.Children.Count == 0)
                {
                    continue;
                }

                string gameId = app.Name;
                if (app.Name.Contains('_'))
                {
                    // Mods are keyed differently, "<appId>_<modId>"
                    // Ex. 215_2287856061
                    string[] parts = app.Name.Split('_');
                    if (uint.TryParse(parts[0], out uint appId) && uint.TryParse(parts[1], out uint modId))
                    {
                        var gid = new GameID
                        {
                            AppID = appId,
                            AppType = GameID.GameType.GameMod,
                            ModID = modId
                        };
                        gameId = gid;
                    }
                    else
                    {
                        // Malformed app id?
                        continue;
                    }
                }

                var dt = DateTimeOffset.FromUnixTimeSeconds(app["LastPlayed"].AsLong()).LocalDateTime;
                if (dt.Year > 1970)
                {
                    result.Add(gameId, dt);
                }
            }

            return result;
        }
        
        internal static GameMetadata GetInstalledGameFromFile(string path)
        {
            var kv = new KeyValue();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                kv.ReadAsText(fs);
            }

            if (!kv["StateFlags"].Value.IsNullOrEmpty() && Enum.TryParse<AppStateFlags>(kv["StateFlags"].Value, out var appState))
            {
                if (!appState.HasFlag(AppStateFlags.FullyInstalled))
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            var name = string.Empty;
            if (string.IsNullOrEmpty(kv["name"].Value))
            {
                if (kv["UserConfig"]["name"].Value != null)
                {
                    name = StringExtensions.NormalizeGameName(kv["UserConfig"]["name"].Value);
                }
            }
            else
            {
                name = StringExtensions.NormalizeGameName(kv["name"].Value);
            }

            var gameId = new GameID(kv["appID"].AsUnsignedInteger());
            var installDir = Path.Combine((new FileInfo(path)).Directory.FullName, "common", kv["installDir"].Value);
            if (!Directory.Exists(installDir))
            {
                installDir = Path.Combine((new FileInfo(path)).Directory.FullName, "music", kv["installDir"].Value);
                if (!Directory.Exists(installDir))
                {
                    installDir = string.Empty;
                }
            }

            var game = new GameMetadata()
            {
                Source = new MetadataNameProperty("Steam"),
                GameId = gameId.ToString(),
                Name = name.RemoveTrademarks().Trim(),
                InstallDirectory = installDir,
                IsInstalled = true,
                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
            };

            return game;
        }

        internal static List<GameMetadata> GetInstalledGamesFromFolder(string path)
        {
            var games = new List<GameMetadata>();

            foreach (var file in Directory.GetFiles(path, @"appmanifest*"))
            {
                if (file.EndsWith("tmp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var game = GetInstalledGameFromFile(Path.Combine(path, file));
                    if (game == null)
                    {
                        continue;
                    }

                    if (game.InstallDirectory.IsNullOrEmpty() || game.InstallDirectory.Contains(@"steamapps\music"))
                    {
                        logger.Info($"Steam game {game.Name} is not properly installed or it's a soundtrack, skipping.");
                        continue;
                    }

                    games.Add(game);
                }
                catch (Exception exc)
                {
                    // Steam can generate invalid acf file according to issue #37
                    logger.Error(exc, $"Failed to get information about installed game from: {file}");
                }
            }

            return games;
        }

        internal static List<GameMetadata> GetInstalledGoldSrcModsFromFolder(string path)
        {
            var games = new List<GameMetadata>();
            var dirInfo = new DirectoryInfo(path);

            foreach (var folder in dirInfo.GetDirectories().Where(a => !firstPartyModPrefixes.Any(prefix => a.Name.StartsWith(prefix))).Select(a => a.FullName))
            {
                try
                {
                    var game = GetInstalledModFromFolder(folder, ModInfo.ModType.HL);
                    if (game != null)
                    {
                        games.Add(game);
                    }
                }
                catch (Exception exc)
                {
                    // GameMetadata.txt may not exist or may be invalid
                    logger.Error(exc, $"Failed to get information about installed GoldSrc mod from: {path}");
                }
            }

            return games;
        }

        internal static List<GameMetadata> GetInstalledSourceModsFromFolder(string path)
        {
            var games = new List<GameMetadata>();

            foreach (var folder in Directory.GetDirectories(path))
            {
                try
                {
                    var game = GetInstalledModFromFolder(folder, ModInfo.ModType.HL2);
                    if (game != null)
                    {
                        games.Add(game);
                    }
                }
                catch (Exception exc)
                {
                    // GameMetadata.txt may not exist or may be invalid
                    logger.Error(exc, $"Failed to get information about installed Source mod from: {path}");
                }
            }

            return games;
        }

        internal static GameMetadata GetInstalledModFromFolder(string path, ModInfo.ModType modType)
        {
            var modInfo = ModInfo.GetFromFolder(path, modType);
            if (modInfo == null)
            {
                return null;
            }

            var game = new GameMetadata
            {
                Source = new MetadataNameProperty("Steam"),
                GameId = modInfo.GameId.ToString(),
                Name = modInfo.Name.RemoveTrademarks().Trim(),
                InstallDirectory = path,
                IsInstalled = true,
                Developers = new HashSet<MetadataProperty> { new MetadataNameProperty(modInfo.Developer) },
                Links = modInfo.Links,
                Tags = modInfo.Categories?.Select(a => new MetadataNameProperty(a)).Cast<MetadataProperty>().ToHashSet(),
                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") }
            };

            if (!modInfo.IconPath.IsNullOrEmpty() && File.Exists(modInfo.IconPath))
            {
                game.Icon = new MetadataFile(modInfo.IconPath);
            }

            return game;
        }

        internal static Dictionary<string, GameMetadata> GetInstalledGames(bool includeMods = true)
        {
            var games = new Dictionary<string, GameMetadata>();
            if (!Steam.IsInstalled)
            {
                throw new Exception("Steam installation not found.");
            }

            foreach (var folder in GetLibraryFolders())
            {
                var libFolder = Path.Combine(folder, "steamapps");
                if (Directory.Exists(libFolder))
                {
                    GetInstalledGamesFromFolder(libFolder).ForEach(a =>
                    {
                        // Ignore redist
                        if (a.GameId == "228980")
                        {
                            return;
                        }

                        if (!games.ContainsKey(a.GameId))
                        {
                            games.Add(a.GameId, a);
                        }
                    });
                }
                else
                {
                    logger.Warn($"Steam library {libFolder} not found.");
                }
            }

            if (includeMods)
            {
                try
                {
                    // In most cases, this will be inside the folder where Half-Life is installed.
                    var modInstallPath = Steam.ModInstallPath;
                    if (!string.IsNullOrEmpty(modInstallPath) && Directory.Exists(modInstallPath))
                    {
                        GetInstalledGoldSrcModsFromFolder(Steam.ModInstallPath).ForEach(a =>
                        {
                            if (!games.ContainsKey(a.GameId))
                            {
                                games.Add(a.GameId, a);
                            }
                        });
                    }

                    // In most cases, this will be inside the library folder where Steam is installed.
                    var sourceModInstallPath = Steam.SourceModInstallPath;
                    if (!string.IsNullOrEmpty(sourceModInstallPath) && Directory.Exists(sourceModInstallPath))
                    {
                        GetInstalledSourceModsFromFolder(Steam.SourceModInstallPath).ForEach(a =>
                        {
                            if (!games.ContainsKey(a.GameId))
                            {
                                games.Add(a.GameId, a);
                            }
                        });
                    }
                }
                catch (Exception e) when (!Environment.IsDebugBuild)
                {
                    logger.Error(e, "Failed to import Steam mods.");
                }
            }

            return games;
        }

        internal static List<string> GetLibraryFolders(KeyValue foldersData)
        {
            var dbs = new List<string>();
            foreach (var child in foldersData.Children)
            {
                if (int.TryParse(child.Name, out int _))
                {
                    if (!child.Value.IsNullOrEmpty())
                    {
                        dbs.Add(child.Value);
                    }
                    else if (child.Children.HasItems())
                    {
                        var path = child.Children.FirstOrDefault(a => a.Name?.Equals("path", StringComparison.OrdinalIgnoreCase) == true);
                        if (!path.Value.IsNullOrEmpty())
                        {
                            dbs.Add(path.Value);
                        }
                    }
                }
            }

            return dbs;
        }

        internal static HashSet<string> GetLibraryFolders()
        {
            var dbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Steam.InstallationPath };
            var configPath = Path.Combine(Steam.InstallationPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(configPath))
            {
                return dbs;
            }

            try
            {
                using (var fs = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var kv = new KeyValue();
                    kv.ReadAsText(fs);
                    foreach (var dir in GetLibraryFolders(kv))
                    {
                        if (Directory.Exists(dir))
                        {
                            dbs.Add(dir);
                        }
                        else
                        {
                            logger.Warn($"Found external Steam directory, but path doesn't exists: {dir}");
                        }
                    }
                }
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                logger.Error(e, "Failed to get additional Steam library folders.");
            }

            return dbs;
        }
        
        [Flags]
        private enum AppStateFlags
        {
            Invalid = 0,
            Uninstalled = 1,
            UpdateRequired = 2,
            FullyInstalled = 4,
            Encrypted = 8,
            Locked = 16,
            FilesMissing = 32,
            AppRunning = 64,
            FilesCorrupt = 128,
            UpdateRunning = 256,
            UpdatePaused = 512,
            UpdateStarted = 1024,
            Uninstalling = 2048,
            BackupRunning = 4096,
            Reconfiguring = 65536,
            Validating = 131072,
            AddingFiles = 262144,
            Preallocating = 524288,
            Downloading = 1048576,
            Staging = 2097152,
            Committing = 4194304,
            UpdateStopping = 8388608
        }
    }
}
