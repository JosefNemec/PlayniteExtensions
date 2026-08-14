using System;
using System.Linq;

namespace SteamLibrary.Services
{
    public static class SourceNames
    {
        public const string Steam = "Steam";
        public const string FamilySharing = "Steam Family Sharing";
        private static readonly string[] updatableSources = [Steam, FamilySharing];

        /// <summary>
        /// This source is only assigned to games already in the Playnite library.
        /// By not being in UpdatableSources, games that we don't actually know the original source of won't get their source overridden.
        /// Since this source isn't assigned to new games, it won't normally be seen by users.
        /// </summary>
        public const string PlaytimeOnly = "Steam playtime only (missing from normal import)";

        public static bool IsUpdatableSource(string source)
        {
            return updatableSources.Contains(source, StringComparer.InvariantCultureIgnoreCase);
        }
    }
}