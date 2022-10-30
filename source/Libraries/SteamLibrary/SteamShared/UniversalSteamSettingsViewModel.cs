using Playnite.SDK;
using Playnite.SDK.Plugins;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SteamLibrary.SteamShared
{
    public abstract class UniversalSteamSettingsViewModel<TSettings, TPlugin> : PluginSettingsViewModel<TSettings, TPlugin>
        where TSettings : UniversalSteamSettings, new()
        where TPlugin : Plugin
    {
        public ObservableCollection<TagInfo> okayTags;
        public ObservableCollection<TagInfo> blacklistedTags;

        protected UniversalSteamSettingsViewModel(TPlugin plugin, IPlayniteAPI playniteApi) : base(plugin, playniteApi)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                Settings = savedSettings;
                OnLoadSettings();
            }
            else
            {
                Settings = new TSettings();
                OnInitSettings();
            }

            InitializeTagNames();
            Settings.PropertyChanged += (sender, ev) =>
            {
                if (ev.PropertyName == nameof(Settings.LanguageKey)
                    || ev.PropertyName == nameof(Settings.UseTagPrefix)
                    || ev.PropertyName == nameof(Settings.TagPrefix))
                {
                    InitializeTagNames();
                }
            };
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
            var tagNamer = new SteamTagNamer(Plugin.GetPluginUserDataPath(), Settings, new Playnite.Common.Web.Downloader());
            var tags = tagNamer.GetTagNames()
                .Select(t => new TagInfo(t.Key, tagNamer.GetFinalTagName(t.Value)))
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
    }
}
