using AngleSharp.Parser.Html;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using Steam.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Web;

namespace UniversalSteamMetadata
{
    [LoadPlugin]
    public class UniversalSteamMetadata : MetadataPluginBase<UniversalSteamMetadataSettingsViewModel>
    {
        private const string searchUrl = @"https://store.steampowered.com/search/?term={0}&ignore_preferences=1&category1=998";
        private readonly string[] backgroundUrls = new string[]
        {
            @"https://steamcdn-a.akamaihd.net/steam/apps/{0}/page.bg.jpg",
            @"https://steamcdn-a.akamaihd.net/steam/apps/{0}/page_bg_generated.jpg"
        };
        private IDownloader downloader;

        public UniversalSteamMetadata(IPlayniteAPI api) : base(
            "Steam Store",
            Guid.Parse("f2db8fb1-4981-4dc4-b087-05c782215b72"),
            new List<MetadataField>
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
                MetadataField.Name,
                MetadataField.Platform,
                MetadataField.Tags,
            },
            () => new UniversalSteamMetadataSettingsView(),
            null,
            api)
        {
            Properties = new MetadataPluginProperties { HasSettings = true };
            SettingsViewModel = new UniversalSteamMetadataSettingsViewModel(this, api);
            downloader = new Downloader();
        }

        public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
        {
            return new UniversalSteamMetadataProvider(options, this, downloader);
        }

        public static List<StoreSearchResult> GetSearchResults(string searchTerm)
        {
            var results = new List<StoreSearchResult>();
            using (var webClient = new WebClient { Encoding = Encoding.UTF8 })
            {
                var searchPageSrc = webClient.DownloadString(string.Format(searchUrl, Uri.EscapeDataString(searchTerm)));
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