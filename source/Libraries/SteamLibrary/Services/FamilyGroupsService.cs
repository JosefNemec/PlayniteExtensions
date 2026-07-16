using SteamLibrary.Models;
using SteamLibrary.Services.Base;
using System.Collections.Generic;
using System.Linq;

namespace SteamLibrary.Services
{
    public class FamilyGroupsService : SteamApiServiceBase
    {
        /// <summary>
        /// IFamilyGroupsService/GetSharedLibraryApps
        /// </summary>
        public IEnumerable<ISteamApp> GetSharedGames(SteamLibrarySettings settings, SteamUserToken userToken, HashSet<string> userIds)
        {
            var familyGroup = GetFamilyGroupForUser(userToken);
            if (familyGroup.is_not_member_of_any_group)
                return Enumerable.Empty<ISteamApp>();

            var sharedLibrary = GetSharedLibraryApps(settings, userToken, familyGroup.family_groupid);

            if (sharedLibrary.apps == null) //user is most likely in a family group without any other members
                return Enumerable.Empty<ISteamApp>();

            userIds.UnionWith(sharedLibrary.apps.SelectMany(a => a.owner_steamids));
            var currentOwner = sharedLibrary.owner_steamid;
            foreach (var app in sharedLibrary.apps)
            {
                // steam lies about ownership: currentOwner in the list, but game has exclude_reason - meaning it's free, coming from family
                app.IsOwned = app.owner_steamids.Contains(currentOwner) && app.exclude_reason == 0;
            }

            return sharedLibrary.apps.Where(x => x.IsImportable);
        }



        /// <summary>
        /// IFamilyGroupsService/GetFamilyGroupForUser
        /// </summary>
        private GetFamilyGroupForUserResponse GetFamilyGroupForUser(SteamUserToken userToken)
        {
            return Get<GetFamilyGroupForUserResponse>("https://api.steampowered.com/IFamilyGroupsService/GetFamilyGroupForUser/v1/",
                                                      new Dictionary<string, string>
                                                      {
                                                          { "access_token", userToken.AccessToken },
                                                      });
        }

        /// <summary>
        /// IFamilyGroupsService/GetSharedLibraryApps
        /// </summary>
        private GetSharedLibraryAppsResponse GetSharedLibraryApps(SteamLibrarySettings settings, SteamUserToken userToken, string familyGroupId)
        {
            return Get<GetSharedLibraryAppsResponse>("https://api.steampowered.com/IFamilyGroupsService/GetSharedLibraryApps/v1/",
                                                     new Dictionary<string, string>
                                                     {
                                                         { "access_token", userToken.AccessToken },
                                                         { "family_groupid", familyGroupId },
                                                         { "language", settings.LanguageKey },
                                                         { "include_own", "true" },
                                                         { "include_excluded", "true" },
                                                         { "include_free", "true" },
                                                         { "include_non_games", "true" },
                                                     });
        }


    }
}
