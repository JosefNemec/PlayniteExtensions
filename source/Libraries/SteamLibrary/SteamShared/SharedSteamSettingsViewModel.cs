using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace SteamLibrary.SteamShared
{
    public abstract class SharedSteamSettingsViewModel<TSettings, TPlugin> : PluginSettingsViewModel<TSettings, TPlugin>
        where TSettings : SharedSteamSettings, new()
        where TPlugin : Plugin
    {
        private ObservableCollection<TagInfo> okayTags;
        private ObservableCollection<TagInfo> blacklistedTags;
        private string fixedTagCountString;
        internal readonly string ApiKeysPath;

        protected SharedSteamSettingsViewModel(TPlugin plugin, IPlayniteAPI playniteApi) : base(plugin, playniteApi)
        {
            ApiKeysPath = Path.Combine(plugin.GetPluginUserDataPath(), "keys.dat");
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                Settings = savedSettings;
                OnLoadSettings();
            }
            else
            {
                Settings = new TSettings() { LanguageKey = GetSteamLanguageForCurrentPlayniteLanguage() };
                OnInitSettings();
            }

            InitializeTagNames();
            Settings.PropertyChanged += (sender, ev) =>
            {
                if (ev.PropertyName == nameof(Settings.LanguageKey)
                    || ev.PropertyName == nameof(Settings.UseTagPrefix)
                    || ev.PropertyName == nameof(Settings.TagPrefix)
                    || ev.PropertyName == nameof(Settings.SetTagCategoryAsPrefix))
                {
                    InitializeTagNames();
                }
            };

            FixedTagCountString = Settings.FixedTagCount.ToString();
        }

        private string GetSteamLanguageForCurrentPlayniteLanguage()
        {
            switch (PlayniteApi.ApplicationSettings.Language)
            {
                case "cs_CZ": return "czech";
                case "da_DK": return "danish";
                case "de_DE": return "german";
                case "el_GR": return "greek";
                case "es_ES": return "spanish";
                case "fi_FI": return "finnish";
                case "fr_FR": return "french";
                case "hu_HU": return "hungarian";
                case "it_IT": return "italian";
                case "ja_JP": return "japanese";
                case "ko_KR": return "korean";
                case "nl_NL": return "dutch";
                case "no_NO": return "norwegian";
                case "pl_PL": return "polish";
                case "pt_BR": return "brazilian";
                case "pt_PT": return "portuguese";
                case "ro_RO": return "romanian";
                case "ru_RU": return "russian";
                case "sv_SE": return "swedish";
                case "tr_TR": return "turkish";
                case "uk_UA": return "ukrainian";
                case "vi_VN": return "vietnamese";
                case "zh_CN":
                case "zh_TW": return "schinese";
                case "en_US":
                default: return "english";
                //no cultures for latam, thai, bulgarian, tchinese
            }
        }

        /// <summary>
        /// Called after settings are successfully loaded
        /// </summary>
        protected virtual void OnLoadSettings()
        {
        }

        /// <summary>
        /// Called after settings are newly created (on first run, or after settings have been deleted)
        /// </summary>
        protected virtual void OnInitSettings()
        {
        }

        private void InitializeTagNames()
        {
            var tagNamer = new SteamTagNamer(Plugin, Settings, new Playnite.Common.Web.Downloader());
            var tags = tagNamer.GetTagNames()
                .Select(t => new TagInfo(t.Key, tagNamer.GetFinalTagName(t.Value, t.Key)))
                .OrderBy(t => t.Name).ToList();
            OkayTags = tags.Where(t => !Settings.BlacklistedTags.Contains(t.Id)).ToObservable();
            BlacklistedTags = tags.Where(t => Settings.BlacklistedTags.Contains(t.Id)).ToObservable();
        }

        public Dictionary<string, string> Languages { get; } = new Dictionary<string, string>
        {
            {"schinese","简体中文 (Simplified Chinese)"},
            {"tchinese","繁體中文 (Traditional Chinese)"},
            {"japanese","日本語 (Japanese)"},
            {"koreana","한국어 (Korean)"},
            {"thai","ไทย (Thai)"},
            {"bulgarian","Български (Bulgarian)"},
            {"czech","Čeština (Czech)"},
            {"danish","Dansk (Danish)"},
            {"german","Deutsch (German)"},
            {"english","English"},
            {"spanish","Español - España (Spanish - Spain)"},
            {"latam","Español - Latinoamérica (Spanish - Latin America)"},
            {"greek","Ελληνικά (Greek)"},
            {"french","Français (French)"},
            {"indonesian","Bahasa Indonesia (Indonesian)"},
            {"italian","Italiano (Italian)"},
            {"hungarian","Magyar (Hungarian)"},
            {"dutch","Nederlands (Dutch)"},
            {"norwegian","Norsk (Norwegian)"},
            {"polish","Polski (Polish)"},
            {"portuguese","Português (Portuguese)"},
            {"brazilian","Português - Brasil (Portuguese - Brazil)"},
            {"romanian","Română (Romanian)"},
            {"russian","Русский (Russian)"},
            {"finnish","Suomi (Finnish)"},
            {"swedish","Svenska (Swedish)"},
            {"turkish","Türkçe (Turkish)"},
            {"vietnamese","Tiếng Việt (Vietnamese)"},
            {"ukrainian","Українська (Ukrainian)"},
        };

        public class TagInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }

            public TagInfo(int id, string name)
            {
                Id = id;
                Name = name;
            }
        }

        public ObservableCollection<TagInfo> OkayTags { get => okayTags; set => SetValue(ref okayTags, value); }
        public ObservableCollection<TagInfo> BlacklistedTags { get => blacklistedTags; set => SetValue(ref blacklistedTags, value); }
        public string FixedTagCountString { get => fixedTagCountString; set => SetValue(ref fixedTagCountString, value); }

        public RelayCommand<IList<object>> WhitelistCommand
        {
            get => new RelayCommand<IList<object>>((selectedItems) =>
            {
                var selectedKeyValuePairs = selectedItems.Cast<TagInfo>().ToList();
                foreach (var sel in selectedKeyValuePairs)
                {
                    BlacklistedTags.Remove(sel);
                    OkayTags.Add(sel);
                    Settings.BlacklistedTags.Remove(sel.Id);
                }
            }, (a) => a?.Count > 0);
        }

        public RelayCommand<IList<object>> BlacklistCommand
        {
            get => new RelayCommand<IList<object>>((selectedItems) =>
            {
                var selectedKeyValuePairs = selectedItems.Cast<TagInfo>().ToList();
                foreach (var sel in selectedKeyValuePairs)
                {
                    OkayTags.Remove(sel);
                    BlacklistedTags.Add(sel);
                    Settings.BlacklistedTags.Add(sel.Id);
                }
            }, (a) => a?.Count > 0);
        }

        public override bool VerifySettings(out List<string> errors)
        {
            if (Settings.LimitTagsToFixedAmount)
            {
                if (!int.TryParse(FixedTagCountString, out int fixedTagCount) || fixedTagCount < 0)
                {
                    errors = new List<string> { PlayniteApi.Resources.GetString(LOC.SteamValidationFixedTagCount) };
                    return false;
                }
            }

            return base.VerifySettings(out errors);
        }

        public override void EndEdit()
        {
            if (int.TryParse(FixedTagCountString, out int fixedTagCount) && fixedTagCount >= 0)
            {
                Settings.FixedTagCount = fixedTagCount;
            }

            base.EndEdit();
        }
    }
}
