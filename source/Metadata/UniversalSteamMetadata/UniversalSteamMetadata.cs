using AngleSharp.Parser.Html;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Metadata;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Steam;
using Steam.Models;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Controls;

namespace UniversalSteamMetadata
{
    public class UniversalSteamMetadata : MetadataPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string searchUrl = @"https://store.steampowered.com/search/?term={0}&category1=998";

        private readonly string[] backgroundUrls = new string[]
        {
            @"https://steamcdn-a.akamaihd.net/steam/apps/{0}/page.bg.jpg",
            @"https://steamcdn-a.akamaihd.net/steam/apps/{0}/page_bg_generated.jpg"
        };

        internal UniversalSteamMetadataSettings Settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("f2db8fb1-4981-4dc4-b087-05c782215b72");

        public override List<MetadataField> SupportedFields { get; } = new List<MetadataField>
        {
            MetadataField.Description,
            MetadataField.BackgroundImage,
            MetadataField.CommunityScore,
            MetadataField.CoverImage,
            MetadataField.CriticScore,
            MetadataField.Developers,
            MetadataField.Genres,
            MetadataField.Icon,
            MetadataField.Links,
            MetadataField.Publishers,
            MetadataField.ReleaseDate,
            MetadataField.Features,
            MetadataField.Name
        };

        public override string Name => "Steam Store";

        public UniversalSteamMetadata(IPlayniteAPI api) : base(api)
        {
            Settings = new UniversalSteamMetadataSettings(this);
        }

        public override void Dispose()
        {
        }

        public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
        {
            return new UniversalSteamMetadataProvider(options, this);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new UniversalSteamMetadataSettingsView();
        }

        public static List<StoreSearchResult> GetSearchResults(string searchTerm)
        {
            var results = new List<StoreSearchResult>();
            using (var webClient = new WebClient { Encoding = Encoding.UTF8 })
            {
                var searchPageSrc = webClient.DownloadString(string.Format(searchUrl, searchTerm));
                var parser = new HtmlParser();
                var searchPage = parser.Parse(searchPageSrc);
                foreach (var gameElem in searchPage.QuerySelectorAll(".search_result_row"))
                {
                    var title = gameElem.QuerySelector(".title").InnerHtml;
                    var releaseDate = gameElem.QuerySelector(".search_released").InnerHtml;
                    if (gameElem.HasAttribute("data-ds-packageid"))
                    {
                        continue;
                    }

                    var gameId = gameElem.GetAttribute("data-ds-appid");
                    results.Add(new StoreSearchResult
                    {
                        Name = HttpUtility.HtmlDecode(title),
                        Description = HttpUtility.HtmlDecode(releaseDate),
                        GameId = uint.Parse(gameId)
                    });
                }
            }

            return results;
        }
    }
}