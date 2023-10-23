using Playnite.SDK;
using Steam;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamLibrary.SteamShared
{
    public abstract class SharedSteamSettings : ObservableObject
    {
        private string languageKey = "english";
        private bool limitTagsToFixedAmount = false;
        private int fixedTagCount = 5;
        private bool useTagPrefix = false;
        private bool setTagCategoryAsPrefix = false;
        private string tagPrefix = string.Empty;

        public bool LimitTagsToFixedAmount { get { return limitTagsToFixedAmount; } set { SetValue(ref limitTagsToFixedAmount, value); } }

        public int FixedTagCount { get { return fixedTagCount; } set { SetValue(ref fixedTagCount, value); } }

        public bool UseTagPrefix { get { return useTagPrefix; } set { SetValue(ref useTagPrefix, value); } }

        public bool SetTagCategoryAsPrefix { get { return setTagCategoryAsPrefix; } set { SetValue(ref setTagCategoryAsPrefix, value); } }

        public string TagPrefix { get { return tagPrefix; } set { SetValue(ref tagPrefix, value); } }

        public string LanguageKey { get => languageKey; set => SetValue(ref languageKey, value); }

        public bool DownloadVerticalCovers { get; set; } = true;

        public BackgroundSource BackgroundSource { get; set; } = BackgroundSource.Image;

        public ObservableCollection<int> BlacklistedTags { get; set; } = new ObservableCollection<int>();
    }
}
