using Playnite.SDK.Models;
using SteamLibrary.Models;
using SteamLibrary.Services.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SteamLibrary.Services
{
    public class FamilyGroupsService : SteamApiServiceBase
    {
        public IEnumerable<GameMetadata> GetSharedGames(SteamLibrarySettings settings, SteamUserToken userToken, out HashSet<string> userIds)
        {
            userIds = new HashSet<string>();
            var familyGroup = GetFamilyGroupForUser(userToken);
            if (familyGroup.is_not_member_of_any_group)
                return Enumerable.Empty<GameMetadata>();

            var sharedLibrary = GetSharedLibraryApps(settings, userToken, familyGroup.family_groupid);
            userIds = sharedLibrary.apps.SelectMany(a => a.owner_steamids).ToHashSet();
            
            return sharedLibrary.apps.Select(ToGame);
        }

        private GetFamilyGroupForUserResponse GetFamilyGroupForUser(SteamUserToken userToken)
        {
            return Get<GetFamilyGroupForUserResponse>("https://api.steampowered.com/IFamilyGroupsService/GetFamilyGroupForUser/v1/",
                                                      new Dictionary<string, string>
                                                      {
                                                          { "access_token", userToken.AccessToken },
                                                          { "steamid", userToken.UserId.ToString() },
                                                      });
        }

        private GetSharedLibraryAppsResponse GetSharedLibraryApps(SteamLibrarySettings settings, SteamUserToken userToken, string familyGroupId)
        {
            return Get<GetSharedLibraryAppsResponse>("https://api.steampowered.com/IFamilyGroupsService/GetSharedLibraryApps/v1/",
                                                     new Dictionary<string, string>
                                                     {
                                                         { "access_token", userToken.AccessToken },
                                                         { "steamid", userToken.UserId.ToString() },
                                                         { "family_groupid", familyGroupId },
                                                         { "language", settings.LanguageKey },
                                                     });
        }

        private static GameMetadata ToGame(FamilySharedApp sharedApp)
        {
            return new GameMetadata
            {
                GameId = sharedApp.appid.ToString(),
                Name = sharedApp.name.RemoveTrademarks().Trim(),
                LastActivity = GetDateTimeFromUnixEpoch(sharedApp.rt_last_played),
                Playtime = sharedApp.rt_playtime * 60,
                Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") },
                Source = new MetadataNameProperty(SourceNames.FamilySharing)
            };
        }
    }
}
