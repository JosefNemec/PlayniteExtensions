using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.SDK;
using GogLibrary.Models;
using System.IO;
using Playnite;
using System.Collections.ObjectModel;
using System.Globalization;
using GogLibrary.Services;
using System.Text.RegularExpressions;

namespace GogLibrary
{
    public class GogMetadataProvider : LibraryMetadataProvider
    {
        private GogApiClient apiClient = new GogApiClient();
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI api;
        private readonly GogLibrarySettings settings;

        public GogMetadataProvider(IPlayniteAPI api, GogLibrarySettings settings)
        {
            this.api = api;
            this.settings = settings;
        }

        public override GameMetadata GetMetadata(Game game)
        {
            var resources = api.Resources;
            var storeData = DownloadGameMetadata(game);
            if (storeData.GameDetails == null)
            {
                logger.Warn($"Could not gather metadata for game {game.GameId}");
                return null;
            }

            storeData.Name = StringExtensions.NormalizeGameName(storeData.GameDetails.title);
            storeData.Description = RemoveDescriptionPromos(storeData.GameDetails.description.full);
            storeData.Links = new List<Link>();

            if (!string.IsNullOrEmpty(storeData.GameDetails.links.forum))
            {
                storeData.Links.Add(new Link(resources.GetString("LOCCommonLinksForum"), storeData.GameDetails.links.forum));
            };

            if (!string.IsNullOrEmpty(storeData.GameDetails.links.product_card))
            {
                storeData.Links.Add(new Link(resources.GetString("LOCCommonLinksStorePage"), storeData.GameDetails.links.product_card));
            };

            storeData.Links.Add(new Link("PCGamingWiki", @"http://pcgamingwiki.com/w/index.php?search=" + storeData.GameDetails.title));

            if (storeData.StoreDetails != null)
            {
                storeData.Genres = storeData.StoreDetails.genres?.Select(a => new MetadataNameProperty(a.name)).ToHashSet<MetadataProperty>();
                storeData.Features = storeData.StoreDetails.features?.Where(a => a.name != "Overlay").Select(a => new MetadataNameProperty(a.name)).ToHashSet<MetadataProperty>();
                storeData.Developers = storeData.StoreDetails.developers.Select(a => new MetadataNameProperty(a.name)).ToHashSet<MetadataProperty>();
                storeData.Publishers = new HashSet<MetadataProperty>() { new MetadataNameProperty(storeData.StoreDetails.publisher) };
                storeData.Tags = storeData.StoreDetails.gameTags?.Select(t => new MetadataNameProperty(t.name)).ToHashSet<MetadataProperty>();
                if (storeData.ReleaseDate == null && storeData.StoreDetails.globalReleaseDate != null)
                {
                    storeData.ReleaseDate = new ReleaseDate(storeData.StoreDetails.globalReleaseDate.Value);
                }

                if (storeData.StoreDetails.size > 0)
                {
                    //StoreDetails.size is in megabytes, convert to bytes here
                    storeData.InstallSize = (ulong)storeData.StoreDetails.size * 1024UL * 1024UL;
                }

                if(settings.UseVerticalCovers && storeData.StoreDetails.boxArtImage != null)
                {
                    storeData.CoverImage = new MetadataFile(storeData.StoreDetails.boxArtImage);
                }
            }

            return storeData;
        }

        internal GogGameMetadata DownloadGameMetadata(Game game)
        {
            var metadata = new GogGameMetadata();
            var gameDetail = apiClient.GetGameDetails(game.GameId, settings.Locale);
            if (gameDetail == null)
            {
                logger.Warn($"Product page for game {game.GameId} not found, using fallback search.");
                var search = apiClient.GetStoreSearch(game.Name);
                var match = search?.FirstOrDefault(a => a.title.Equals(game.Name, StringComparison.InvariantCultureIgnoreCase));
                if (match != null)
                {
                    gameDetail = apiClient.GetGameDetails(match.id.ToString(), settings.Locale);
                }
            }

            metadata.GameDetails = gameDetail;

            if (gameDetail != null)
            {
                if (gameDetail.links.product_card != @"https://www.gog.com/" && !string.IsNullOrEmpty(gameDetail.links.product_card))
                {
                    string gamePath = gameDetail.links.product_card.Substring(gameDetail.links.product_card.IndexOf("game/"));
                    var productUrl = $"https://www.gog.com/{settings.Locale}/{gamePath}";
                    metadata.StoreDetails = apiClient.GetGameStoreData(productUrl);
                }

                metadata.Icon = new MetadataFile("http:" + gameDetail.images.icon);
                if (!settings.UseVerticalCovers)
                {
                    if (metadata.StoreDetails != null)
                    {
                        var imageUrl = metadata.StoreDetails.image + "_product_card_v2_mobile_slider_639.jpg";
                        metadata.CoverImage = new MetadataFile(imageUrl);
                    }
                    else
                    {
                        metadata.CoverImage = new MetadataFile("http:" + gameDetail.images.logo2x);
                    }
                }

                if (metadata.StoreDetails != null)
                {
                    var url = metadata.StoreDetails.galaxyBackgroundImage ?? metadata.StoreDetails.backgroundImage;
                    metadata.BackgroundImage = new MetadataFile(url.Replace(".jpg", "_bg_crop_1920x655.jpg"));
                }
                else
                {
                    metadata.BackgroundImage = new MetadataFile("http:" + gameDetail.images.background);
                }
            }

            return metadata;
        }

        internal string RemoveDescriptionPromos(string originalDescription)
        {
            if (originalDescription.IsNullOrEmpty())
            {
                return originalDescription;
            }

            // It's possible to check if a description has a promo if they contain known promo image urls
            if (!Regex.IsMatch(originalDescription, @"<img src=""https:\/\/items.gog.com\/(promobanners|autumn|fall|summer|winter)\/"))
            {
                return originalDescription;
            }

            // Get opening element in description. Promos are always at the start of description.
            // It has been seen that descriptions start with <a> or <div> elements
            var openingTagMatch = Regex.Match(originalDescription, @"^<(\w+)");
            if (!openingTagMatch.Success)
            {
                return originalDescription;
            }

            var openingTagElement = openingTagMatch.Groups[1];
            var openingTag = string.Format("<{0}", openingTagElement);
            var closingTag = string.Format("</{0}>", openingTagElement);

            var openingTagsCount = 1;
            var closingTagsCount = 0;
            var newDescription = originalDescription.Substring(openingTag.Length);

            // Although not seen yet in promo blocks, check for nested elements of the same tag by
            // counting number of opening and closing elements to prevent not removing all the promo
            while (true)
            {
                var openingIndex = newDescription.IndexOf(openingTag);
                var closingIndex = newDescription.IndexOf(closingTag);
                if (closingIndex == -1)
                {
                    // Return original description if for some reason closing tag is not found
                    return originalDescription;
                }

                if (openingIndex != -1 && openingIndex < closingIndex)
                {
                    // This means there's a nested element with the same tag
                    openingTagsCount++;

                    // Store description after nested element to keep checking
                    newDescription = newDescription.Substring(openingIndex + openingTag.Length);
                }
                else
                {
                    closingTagsCount++;
                    newDescription = newDescription.Substring(closingIndex + closingTag.Length);
                    if (openingTagsCount == closingTagsCount)
                    {
                        // Same number of elements means the whole promo block has been detected
                        break;
                    }
                }
            }

            // Remove all starting <hr> and <br> elements that GOG adds after a promo
            newDescription = Regex.Replace(newDescription, @"^(<hr>|<br>)+", string.Empty);
            return newDescription.Trim();
        }
    }
}
