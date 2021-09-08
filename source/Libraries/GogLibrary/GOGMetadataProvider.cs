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

namespace GogLibrary
{
    public class GogMetadataProvider : LibraryMetadataProvider
    {
        private GogApiClient apiClient = new GogApiClient();
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI api;

        public GogMetadataProvider(IPlayniteAPI api)
        {
            this.api = api;
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
            storeData.Description = storeData.GameDetails.description.full;
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
                storeData.Genres = storeData.StoreDetails.genres?.Select(a => new MetadataNameProperty(a.name)).Cast<MetadataProperty>().ToHashSet();
                storeData.Features = storeData.StoreDetails.features?.Where(a => a.name != "Overlay").Select(a => new MetadataNameProperty(a.name)).Cast<MetadataProperty>().ToHashSet();
                storeData.Developers = storeData.StoreDetails.developers.Select(a => new MetadataNameProperty(a.name)).Cast<MetadataProperty>().ToHashSet();
                storeData.Publishers = new HashSet<MetadataProperty>() { new MetadataNameProperty(storeData.StoreDetails.publisher) };
                var cultInfo = new CultureInfo("en-US", false).TextInfo;
                if (storeData.ReleaseDate == null && storeData.StoreDetails.globalReleaseDate != null)
                {
                    storeData.ReleaseDate = new ReleaseDate(storeData.StoreDetails.globalReleaseDate.Value);
                }
            }

            return storeData;
        }

        internal GogGameMetadata DownloadGameMetadata(Game game)
        {
            var metadata = new GogGameMetadata();
            var gameDetail = apiClient.GetGameDetails(game.GameId);
            if (gameDetail == null)
            {
                logger.Warn($"Product page for game {game.GameId} not found, using fallback search.");
                var search = apiClient.GetStoreSearch(game.Name);
                var match = search?.FirstOrDefault(a => a.title.Equals(game.Name, StringComparison.InvariantCultureIgnoreCase));
                if (match != null)
                {
                    gameDetail = apiClient.GetGameDetails(match.id.ToString());
                }
            }

            metadata.GameDetails = gameDetail;

            if (gameDetail != null)
            {
                if (gameDetail.links.product_card != @"https://www.gog.com/" && !string.IsNullOrEmpty(gameDetail.links.product_card))
                {
                    metadata.StoreDetails = apiClient.GetGameStoreData(gameDetail.links.product_card);
                }

                metadata.Icon = new MetadataFile("http:" + gameDetail.images.icon);
                if (metadata.StoreDetails != null)
                {
                    var imageUrl = metadata.StoreDetails.image + "_product_card_v2_mobile_slider_639.jpg";
                    metadata.CoverImage = new MetadataFile(imageUrl);
                }
                else
                {
                    metadata.CoverImage = new MetadataFile("http:" + gameDetail.images.logo2x);
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
    }
}
