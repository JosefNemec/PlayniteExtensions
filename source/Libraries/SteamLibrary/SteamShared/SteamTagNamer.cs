using Playnite.Common;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SteamLibrary.SteamShared
{
    public class SteamTagNamer
    {
        private static string extensionFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private const string getTagListApiTemplate = "https://api.steampowered.com/IStoreService/GetTagList/v1?language={0}&have_version_hash={1}";
        private const string steamSharedFolder = "SteamShared";
        private readonly Plugin plugin;
        private readonly SharedSteamSettings settings;
        private readonly IDownloader downloader;
        private readonly Dictionary<int, TagCategory> tagCategoriesDictionary;
        private readonly Dictionary<TagCategory, string> tagCategoryStringMapping;
        private ILogger logger = LogManager.GetLogger();

        public SteamTagNamer(Plugin plugin, SharedSteamSettings settings, IDownloader downloader)
        {
            this.plugin = plugin;
            this.settings = settings;
            this.downloader = downloader;

            var tagCategoriesFilePath = Path.Combine(extensionFolder, steamSharedFolder, "TagCategories", "tagsCategories.json");
            var tagsIdsWithCategories = Serialization.FromJsonFile<List<TagIdCategory>>(tagCategoriesFilePath);
            tagCategoriesDictionary = new Dictionary<int, TagCategory>();
            foreach (var tag in tagsIdsWithCategories)
            {
                if (tagCategoriesDictionary.ContainsKey(tag.Id))
                {
                    // Some tags are in multiple categories, for example "Experimental"
                    // which is a Top-Level Genre and Genre.
                    continue;
                }

                tagCategoriesDictionary[tag.Id] = tag.Category;
            }

            tagCategoryStringMapping = new Dictionary<TagCategory, string>
            {
                { TagCategory.Assessments, ResourceProvider.GetString("LOCSteamTagCategoryAssessments") },
                { TagCategory.Features, ResourceProvider.GetString("LOCSteamTagCategoryFeatures") },
                { TagCategory.FundingEtc, ResourceProvider.GetString("LOCSteamTagCategoryFundingEtc") },
                { TagCategory.Genres, ResourceProvider.GetString("LOCSteamTagCategoryGenres") },
                { TagCategory.HardwareInput, ResourceProvider.GetString("LOCSteamTagCategoryHardwareInput") },
                { TagCategory.OtherTags, ResourceProvider.GetString("LOCSteamTagCategoryOtherTags") },
                { TagCategory.Players, ResourceProvider.GetString("LOCSteamTagCategoryPlayers") },
                { TagCategory.RatingsEtc, ResourceProvider.GetString("LOCSteamTagCategoryRatingsEtc") },
                { TagCategory.Software, ResourceProvider.GetString("LOCSteamTagCategorySoftware") },
                { TagCategory.SubGenres, ResourceProvider.GetString("LOCSteamTagCategorySubGenres") },
                { TagCategory.ThemesMoods, ResourceProvider.GetString("LOCSteamTagCategoryThemesMoods") },
                { TagCategory.TopLevelGenres, ResourceProvider.GetString("LOCSteamTagCategoryTopLevelGenres") },
                { TagCategory.VisualsViewpoint, ResourceProvider.GetString("LOCSteamTagCategoryVisualsViewpoint") }
            };
        }

        private string GetTagNameFilePath()
        {
            var path = Path.Combine(plugin.GetPluginUserDataPath(), $"tagnames-{settings?.LanguageKey}.json");
            return Paths.FixPathLength(path);
        }

        private string GetPackedTagNameFilePath()
        {
            var path = Path.Combine(extensionFolder, steamSharedFolder, "TagLocalization", $"{settings?.LanguageKey}.json");
            return Paths.FixPathLength(path);
        }

        public Dictionary<int, string> GetTagNames()
        {
            var fileName = GetTagNameFilePath();
            if (!FileSystem.FileExists(fileName))
            {
                logger.Trace($"No new tag names found for '{settings?.LanguageKey}'");
                fileName = GetPackedTagNameFilePath();
                if (!FileSystem.FileExists(fileName))
                {
                    logger.Warn($"No tag names found for language '{settings?.LanguageKey}'");
                    return new Dictionary<int, string>();
                }
            }

            var content = GetFileContents(fileName);
            return content.response.tags.ToDictionary(x => x.tagid, x => x.name);
        }

        private SteamTagFile GetFileContents()
        {
            return GetFileContents(GetTagNameFilePath()) ?? GetFileContents(GetPackedTagNameFilePath());
        }

        private SteamTagFile GetFileContents(string path)
        {
            if (!FileSystem.FileExists(path))
            {
                return null;
            }

            logger.Debug("Opening " + path);
            try
            {
                var content = File.ReadAllText(path, Encoding.UTF8);
                return Serialization.FromJson<SteamTagFile>(content);
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to read " + path);
                throw;
            }
        }

        public Dictionary<int, string> UpdateAndGetTagNames()
        {
            var existingFile = GetFileContents();
            var url = string.Format(getTagListApiTemplate, settings?.LanguageKey, existingFile?.response?.version_hash);
            logger.Debug("Downloading " + url);
            var content = downloader.DownloadString(url);
            if (!content.IsNullOrWhiteSpace())
            {
                File.WriteAllText(GetTagNameFilePath(), content, Encoding.UTF8);
            }

            var tagFile = GetFileContents();
            return tagFile.response.tags.ToDictionary(x => x.tagid, x => x.name);
        }

        public string GetFinalTagName(string tagName, int tagId)
        {
            tagName = HttpUtility.HtmlDecode(tagName).Trim();
            if (settings.SetTagCategoryAsPrefix)
            {
                if (!tagCategoriesDictionary.TryGetValue(tagId, out var tagCategory))
                {
                    tagCategory = TagCategory.OtherTags;
                }

                var categoryName = tagCategoryStringMapping[tagCategory];
                tagName = $"{categoryName}: {tagName}";
            }
            else if (settings.UseTagPrefix && !settings.TagPrefix.IsNullOrEmpty())
            {
                tagName = $"{settings.TagPrefix}{tagName}";
            }

            return tagName;
        }
    }

    public class SteamTagFile
    {
        public SteamTagResponse response { get; set; } = new SteamTagResponse();
    }

    public class SteamTagResponse
    {
        public string version_hash { get; set; }
        public List<SteamTag> tags { get; set; } = new List<SteamTag>();
    }

    public class SteamTag
    {
        public int tagid { get; set; }
        public string name { get; set; }
    }
}
