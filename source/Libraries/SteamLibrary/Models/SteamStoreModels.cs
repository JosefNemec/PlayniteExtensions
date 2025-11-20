namespace SteamLibrary.Models
{
    public class SteamUserDataRoot
    {
        public uint[] rgOwnedApps { get; set; }
    }

    public struct SteamUserToken
    {
        public ulong UserId;
        public string AccessToken;
    }
}
