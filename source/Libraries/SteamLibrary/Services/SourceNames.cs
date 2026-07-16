using Playnite.SDK.Models;

namespace SteamLibrary.Services
{
    public static class SourceNames
    {
        public const string Steam = "Steam";
        public const string Extras = "Steam Extras";
        public const string Video = "Steam Video";
        public const string FamilySharing = "Steam Family Sharing";
        public const string FamilySharingExtras = "Steam Family Sharing Extras";
        public static readonly string[] AllKnown = {Steam, FamilySharing, Extras, FamilySharingExtras, Video};

        // TODO i don't really see a good way to cram type (game/app/media/etc) and ownership (own/family) into one "source" property
        // it was ok for gog and epic but this is starting to look insane
        // also videos need to launch browser instead of installing, i decided not to break steam ids and encode this info here
        // all this infinitely debatable, for example should apps not be labeled "extras"?..
        // maybe make an option to create tags with type, ownership, whatever else?
        public static MetadataNameProperty GetSource(bool isOwned, string type)
        {
            var source = type switch
            {
                "video" => Video,
                "game" when isOwned => Steam,
                "game" => FamilySharing,
                _ when isOwned => Extras,
                _ => FamilySharingExtras
            };
            return new MetadataNameProperty(source);
        }
    }
}
