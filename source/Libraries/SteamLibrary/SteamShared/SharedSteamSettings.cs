using Playnite.SDK.Models;
using Steam;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SteamLibrary.SteamShared
{
    public abstract class SharedSteamSettings : ObservableObject
    {
        private string languageKey = "english";
        private bool limitTagsToFixedAmount = false;
        private int fixedTagCount = 5;
        private bool useTagPrefix = false;
        private string tagPrefix = string.Empty;

        public bool LimitTagsToFixedAmount { get { return limitTagsToFixedAmount; } set { SetValue(ref limitTagsToFixedAmount, value); } }

        public int FixedTagCount { get { return fixedTagCount; } set { SetValue(ref fixedTagCount, value); } }

        public bool UseTagPrefix { get { return useTagPrefix; } set { SetValue(ref useTagPrefix, value); } }

        public string TagPrefix { get { return tagPrefix; } set { SetValue(ref tagPrefix, value); } }

        public string LanguageKey { get => languageKey; set => SetValue(ref languageKey, value); }

        public bool DownloadVerticalCovers { get; set; } = true;

        public BackgroundSource BackgroundSource { get; set; } = BackgroundSource.Image;

        public ObservableCollection<int> BlacklistedTags { get; set; } = new ObservableCollection<int>();

        public GameField SteamDeckCompatibilityField { get; set; } = GameField.None;

        public class CheckedCompatibility
        {
            public SteamDeckCompatibility Compatibility { get; set; }
            public bool IsChecked { get; set; }
        }

        public List<CheckedCompatibility> SteamDeckCompatibilitySettings { get; set; } = new List<CheckedCompatibility>();
    }

    public enum SteamDeckCompatibility
    {
        Unknown = 0,
        Unsupported = 1,
        Playable = 2,
        Verified = 3,
    }
}
