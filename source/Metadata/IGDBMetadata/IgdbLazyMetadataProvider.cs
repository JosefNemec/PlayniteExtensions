using Playnite.Common;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteServices.IGDB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static IGDBMetadata.IgdbMetadataPlugin;
using Igdb = PlayniteServices.IGDB;

namespace IGDBMetadata
{
    public class IgdbLazyMetadataProvider : OnDemandMetadataProvider
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly CultureInfo cultInfo = new CultureInfo("en-US", false);
        private readonly MetadataRequestOptions options;
        private readonly IgdbMetadataPlugin plugin;
        private Igdb.Game GameData;

        private readonly static Dictionary<AgeRatingRatingEnum, string> pegiRatingToStr = new Dictionary<AgeRatingRatingEnum, string>()
        {
            [AgeRatingRatingEnum.THREE] = "3",
            [AgeRatingRatingEnum.SEVEN] = "7",
            [AgeRatingRatingEnum.TWELVE] = "12",
            [AgeRatingRatingEnum.SIXTEEN] = "16",
            [AgeRatingRatingEnum.EIGHTEEN] = "18"
        };

        private readonly static Dictionary<WebsiteCategoryEnum, string> websiteCategoryToStr = new Dictionary<WebsiteCategoryEnum, string>()
        {
            [WebsiteCategoryEnum.WEBSITE_ANDROID] = "Android",
            [WebsiteCategoryEnum.WEBSITE_DISCORD] = "Discord",
            [WebsiteCategoryEnum.WEBSITE_EPICGAMES] = "Epic",
            [WebsiteCategoryEnum.WEBSITE_FACEBOOK] = "Facebook",
            [WebsiteCategoryEnum.WEBSITE_GOG] = "GOG",
            [WebsiteCategoryEnum.WEBSITE_INSTAGRAM] = "Instagram",
            [WebsiteCategoryEnum.WEBSITE_IPAD] = "iPad",
            [WebsiteCategoryEnum.WEBSITE_IPHONE] = "iPhone",
            [WebsiteCategoryEnum.WEBSITE_ITCH] = "Itch",
            [WebsiteCategoryEnum.WEBSITE_OFFICIAL] = "Official",
            [WebsiteCategoryEnum.WEBSITE_REDDIT] = "Reddit",
            [WebsiteCategoryEnum.WEBSITE_STEAM] = "Steam",
            [WebsiteCategoryEnum.WEBSITE_TWITCH] = "Twitch",
            [WebsiteCategoryEnum.WEBSITE_TWITTER] = "Twitter",
            [WebsiteCategoryEnum.WEBSITE_WIKIA] = "Wikia",
            [WebsiteCategoryEnum.WEBSITE_WIKIPEDIA] = "Wikipedia",
            [WebsiteCategoryEnum.WEBSITE_YOUTUBE] = "YouTube",
        };

        private List<MetadataField> availableFields;
        public override List<MetadataField> AvailableFields
        {
            get
            {
                if (availableFields == null)
                {
                    availableFields = GetAvailableFields();
                }

                return availableFields;
            }
        }

        public IgdbLazyMetadataProvider(MetadataRequestOptions options, IgdbMetadataPlugin plugin)
        {
            this.options = options;
            this.plugin = plugin;
        }

        private IIgdbItem GetBackgroundManually(List<IIgdbItem> possibleBackgrounds)
        {
            var selection = new List<ImageFileOption>();
            foreach (var image in possibleBackgrounds)
            {
                if (image is Artwork artwork)
                {
                    selection.Add(new IgdbImageSlectItem(artwork));
                }
                else if (image is Screenshot screenshot)
                {
                    selection.Add(new IgdbImageSlectItem(screenshot));
                }
            }

            var res = plugin.PlayniteApi.Dialogs.ChooseImageFile(
                selection,
                string.Format(plugin.PlayniteApi.Resources.GetString(LOC.IgdbSelectBackgroundTitle), GameData.name));
            if (res == null)
            {
                return null;
            }
            else
            {
                return (res as IgdbImageSlectItem).Image;
            }
        }

        public override MetadataFile GetBackgroundImage(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.BackgroundImage))
            {
                return base.GetBackgroundImage(args);
            }

            var settings = plugin.SettingsViewModel.Settings;
            var possibleBackgrounds = new List<IIgdbItem>();
            if (GameData.artworks_expanded.HasItems())
            {
                possibleBackgrounds.AddRange(GameData.artworks_expanded.Where(a => !a.url.IsNullOrEmpty()));
            }
            else if (settings.UseScreenshotsIfNecessary && GameData.screenshots_expanded.HasItems())
            {
                possibleBackgrounds.AddRange(GameData.screenshots_expanded.Where(a => !a.url.IsNullOrEmpty()));
            }

            if (!possibleBackgrounds.HasItems())
            {
                return base.GetBackgroundImage(args);
            }

            IIgdbItem selected = null;
            if (possibleBackgrounds.Count == 1)
            {
                selected = possibleBackgrounds[0];
            }
            else
            {
                if (options.IsBackgroundDownload)
                {
                    if (settings.ImageSelectionPriority == MultiImagePriority.First)
                    {
                        selected = possibleBackgrounds[0];
                    }
                    else if (settings.ImageSelectionPriority == MultiImagePriority.Random ||
                        (settings.ImageSelectionPriority == MultiImagePriority.Select && plugin.PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Fullscreen))
                    {
                        var index = GlobalRandom.Next(0, possibleBackgrounds.Count - 1);
                        selected = possibleBackgrounds[index];
                    }
                    else if (settings.ImageSelectionPriority == MultiImagePriority.Select && plugin.PlayniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop)
                    {
                        selected = GetBackgroundManually(possibleBackgrounds);
                    }
                }
                else
                {
                    selected = GetBackgroundManually(possibleBackgrounds);
                }
            }

            if (selected != null)
            {
                if (selected is Artwork artwork)
                {
                    return new MetadataFile(GetImageUrl(artwork.url, artwork.height > 1080 ? ImageSizes.p1080 : ImageSizes.original));
                }
                else if (selected is Screenshot screenshot)
                {
                    return new MetadataFile(GetImageUrl(screenshot.url, screenshot.height > 1080 ? ImageSizes.p1080 : ImageSizes.original));
                }
            }

            return base.GetBackgroundImage(args);
        }

        public override int? GetCommunityScore(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.CommunityScore))
            {
                return base.GetCommunityScore(args);
            }

            return Convert.ToInt32(GameData.rating);
        }

        public override MetadataFile GetCoverImage(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.CoverImage))
            {
                return base.GetCoverImage(args);
            }

            return new MetadataFile(GetImageUrl(GameData.cover_expanded.url, GameData.cover_expanded.height > 1080 ? ImageSizes.p1080 : ImageSizes.original));
        }

        public override MetadataFile GetIcon(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.Icon))
            {
                return base.GetCoverImage(args);
            }

            return new MetadataFile(GetImageUrl(GameData.cover_expanded.url, ImageSizes.thumb + "_2x"));
        }

        public override int? GetCriticScore(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.CriticScore))
            {
                return base.GetCriticScore(args);
            }

            return Convert.ToInt32(GameData.aggregated_rating);
        }

        public override string GetDescription(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.Description))
            {
                return base.GetDescription(args);
            }

            return GameData.summary.Replace("\n", "\n<br>");
        }

        public override IEnumerable<MetadataProperty> GetDevelopers(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.Developers))
            {
                return base.GetDevelopers(args);
            }

            return GameData.involved_companies_expanded?.
                Where(a => a.developer && a.company_expanded != null).
                Select(a => new MetadataNameProperty(a.company_expanded.name)).
                ToList();

        }

        public override IEnumerable<MetadataProperty> GetPublishers(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.Publishers))
            {
                return base.GetPublishers(args);
            }

            return GameData.involved_companies_expanded?.
                Where(a => a.publisher && a.company_expanded != null).
                Select(a => new MetadataNameProperty(a.company_expanded.name)).
                ToList();
        }

        public override IEnumerable<MetadataProperty> GetGenres(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.Genres))
            {
                return base.GetGenres(args);
            }

            return GameData.genres_expanded?.Select(a => new MetadataNameProperty(a.name)).ToList();
        }

        public override string GetName(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.Name))
            {
                return base.GetName(args);
            }

            return GameData.name;
        }

        public override IEnumerable<MetadataProperty> GetAgeRatings(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.AgeRating))
            {
                return base.GetAgeRatings(args);
            }

            if (plugin.PlayniteApi.ApplicationSettings.AgeRatingOrgPriority == AgeRatingOrg.ESRB)
            {
                return GameData.age_ratings_expanded.
                    Where(a => a.category == AgeRatingCategoryEnum.ESRB).
                    Select(a => new MetadataNameProperty($"ESRB {a.rating}")).
                    ToList();
            }
            else if (plugin.PlayniteApi.ApplicationSettings.AgeRatingOrgPriority == AgeRatingOrg.PEGI)
            {
                return GameData.age_ratings_expanded.
                    Where(a => a.category == AgeRatingCategoryEnum.PEGI).
                    Select(a => new MetadataNameProperty("PEGI " + (pegiRatingToStr.TryGetValue(a.rating, out var val) ? val : "None"))).
                    ToList();
            }

            return base.GetAgeRatings(args);
        }

        public override IEnumerable<MetadataProperty> GetSeries(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.Series))
            {
                return base.GetSeries(args);
            }

            return GameData.collections_expanded?.Select(a => new MetadataNameProperty(a.name)).ToList();
        }

        public override Playnite.SDK.Models.ReleaseDate? GetReleaseDate(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.ReleaseDate))
            {
                return base.GetReleaseDate(args);
            }

            return new Playnite.SDK.Models.ReleaseDate(DateTimeOffset.FromUnixTimeSeconds(GameData.first_release_date).DateTime);
        }

        public override IEnumerable<MetadataProperty> GetFeatures(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.Features))
            {
                return base.GetFeatures(args);
            }

            var features = GameData.game_modes_expanded.Select(a => new MetadataNameProperty(cultInfo.TextInfo.ToTitleCase(a.name))).ToList();
            if (GameData.player_perspectives_expanded?.FirstOrDefault(a => a.name == "Virtual Reality") != null)
            {
                features.Add(new MetadataNameProperty("VR"));
            }

            return features;
        }

        public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args)
        {
            if (!AvailableFields.Contains(MetadataField.Links))
            {
                return base.GetLinks(args);
            }

            return GameData.websites_expanded.
                Where(a => !a.url.IsNullOrEmpty()).
                Select(a => new Link(websiteCategoryToStr.TryGetValue(a.category, out var val) ? val : "Uknown", a.url)).
                ToList();
        }

        private List<MetadataField> GetAvailableFields()
        {
            if (GameData == null)
            {
                GetIgdbMetadata();
            }

            if (GameData.id == 0)
            {
                return new List<MetadataField>();
            }
            else
            {
                var fields = new List<MetadataField> { MetadataField.Name };
                if (!GameData.summary.IsNullOrEmpty())
                {
                    fields.Add(MetadataField.Description);
                }

                if (GameData.cover_expanded != null && !GameData.cover_expanded.url.IsNullOrEmpty())
                {
                    fields.Add(MetadataField.CoverImage);
                    if (plugin.SettingsViewModel.Settings.UseCoverAsIcon)
                    {
                        fields.Add(MetadataField.Icon);
                    }
                }

                if (GameData.artworks_expanded.HasItems())
                {
                    fields.Add(MetadataField.BackgroundImage);
                }
                else if (GameData.screenshots_expanded.HasItems() && plugin.SettingsViewModel.Settings.UseScreenshotsIfNecessary)
                {
                    fields.Add(MetadataField.BackgroundImage);
                }

                if (GameData.first_release_date != 0)
                {
                    fields.Add(MetadataField.ReleaseDate);
                }

                if (GameData.involved_companies_expanded.HasItems(a => a.developer))
                {
                    fields.Add(MetadataField.Developers);
                }

                if (GameData.involved_companies_expanded.HasItems(a => a.publisher))
                {
                    fields.Add(MetadataField.Publishers);
                }

                if (GameData.genres_expanded.HasItems())
                {
                    fields.Add(MetadataField.Genres);
                }

                if (GameData.websites_expanded.HasItems())
                {
                    fields.Add(MetadataField.Links);
                }

                if (GameData.game_modes_expanded.HasItems())
                {
                    fields.Add(MetadataField.Features);
                }

                if (GameData.aggregated_rating != 0)
                {
                    fields.Add(MetadataField.CriticScore);
                }

                if (GameData.rating != 0)
                {
                    fields.Add(MetadataField.CommunityScore);
                }

                if (GameData.age_ratings_expanded.HasItems())
                {
                    fields.Add(MetadataField.AgeRating);
                }

                if (GameData.collections_expanded.HasItems())
                {
                    fields.Add(MetadataField.Series);
                }

                return fields;
            }
        }

        private void GetIgdbMetadata()
        {
            if (GameData != null)
            {
                return;
            }

            if (options.IsBackgroundDownload)
            {
                try
                {
                    GameData = plugin.Client.GetMetadata(new MetadataRequest(options.GameData.Name)
                    {
                        ReleaseYear = options.GameData.ReleaseYear ?? 0,
                        LibraryId = options.GameData.PluginId,
                        GameId = options.GameData.GameId
                    }).GetAwaiter().GetResult();

                    if (GameData == null)
                    {
                        GameData = new Igdb.Game() { id = 0 };
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to get IGDB metadata.");
                    GameData = new Igdb.Game() { id = 0 };
                }
            }
            else
            {
                var item = plugin.PlayniteApi.Dialogs.ChooseItemWithSearch(null, (a) =>
                {
                    if (a.IsNullOrWhiteSpace())
                    {
                        return new List<GenericItemOption>();
                    }

                    if (ulong.TryParse(a, out var parsedId))
                    {
                        try
                        {
                            var game = plugin.Client.GetGame(parsedId).GetAwaiter().GetResult();
                            return new List<GenericItemOption> { new IgdbGameSelectItem(game) };
                        }
                        catch (Exception e)
                        {
                            logger.Error(e, $"Failed to get game data from {a}");
                            return new List<GenericItemOption>();
                        }
                    }
                    else
                    {
                        var res = plugin.Client.SearchGames(new SearchRequest(a)).GetAwaiter().GetResult();
                        return res.Select(b => new IgdbGameSelectItem(b)).Cast<GenericItemOption>().ToList();
                    }
                }, options.GameData.Name);

                if (item != null)
                {
                    var searchItem = item as IgdbGameSelectItem;
                    GameData = plugin.Client.GetGame(searchItem.Game.id).GetAwaiter().GetResult();
                }
                else
                {
                    GameData = new Igdb.Game() { id = 0 };
                }
            }
        }

        private string GetGameInfoFromUrl(string url)
        {
            var data = HttpDownloader.DownloadString(url);
            var regex = Regex.Match(data, @"games\/(\d+)\/rates");
            if (regex.Success)
            {
                return regex.Groups[1].Value;
            }
            else
            {
                logger.Error($"Failed to get game id from {url}");
                return string.Empty;
            }
        }
    }
}
