using IGDBMetadata.Models;
using IGDBMetadata.Services;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace IGDBMetadata
{
    [LoadPlugin]
    public class IgdbMetadataPlugin : MetadataPluginBase<IgdbMetadataSettingsViewModel>
    {
        public class IgdbImageOption : ImageFileOption
        {
            public PlayniteServices.Models.IGDB.GameImage Image { get; set; }
        }

        public readonly IgdbServiceClient Client;

        public IgdbMetadataPlugin(IPlayniteAPI api) : base(
            "IGDB",
            Guid.Parse("000001DB-DBD1-46C6-B5D0-B1BA559D10E4"),
            new List<MetadataField>
            {
                MetadataField.Name,
                MetadataField.Description,
                MetadataField.CoverImage,
                MetadataField.BackgroundImage,
                MetadataField.ReleaseDate,
                MetadataField.Developers,
                MetadataField.Publishers,
                MetadataField.Genres,
                MetadataField.Links,
                MetadataField.Features,
                MetadataField.CriticScore,
                MetadataField.CommunityScore,
                MetadataField.Series,
                MetadataField.AgeRating
            },
            () => new IgdbMetadataSettingsView(),
            null,
            api)
        {
            Properties = new MetadataPluginProperties { HasSettings = true };
            Client = new IgdbServiceClient(api.ApplicationInfo.ApplicationVersion);
            SettingsViewModel = new IgdbMetadataSettingsViewModel(this, api);
        }

        public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
        {
            return new IgdbLazyMetadataProvider(options, this);
        }

        internal static string GetImageUrl(PlayniteServices.Models.IGDB.GameImage image, string imageSize)
        {
            var url = image.url;
            if (!url.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
            {
                url = "https:" + url;
            }

            url = Regex.Replace(url, @"\/t_[^\/]+", "/t_" + imageSize);
            return url;
        }

        public List<SearchResult> GetSearchResults(string gameName)
        {
            var results = new List<SearchResult>();
            foreach (var game in Client.GetIGDBGames(gameName))
            {
                DateTime? releaseDate = null;
                string description = null;
                if (game.first_release_date != 0)
                {
                    releaseDate = DateTimeOffset.FromUnixTimeMilliseconds(game.first_release_date).DateTime;
                    description = $"({releaseDate.Value.Year})";
                }

                results.Add(new SearchResult(
                    game.id.ToString(),
                    game.name.RemoveTrademarks(),
                    releaseDate,
                    game.alternative_names?.Any() == true ? game.alternative_names.Select(name => name.name.RemoveTrademarks()).ToList() : null,
                    description));
            }

            return results;
        }
    }
}
