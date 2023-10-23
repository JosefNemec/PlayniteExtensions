using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamLibrary.SteamShared
{
    public class TagIdCategory
    {
        [SerializationPropertyName("Id")]
        public int Id { get; set; }

        [SerializationPropertyName("Category")]
        public TagCategory Category { get; set; }
    }

    public enum TagCategory
    {
        Assessments,
        Features,
        FundingEtc,
        Genres,
        HardwareInput,
        OtherTags,
        Players,
        RatingsEtc,
        Software,
        SubGenres,
        ThemesMoods,
        TopLevelGenres,
        VisualsViewpoint
    };
}
