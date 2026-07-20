namespace SteamLibrary.Models
{
    public class SteamUserDataRoot
    {
        public uint[] rgOwnedApps { get; set; }
    }

    public struct SteamUserToken
    {
        public SteamUserToken(ulong userId, string accessToken)
        {
            UserId = userId;
            AccessToken = accessToken;
        }

        public readonly ulong UserId;
        public readonly string AccessToken;
    }

    public class StoreUserConfig
    {
        public string webapi_token { get; set; }
    }

    public class UserInfo
    {
        public bool logged_in { get; set; }
        public string steamid { get; set; }
        public string account_name { get; set; }
    }
}
