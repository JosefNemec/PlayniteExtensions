using System;
using System.Collections.Generic;

namespace GogLibrary.Models
{
    public class OwnedGameDetailsResponse
    {
        /// <example>The Witcher 3: Wild Hunt - Complete Edition</example>>
        public string Title { get; set; }

        public List<Extra> Extras { get; set; }
    }

    public class Extra
    {
        /// <example>/downloads/the_witcher_3_wild_hunt_game_of_the_year_edition_game/70003</example>
        public string ManualUrl { get; set; }

        /// <example>soundtrack (MP3)</example>
        public string Name { get; set; }

        /// <example>audio</example>
        public string Type { get; set; }

        /// <summary>
        /// number of items in downloaded archive, displayed in UI like "paper toys (6)". not shown when equals 1 or 2.
        /// </summary>
        /// <example>1</example>
        public long Info { get; set; }

        /// <example>206 MB</example>
        /// <example>4096 MB</example>
        public string Size { get; set; }
    }
}
