using IGDBMetadata.Models;
using Playnite.Common;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static IGDBMetadata.IgdbMetadataPlugin;
using IgdbServerModels = PlayniteServices.Models.IGDB;

namespace IGDBMetadata
{
    public class IgdbLazyMetadataProvider : OnDemandMetadataProvider
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly MetadataRequestOptions options;
        private readonly IgdbMetadataPlugin plugin;
        internal IgdbServerModels.ExpandedGame IgdbData { get; private set; }

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

        private IgdbImageOption GetBackgroundManually(List<IgdbServerModels.GameImage> possibleBackgrounds)
        {
            var selection = new List<ImageFileOption>();
            foreach (var artwork in possibleBackgrounds)
            {
                selection.Add(new IgdbImageOption
                {
                    Path = IgdbMetadataPlugin.GetImageUrl(artwork, ImageSizes.screenshot_med),
                    Image = artwork
                });
            }
            return plugin.PlayniteApi.Dialogs.ChooseImageFile(
                selection,
                string.Format(plugin.PlayniteApi.Resources.GetString(LOC.IgdbSelectBackgroundTitle), IgdbData.name)) as IgdbImageOption;
        }

        public override MetadataFile GetBackgroundImage(GetMetadataFieldArgs args)
        {
            var settings = plugin.SettingsViewModel.Settings;
            if (AvailableFields.Contains(MetadataField.BackgroundImage))
            {
                List<IgdbServerModels.GameImage> possibleBackgrounds = null;
                if (IgdbData.artworks.HasItems())
                {
                    possibleBackgrounds = IgdbData.artworks.Where(a => !a.url.IsNullOrEmpty()).ToList();
                }
                else if (settings.UseScreenshotsIfNecessary && IgdbData.screenshots.HasItems())
                {
                    possibleBackgrounds = IgdbData.screenshots.Where(a => !a.url.IsNullOrEmpty()).ToList();
                }

                if (possibleBackgrounds.HasItems())
                {
                    IgdbServerModels.GameImage selected = null;
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
                                selected = GetBackgroundManually(possibleBackgrounds)?.Image;
                            }
                        }
                        else
                        {
                            selected = GetBackgroundManually(possibleBackgrounds)?.Image;
                        }
                    }

                    if (selected != null && !selected.url.IsNullOrEmpty())
                    {
                        if (selected.height > 1080)
                        {
                            return new MetadataFile(GetImageUrl(selected, ImageSizes.p1080));
                        }
                        else
                        {
                            return new MetadataFile(GetImageUrl(selected, ImageSizes.original));
                        }
                    }
                }
            }

            return base.GetBackgroundImage(args);
        }

        public override int? GetCommunityScore(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.CommunityScore))
            {
                return Convert.ToInt32(IgdbData.rating);
            }

            return base.GetCommunityScore(args);
        }

        public override MetadataFile GetCoverImage(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.CoverImage) && !IgdbData.cover.url.IsNullOrEmpty())
            {
                if (IgdbData.cover.height > 1080)
                {
                    return new MetadataFile(GetImageUrl(IgdbData.cover, ImageSizes.p1080));
                }
                else
                {
                    return new MetadataFile(GetImageUrl(IgdbData.cover, ImageSizes.original));
                }
            }

            return base.GetCoverImage(args);
        }

        public override int? GetCriticScore(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.CriticScore))
            {
                return Convert.ToInt32(IgdbData.aggregated_rating);
            }

            return base.GetCriticScore(args);
        }

        public override string GetDescription(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Description))
            {
                return IgdbData.summary.Replace("\n", "\n<br>");
            }

            return base.GetDescription(args);
        }

        public override IEnumerable<MetadataProperty> GetDevelopers(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Developers))
            {
                return IgdbData.involved_companies?.Where(a => a.developer).Select(a => new MetadataNameProperty(a.company.name)).ToList();
            }

            return base.GetDevelopers(args);
        }

        public override IEnumerable<MetadataProperty> GetGenres(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Genres))
            {
                return IgdbData.genres?.Select(a => new MetadataNameProperty(a.name)).ToList();
            }

            return base.GetGenres(args);
        }

        public override string GetName(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Name))
            {
                return IgdbData.name;
            }

            return base.GetName(args);
        }

        public override IEnumerable<MetadataProperty> GetPublishers(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Publishers))
            {
                return IgdbData.involved_companies?.Where(a => a.publisher).Select(a => new MetadataNameProperty(a.company.name)).ToList();
            }

            return base.GetPublishers(args);
        }

        public override IEnumerable<MetadataProperty> GetAgeRatings(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.AgeRating))
            {
                return IgdbData.age_ratings.Select(a => new MetadataNameProperty(a.category + " " + a.rating.GetDescription())).ToList();
            }

            return base.GetAgeRatings(args);
        }

        public override IEnumerable<MetadataProperty> GetSeries(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Series))
            {
                return new HashSet<MetadataProperty> { new MetadataNameProperty(IgdbData.collection.name) };
            }

            return base.GetSeries(args);
        }

        public override ReleaseDate? GetReleaseDate(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.ReleaseDate))
            {
                return new ReleaseDate(DateTimeOffset.FromUnixTimeMilliseconds(IgdbData.first_release_date).DateTime);
            }

            return base.GetReleaseDate(args);
        }

        public override IEnumerable<MetadataProperty> GetFeatures(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Features))
            {
                var cultInfo = new CultureInfo("en-US", false).TextInfo;
                var features = IgdbData.game_modes.Select(a => new MetadataNameProperty(cultInfo.ToTitleCase(a.name))).ToList();
                if (IgdbData.player_perspectives.HasItems() &&
                    IgdbData.player_perspectives.FirstOrDefault(a => a.name == "Virtual Reality") != null)
                {
                    features.Add(new MetadataNameProperty("VR"));
                }

                return features;
            }
            return base.GetFeatures(args);
        }

        public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Links))
            {
                return IgdbData.websites.Where(a => !a.url.IsNullOrEmpty()).Select(a => new Link(a.category.GetDescription(), a.url)).ToList();
            }

            return base.GetLinks(args);
        }

        private List<MetadataField> GetAvailableFields()
        {
            if (IgdbData == null)
            {
                GetIgdbMetadata();
            }

            if (IgdbData.id == 0)
            {
                return new List<MetadataField>();
            }
            else
            {
                var fields = new List<MetadataField> { MetadataField.Name };
                if (!IgdbData.summary.IsNullOrEmpty())
                {
                    fields.Add(MetadataField.Description);
                }

                if (IgdbData.cover != null)
                {
                    fields.Add(MetadataField.CoverImage);
                }

                if (IgdbData.artworks.HasItems())
                {
                    fields.Add(MetadataField.BackgroundImage);
                }
                else if (IgdbData.screenshots.HasItems() && plugin.SettingsViewModel.Settings.UseScreenshotsIfNecessary)
                {
                    fields.Add(MetadataField.BackgroundImage);
                }

                if (IgdbData.first_release_date != 0)
                {
                    fields.Add(MetadataField.ReleaseDate);
                }

                if (IgdbData.involved_companies.HasItems(a => a.developer))
                {
                    fields.Add(MetadataField.Developers);
                }

                if (IgdbData.involved_companies.HasItems(a => a.publisher))
                {
                    fields.Add(MetadataField.Publishers);
                }

                if (IgdbData.genres.HasItems())
                {
                    fields.Add(MetadataField.Genres);
                }

                if (IgdbData.websites.HasItems())
                {
                    fields.Add(MetadataField.Links);
                }

                if (IgdbData.game_modes.HasItems())
                {
                    fields.Add(MetadataField.Features);
                }

                if (IgdbData.aggregated_rating != 0)
                {
                    fields.Add(MetadataField.CriticScore);
                }

                if (IgdbData.rating != 0)
                {
                    fields.Add(MetadataField.CommunityScore);
                }

                if (IgdbData.age_ratings.HasItems())
                {
                    fields.Add(MetadataField.AgeRating);
                }

                if (IgdbData.collection != null)
                {
                    fields.Add(MetadataField.Series);
                }

                return fields;
            }
        }

        private void GetIgdbMetadata()
        {
            if (IgdbData != null)
            {
                return;
            }

            if (!options.IsBackgroundDownload)
            {
                var item = plugin.PlayniteApi.Dialogs.ChooseItemWithSearch(null, (a) =>
                {
                    if (a.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var gameId = GetGameInfoFromUrl(a);
                            var data = plugin.Client.GetIGDBGameExpanded(ulong.Parse(gameId));
                            return new List<GenericItemOption> { new SearchResult(gameId, data.name) };
                        }
                        catch (Exception e)
                        {
                            logger.Error(e, $"Failed to get game data from {a}");
                            return new List<GenericItemOption>();
                        }
                    }
                    else
                    {
                        if (a.IsNullOrWhiteSpace())
                        {
                            return new List<GenericItemOption>();
                        }
                        else
                        {
                            var res = plugin.GetSearchResults(a.Replace("\\", "").Replace("/", "").Trim());
                            return res.Select(b => b as GenericItemOption).ToList();
                        }
                    }
                }, options.GameData.Name);

                if (item != null)
                {
                    var searchItem = item as SearchResult;
                    IgdbData = plugin.Client.GetIGDBGameExpanded(ulong.Parse(searchItem.Id));
                }
                else
                {
                    IgdbData = new IgdbServerModels.ExpandedGame() { id = 0 };
                }
            }
            else
            {
                try
                {
                    var metadata = plugin.Client.GetMetadata(options.GameData);
                    if (metadata.id > 0)
                    {
                        IgdbData = metadata;
                    }
                    else
                    {
                        IgdbData = new IgdbServerModels.ExpandedGame() { id = 0 };
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to get IGDB metadata.");
                    IgdbData = new IgdbServerModels.ExpandedGame() { id = 0 };
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
