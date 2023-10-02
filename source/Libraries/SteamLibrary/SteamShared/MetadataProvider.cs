using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteExtensions.Common;
using Steam.Models;
using SteamKit2;
using SteamLibrary.SteamShared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Steam
{
    public enum BackgroundSource
    {
        Image,
        StoreScreenshot,
        StoreBackground,
        Banner
    }

    public class MetadataProvider
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly SteamApiClient apiClient;
        private readonly WebApiClient webApiClient;
        private readonly SteamTagNamer tagNamer;
        private readonly SharedSteamSettings settings;
        private static readonly string[] backgroundUrls = new string[]
        {
            @"https://steamcdn-a.akamaihd.net/steam/apps/{0}/page.bg.jpg",
            @"https://steamcdn-a.akamaihd.net/steam/apps/{0}/page_bg_generated.jpg"
        };

        private static readonly string[] childGameTypes = new[] { "Demo", "Beta", "Tool", "Video" };

        public MetadataProvider(SteamApiClient apiClient, WebApiClient webApiClient, SteamTagNamer tagNamer, SharedSteamSettings settings)
        {
            this.apiClient = apiClient;
            this.webApiClient = webApiClient;
            this.tagNamer = tagNamer;
            this.settings = settings;
        }

        public static string GetWorkshopUrl(uint appId)
        {
            return $"https://steamcommunity.com/app/{appId}/workshop/";
        }

        public static string GetAchievementsUrl(uint appId)
        {
            return $"https://steamcommunity.com/stats/{appId}/achievements";
        }

        internal KeyValue GetAppInfo(uint appId)
        {
            try
            {
                return apiClient.GetProductInfo(appId).GetAwaiter().GetResult();
            }
            catch (Exception e) when (!Debugger.IsAttached)
            {
                logger.Error(e, $"Failed to get Steam appinfo {appId}");
                return null;
            }
        }

        private T SendDelayedStoreRequest<T>(Func<T> request, uint appId) where T : class
        {
            // Steam may return 429 if we send too many requests
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    return request();
                }
                catch (WebException e)
                {
                    if (i + 1 == 10)
                    {
                        logger.Error($"Reached download timeout for Steam store game {appId}");
                        return null;
                    }

                    if (e.Message.Contains("429"))
                    {
                        Thread.Sleep(2500);
                        continue;
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return null;
        }

        private int CalculateUserScore(AppReviewsResult.QuerySummary reviews)
        {
            var totalVotes = reviews.total_positive + reviews.total_negative;
            double average = (double)reviews.total_positive / (double)totalVotes;
            double score = average - (average - 0.5) * Math.Pow(2, -Math.Log10(totalVotes + 1));
            return Convert.ToInt32(score * 100);
        }

        internal StoreAppDetailsResult.AppDetails GetStoreData(uint appId)
        {
            return SendDelayedStoreRequest(() => webApiClient.GetStoreAppDetail(appId, settings.LanguageKey), appId);
        }

        internal AppReviewsResult.QuerySummary GetUserReviewsData(uint appId)
        {
            var ratings = SendDelayedStoreRequest(() => webApiClient.GetUserRating(appId), appId);
            if (ratings?.success == 1)
            {
                return ratings.query_summary;
            }
            else
            {
                return null;
            }
        }

        internal SteamGameMetadata DownloadGameMetadata(
            uint appId,
            BackgroundSource backgroundSource,
            bool downloadVerticalCovers)
        {
            var metadata = new SteamGameMetadata();
            var productInfo = GetAppInfo(appId);
            metadata.ProductDetails = productInfo;

            try
            {
                metadata.StoreDetails = GetStoreData(appId);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to download Steam store metadata {appId}");
            }

            try
            {
                metadata.UserReviewDetails = GetUserReviewsData(appId);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to download Steam user reviews metadata {appId}");
            }

            // Icon
            if (productInfo != null)
            {
                var iconRoot = @"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/{0}/{1}.ico";
                var icon = productInfo["common"]["clienticon"];
                var iconUrl = string.Empty;
                if (!string.IsNullOrEmpty(icon.Value))
                {
                    iconUrl = string.Format(iconRoot, appId, icon.Value);
                }
                else
                {
                    var newIcon = productInfo["common"]["icon"];
                    if (!string.IsNullOrEmpty(newIcon.Value))
                    {
                        iconRoot = @"https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/{0}/{1}.jpg";
                        iconUrl = string.Format(iconRoot, appId, newIcon.Value);
                    }
                }

                // There might be no icon assigned to game
                if (!string.IsNullOrEmpty(iconUrl))
                {
                    metadata.Icon = new MetadataFile(iconUrl);
                }
            }

            // Cover Image
            if (downloadVerticalCovers)
            {
                var imageRoot = @"https://steamcdn-a.akamaihd.net/steam/apps/{0}/library_600x900_2x.jpg";
                var imageUrl = string.Format(imageRoot, appId);
                if (HttpDownloader.GetResponseCode(imageUrl, out var _).IsSuccess())
                {
                    metadata.CoverImage = new MetadataFile(imageUrl);
                }
            }
            else
            {
                var imageRoot = @"https://steamcdn-a.akamaihd.net/steam/apps/{0}/header.jpg";
                var imageUrl = string.Format(imageRoot, appId);
                if (HttpDownloader.GetResponseCode(imageUrl, out var _).IsSuccess())
                {
                    metadata.CoverImage = new MetadataFile(imageUrl);
                }
            }

            // Background Image
            var bannerBk = string.Format(@"https://steamcdn-a.akamaihd.net/steam/apps/{0}/library_hero.jpg", appId);
            var storeBk = string.Format(@"https://steamcdn-a.akamaihd.net/steam/apps/{0}/page_bg_generated_v6b.jpg", appId);

            switch (backgroundSource)
            {
                case BackgroundSource.Image:
                    var bk = GetGameBackground(appId);
                    if (string.IsNullOrEmpty(bk))
                    {
                        metadata.BackgroundImage = new MetadataFile(bannerBk);
                    }
                    else
                    {
                        metadata.BackgroundImage = new MetadataFile(bk);
                    }
                    break;
                case BackgroundSource.StoreScreenshot:
                    if (metadata.StoreDetails != null)
                    {
                        metadata.BackgroundImage = new MetadataFile(Regex.Replace(metadata.StoreDetails.screenshots.First().path_full, "\\?.*$", ""));
                    }
                    break;
                case BackgroundSource.StoreBackground:
                    if (HttpDownloader.GetResponseCode(storeBk, out var _).IsSuccess())
                    {
                        metadata.BackgroundImage = new MetadataFile(storeBk);
                    }
                    break;
                case BackgroundSource.Banner:
                    if (HttpDownloader.GetResponseCode(bannerBk, out var _).IsSuccess())
                    {
                        metadata.BackgroundImage = new MetadataFile(bannerBk);
                    }
                    break;
                default:
                    break;
            }

            return metadata;
        }

        internal string ParseDescription(string description)
        {
            return description.Replace("%CDN_HOST_MEDIA_SSL%", "steamcdn-a.akamaihd.net");
        }

        public SteamGameMetadata GetGameMetadata(
            uint appId,
            BackgroundSource backgroundSource,
            bool downloadVerticalCovers,
            bool downloadParentMetadata = true)
        {
            logger.Trace($"Getting metadata for {appId}");
            var metadata = DownloadGameMetadata(appId, backgroundSource, downloadVerticalCovers);
            var newName = metadata.ProductDetails?["common"]["name_localized"][settings.LanguageKey]?.Value;
            if (newName != null)
            {
                metadata.Name = newName;
            }
            else
            {
                metadata.Name = metadata.ProductDetails?["common"]["name"]?.Value ?? metadata.StoreDetails?.name;
            }

            metadata.Links = new List<Link>()
            {
                new Link(ResourceProvider.GetString(LOC.SteamLinksCommunityHub), $"https://steamcommunity.com/app/{appId}"),
                new Link(ResourceProvider.GetString(LOC.SteamLinksDiscussions), $"https://steamcommunity.com/app/{appId}/discussions/"),
                new Link(ResourceProvider.GetString(LOC.SteamLinksGuides), $"https://steamcommunity.com/app/{appId}/guides/"),
                new Link(ResourceProvider.GetString("LOCCommonLinksNews"), $"https://store.steampowered.com/news/?appids={appId}"),
                new Link(ResourceProvider.GetString("LOCCommonLinksStorePage"), $"https://store.steampowered.com/app/{appId}"),
                new Link("PCGamingWiki", $"https://pcgamingwiki.com/api/appid.php?appid={appId}")
            };

            if (metadata.StoreDetails?.categories?.FirstOrDefault(a => a.id == 22) != null)
            {
                metadata.Links.Add(new Link(ResourceProvider.GetString("LOCCommonLinksAchievements"), GetAchievementsUrl(appId)));
            }

            if (metadata.StoreDetails?.categories?.FirstOrDefault(a => a.id == 30) != null)
            {
                metadata.Links.Add(new Link(ResourceProvider.GetString("LOCSteamLinksWorkshop"), GetWorkshopUrl(appId)));
            }

            var features = new HashSet<MetadataProperty>();
            IEnumerable<string> publishers = null;
            IEnumerable<string> developers = null;
            if (metadata.StoreDetails != null)
            {
                metadata.Description = ParseDescription(metadata.StoreDetails.about_the_game);
                var cultInfo = new CultureInfo("en-US", false).TextInfo;

                metadata.ReleaseDate = DateHelper.ParseReleaseDate(metadata.StoreDetails.release_date.date);
                metadata.CriticScore = metadata.StoreDetails.metacritic?.score;
                if (metadata.UserReviewDetails?.total_reviews > 0)
                {
                    metadata.CommunityScore = CalculateUserScore(metadata.UserReviewDetails);
                }

                publishers = metadata.StoreDetails.publishers?.Where(a => !a.IsNullOrWhiteSpace());
                if (publishers.HasItems())
                {
                    metadata.Publishers = publishers.
                        Select(a => new MetadataNameProperty(a)).
                        Cast<MetadataProperty>().
                        ToHashSet();
                }

                developers = metadata.StoreDetails.developers?.Where(a => !a.IsNullOrWhiteSpace());
                if (developers.HasItems())
                {
                    metadata.Developers = developers.
                        Select(a => new MetadataNameProperty(a)).
                        Cast<MetadataProperty>().
                        ToHashSet();
                }

                metadata.Features = features;
                if (metadata.StoreDetails.categories.HasItems())
                {
                    foreach (var category in metadata.StoreDetails.categories)
                    {
                        // Ignore VR category, will be set from appinfo
                        if (category.id == 31)
                        {
                            continue;
                        }

                        if (category.description == "Steam Cloud")
                        {
                            category.description = "Cloud Saves";
                        }

                        features.Add(new MetadataNameProperty(cultInfo.ToTitleCase(category.description.Replace("steam", "", StringComparison.OrdinalIgnoreCase).Trim())));
                    }
                }

                if (metadata.StoreDetails.genres.HasItems())
                {
                    metadata.Genres = metadata.StoreDetails.genres.Select(a => new MetadataNameProperty(a.description)).Cast<MetadataProperty>().ToHashSet();
                }

                if (metadata.StoreDetails.platforms != null)
                {
                    metadata.Platforms = new HashSet<MetadataProperty>();

                    if (metadata.StoreDetails.platforms.windows)
                    {
                        metadata.Platforms.Add(new MetadataSpecProperty("pc_windows"));
                    }

                    if (metadata.StoreDetails.platforms.mac)
                    {
                        metadata.Platforms.Add(new MetadataSpecProperty("macintosh"));
                    }

                    if (metadata.StoreDetails.platforms.linux)
                    {
                        metadata.Platforms.Add(new MetadataSpecProperty("pc_linux"));
                    }
                }
                else
                {
                    metadata.Platforms = new HashSet<MetadataProperty> { new MetadataSpecProperty("pc_windows") };
                }
            }

            if (metadata.ProductDetails != null)
            {
                // VR features
                var vrSupport = false;
                foreach (var vrArea in metadata.ProductDetails["common"]["playareavr"].Children)
                {
                    if (vrArea.Name == "seated" && vrArea.Value == "1")
                    {
                        features.Add(new MetadataNameProperty("VR Seated"));
                        vrSupport = true;
                    }
                    else if (vrArea.Name == "standing" && vrArea.Value == "1")
                    {
                        features.Add(new MetadataNameProperty("VR Standing"));
                        vrSupport = true;
                    }
                    if (vrArea.Name.Contains("roomscale"))
                    {
                        features.Add(new MetadataNameProperty("VR Room-Scale"));
                        vrSupport = true;
                    }
                }

                foreach (var vrArea in metadata.ProductDetails["common"]["controllervr"].Children)
                {
                    if (vrArea.Name == "kbm" && vrArea.Value == "1")
                    {
                        features.Add(new MetadataNameProperty("VR Keyboard / Mouse"));
                        vrSupport = true;
                    }
                    else if (vrArea.Name == "xinput" && vrArea.Value == "1")
                    {
                        features.Add(new MetadataNameProperty("VR Gamepad"));
                        vrSupport = true;
                    }
                    if ((vrArea.Name == "oculus" && vrArea.Value == "1") ||
                        (vrArea.Name == "steamvr" && vrArea.Value == "1"))
                    {
                        features.Add(new MetadataNameProperty("VR Motion Controllers"));
                        vrSupport = true;
                    }
                }

                if (vrSupport)
                {
                    features.Add(new MetadataNameProperty("VR"));
                }

                foreach (var ass in metadata.ProductDetails["common"]["associations"].Children)
                {
                    if (ass["type"].Value == "franchise")
                    {
                        var value = HttpUtility.HtmlDecode(ass["name"].Value);
                        if (publishers?.Any(x => x.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                            value.Contains(x, StringComparison.OrdinalIgnoreCase)) == true)
                        {
                            logger.Debug($"Franchise value \"{value}\" of game \"{metadata.Name}\" matched a publisher name and was skipped");
                            break;
                        }

                        if (developers?.Any(x => x.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                            value.Contains(x, StringComparison.OrdinalIgnoreCase)) == true)
                        {
                            logger.Debug($"Franchise value \"{value}\" of game \"{metadata.Name}\" matched a developer name and was skipped");
                            break;
                        }

                        metadata.Series = new HashSet<MetadataProperty> { new MetadataNameProperty(value) };
                        break;
                    }
                }

                var tagNames = tagNamer.GetTagNames();
                Dictionary<int, string> newTagNames = null;

                var tagKeyValues = metadata.ProductDetails["common"]["store_tags"]?.Children;
                logger.Debug($"Setting tags for {appId}, found {tagKeyValues?.Count}");
                if (tagKeyValues != null)
                {
                    metadata.Tags = new HashSet<MetadataProperty>();
                    if (settings.LimitTagsToFixedAmount)
                    {
                        tagKeyValues = tagKeyValues.Take(settings.FixedTagCount).ToList();
                    }

                    foreach (var tag in tagKeyValues)
                    {
                        if (!int.TryParse(tag.Value, out int tagId) || settings.BlacklistedTags.Contains(tagId))
                        {
                            continue;
                        }

                        if (!tagNames.TryGetValue(tagId, out string name))
                        {
                            if (newTagNames == null)
                            {
                                logger.Debug($"Tag {tagId} not found. Fetching new ones.");
                                newTagNames = tagNamer.UpdateAndGetTagNames();
                            }
                            if (!newTagNames.TryGetValue(tagId, out name))
                            {
                                logger.Warn($"Could not find tag name for tag {tagId}");
                                continue;
                            }
                        }
                        name = tagNamer.GetFinalTagName(name);
                        metadata.Tags.Add(new MetadataNameProperty(name));
                    }
                }
            }

            string appType = metadata.ProductDetails?["common"]["type"]?.Value;
            string parentStr = metadata.ProductDetails?["common"]["parent"]?.Value;

            if (downloadParentMetadata
                && childGameTypes.Contains(appType)
                && uint.TryParse(parentStr, out uint parentId))
            {
                logger.Debug($"Getting parent metadata for {appId} from {parentId}");
                var parentMetadata = GetGameMetadata(parentId, backgroundSource, downloadVerticalCovers, downloadParentMetadata: false);

                metadata.Links = parentMetadata.Links; //demo appId urls either redirect to the main game or are broken
                metadata.CoverImage = metadata.CoverImage ?? parentMetadata.CoverImage;
                metadata.BackgroundImage = metadata.BackgroundImage ?? parentMetadata.BackgroundImage;

                if (string.IsNullOrWhiteSpace(metadata.Description))
                {
                    metadata.Description = parentMetadata.Description;
                }

                if (metadata.Tags == null || metadata.Tags.Count == 0)
                {
                    metadata.Tags = parentMetadata.Tags;
                }
            }

            var compatibility = GetSteamDeckCompatibility(metadata.ProductDetails);
            AddProperty(metadata, settings.SteamDeckCompatibilityField, compatibility);

            return metadata;
        }

        private string GetGameBackground(uint appId)
        {
            foreach (var url in backgroundUrls)
            {
                var bk = string.Format(url, appId);
                if (HttpDownloader.GetResponseCode(bk, out var headers).IsSuccess())
                {
                    // One of these sources sometimes returns images that are badly encoded and are 20MBs and more.
                    // 10MBs should be safe limit.
                    if (headers.TryGetValue("Content-Length", out var ln) &&
                        int.TryParse(ln, out var length) &&
                        length < 10_000_000)
                    {
                        return bk;
                    }
                }
            }

            return null;
        }

        private string GetSteamDeckCompatibility(KeyValue productDetails)
        {
            string categoryStr = productDetails["common"]["steam_deck_compatibility"]["category"].Value;
            var compatibility = SteamDeckCompatibility.Unknown;

            if (int.TryParse(categoryStr, out int category) && Enum.IsDefined(typeof(SteamDeckCompatibility), category))
                compatibility = (SteamDeckCompatibility)category;

            var compatSetting = settings.SteamDeckCompatibilitySettings.FirstOrDefault(x => x.Compatibility == compatibility);
            if (compatSetting == null || !compatSetting.IsChecked)
                return null;

            return $"Steam Deck {compatibility}";
        }

        private void AddProperty(GameMetadata game, GameField field, string value)
        {
            if (value == null)
                return;

            switch (field)
            {
                case GameField.Features:
                    if (game.Features == null)
                        game.Features = new HashSet<MetadataProperty>();

                    game.Features.Add(new MetadataNameProperty(value));
                    break;
                case GameField.Tags:
                    if (game.Tags == null)
                        game.Tags = new HashSet<MetadataProperty>();

                    game.Tags.Add(new MetadataNameProperty(value));
                    break;
            }
        }
    }
}
