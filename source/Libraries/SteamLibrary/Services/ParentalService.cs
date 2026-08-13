using SteamLibrary.Models;
using SteamLibrary.Services.Base;
using System.Collections.Generic;

namespace SteamLibrary.Services
{
    public class ParentalService: SteamApiServiceBase
    {
        public ParentalAppAllower GetParentalAppAllower(SteamUserToken userToken)
        {
            var response = GetParentalSettings(userToken);
            return new(response.settings);
        }

        private ParentalSettingsResponse GetParentalSettings(SteamUserToken userToken)
        {
            return Get<ParentalSettingsResponse>("https://api.steampowered.com/IParentalService/GetParentalSettings/v1/",
                                                 new Dictionary<string, string>
                                                 {
                                                     { "access_token", userToken.AccessToken },
                                                     { "steamid", userToken.UserId.ToString() },
                                                 });
        }
    }

    public class ParentalAppAllower
    {
        private readonly bool defaultAllowState;
        private readonly Dictionary<uint, bool> customAllowStates = [];

        public ParentalAppAllower(ParentalSettings parentalSettings)
        {
            // applist_base_id 1 is the don't-allow-by-default applist_base, where you have to whitelist games in applist_custom
            defaultAllowState = !parentalSettings.is_enabled || parentalSettings.applist_base_id != 1;

            foreach (var parentalApp in parentalSettings.applist_base)
                customAllowStates[parentalApp.appid] = parentalApp.is_allowed;

            foreach (var parentalApp in parentalSettings.applist_custom)
                customAllowStates[parentalApp.appid] = parentalApp.is_allowed;
        }

        public bool AppIsAllowed(uint appId)
        {
            return customAllowStates.TryGetValue(appId, out bool allowed) ? allowed : defaultAllowState;
        }

        public bool AppIsAllowed(string appIdString)
        {
            return uint.TryParse(appIdString, out uint appId)
                ? AppIsAllowed(appId)
                : defaultAllowState;
        }
    }
}
