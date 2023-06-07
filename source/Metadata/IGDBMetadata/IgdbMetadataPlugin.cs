using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IGDBMetadata
{
    public class Configuration
    {
        public string BackendEndpoint { get; set; }
    }

    [LoadPlugin]
    public class IgdbMetadataPlugin : MetadataPluginBase<IgdbMetadataSettingsViewModel>
    {
        public readonly IgdbClient Client;
        private readonly Configuration pluginConfig;

        public IgdbMetadataPlugin(IPlayniteAPI api) : base(
            "IGDB",
            Guid.Parse("000001DB-DBD1-46C6-B5D0-B1BA559D10E4"),
            new List<MetadataField>
            {
                MetadataField.Name,
                MetadataField.Description,
                MetadataField.Icon,
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
            pluginConfig = GetPluginConfiguration<Configuration>();
            Client = new IgdbClient(pluginConfig.BackendEndpoint);
            Properties = new MetadataPluginProperties { HasSettings = true };
            SettingsViewModel = new IgdbMetadataSettingsViewModel(this, api);
            Searches = new List<SearchSupport> { new SearchSupport("i", "IGDB", new IgdbSearchContext(Client)) };
        }

        public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
        {
            return new IgdbLazyMetadataProvider(options, this);
        }

        internal static string GetImageUrl(string url, string imageSize)
        {
            if (!url.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
            {
                url = "https:" + url;
            }

            url = Regex.Replace(url, @"\/t_[^\/]+", "/t_" + imageSize);
            return url;
        }
    }
}
