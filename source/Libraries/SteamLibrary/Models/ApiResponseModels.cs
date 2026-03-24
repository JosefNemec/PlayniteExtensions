using System;
using System.Collections.Generic;
using Playnite.SDK.Models;
using SteamKit2;
using SteamLibrary.Services;
using SteamLibrary.Services.Base;

namespace SteamLibrary.Models
{
    public class SteamApiResponseRoot<TResponse>
    {
        public TResponse response { get; set; }
    }

    public class GetFamilyGroupForUserResponse
    {
        public string family_groupid { get; set; }
        public bool is_not_member_of_any_group { get; set; }
    }

    public class GetSharedLibraryAppsResponse
    {
        public FamilySharedApp[] apps { get; set; }
        public string owner_steamid { get; set; }
    }

    public class FamilySharedApp : ISteamApp
    {
        public int appid { get; set; }
        public string[] owner_steamids { get; set; }
        public string name { get; set; }
        public string capsule_filename { get; set; }
        public string img_icon_hash { get; set; }
        public uint exclude_reason { get; set; }
        public uint rt_time_acquired { get; set; }
        public uint rt_last_played { get; set; }
        /// <summary>
        /// Playtime for the current user in minutes
        /// </summary>
        public uint rt_playtime { get; set; }
        public uint app_type { get; set; }
        public uint[] content_descriptors { get; set; }
        public string sort_as { get; set; }

        public string NiceAppType =>
            app_type switch
            {
                1 => "game",
                2 => "application",
                4 => "tool",
                8 => "demo",
                8192 => "soundtrack",
                _ => $"{app_type}"
            };

        public string NiceExcludeReason =>
            exclude_reason switch
            {
                0 => "none",
                1 => "ineligible",
                3 => "free",
                6 => "type_not_shared", // guessed by looking at data
                _ => $"{exclude_reason}"
            };

        // we probably don't want free apps coming from family members (they do exist)
        public bool IsImportable => (NiceExcludeReason == "none" || NiceExcludeReason == "free")
                                    && (NiceAppType == "game" || NiceAppType == "demo" || NiceAppType == "tool");

        public GameMetadata ToGame()
        {
            return new GameMetadata
            {
                GameId = Id,
                Name = name.RemoveTrademarks().Trim(),
                LastActivity = SteamApiServiceBase.GetLastPlayedDateTime(rt_last_played),
                Playtime = rt_playtime * 60,
                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                Source = SourceNames.GetSource(IsOwned, BackendAppInfo?.Type),
            };
        }

        public GameID Id => (uint)appid;
        public bool IsOwned { get; set; }
        public BackendAppInfo BackendAppInfo { get; set; }
    }

    public class GetClientAppListResponse
    {
        public string bytes_available { get; set; }
        public SteamClientApp[] apps { get; set; }
    }

    public class SteamClientApp : ISteamApp
    {
        public ulong appid { get; set; }
        public string app { get; set; }
        public string app_type { get; set; }
        public bool available_on_platform { get; set; }
        public string bytes_required { get; set; }
        public bool running { get; set; }
        public bool installed { get; set; }

        public GameMetadata ToGame()
        {
            return new GameMetadata
            {
                GameId = Id,
                Name = app.RemoveTrademarks().Trim(),
                InstallSize = GetInstallSize(),
                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                Source = SourceNames.GetSource(IsOwned, BackendAppInfo?.Type),
            };
        }

        public GameID Id => appid;
        public bool IsOwned { get; set; } = true;
        public BackendAppInfo BackendAppInfo { get; set; }


        private ulong? GetInstallSize()
        {
            if (ulong.TryParse(bytes_required, out ulong size))
                return size;

            return null;
        }
    }

    public class GetOwnedGamesResponse
    {
        public int game_count { get; set; }
        public List<OwnedGame> games { get; set; }
    }

    public class OwnedGame : ISteamApp
    {
        public int appid { get; set; }
        public string name { get; set; }
        public uint playtime_forever { get; set; }
        public uint rtime_last_played { get; set; }

        public bool IncludePlaytime { get; set; }

        public GameMetadata ToGame()
        {
            var output = new GameMetadata
            {
                GameId = Id,
                Name = name.RemoveTrademarks().Trim(),
                Platforms = new HashSet<MetadataProperty> {new MetadataSpecProperty("pc_windows")},
                Source = SourceNames.GetSource(IsOwned, BackendAppInfo?.Type),
            };

            if (IncludePlaytime)
            {
                output.Playtime = playtime_forever * 60;
                output.LastActivity = SteamApiServiceBase.GetLastPlayedDateTime(rtime_last_played);
            }

            return output;
        }

        public GameID Id => (uint) appid;
        public bool IsOwned => true;
        public BackendAppInfo BackendAppInfo { get; set; }

    }

    public interface ISteamApp
    {
        GameID Id { get; }
        GameMetadata ToGame();
        public BackendAppInfo BackendAppInfo { get; set; }
        bool IsOwned { get; }
    }

    public class LocalSteamApp : ISteamApp
    {
        public GameID Id { get; set; }
        public string Name { get; set; }
        public string InstallDir { get; set; }

        public GameMetadata ToGame()
        {
            return new GameMetadata()
            {
                GameId = Id,
                Name = Name,
                InstallDirectory = InstallDir,
                IsInstalled = true,
                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                Source = SourceNames.GetSource(IsOwned, BackendAppInfo?.Type),
            };
        }

        public bool IsOwned => true;
        public BackendAppInfo BackendAppInfo { get; set; }

    }

    public class ExtraIdSteamApp : ISteamApp
    {
        public GameID Id { get; set; }
        public string Name { get; set; }

        public GameMetadata ToGame()
        {
            return new GameMetadata
            {
                GameId = Id,
                Name = Name,
                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                Source = SourceNames.GetSource(IsOwned, BackendAppInfo?.Type),
            };
        }

        public bool IsOwned => true;
        public BackendAppInfo BackendAppInfo { get; set; }

    }

    public class BackendOwnSteamDbApp : ISteamApp
    {
        public BackendOwnSteamDbApp(BackendAppInfo item)
        {
            BackendAppInfo = item;
        }

        public GameMetadata ToGame() =>
            new GameMetadata
            {
                Name = BackendAppInfo.Name.RemoveTrademarks(),
                GameId = Id,
                Platforms = new HashSet<MetadataProperty> {new MetadataSpecProperty("pc_windows")},
                Source = SourceNames.GetSource(IsOwned, BackendAppInfo?.Type),
            };

        public GameID Id => BackendAppInfo.AppId;
        public bool IsOwned => true;
        public BackendAppInfo BackendAppInfo { get; set; }
    }
}
