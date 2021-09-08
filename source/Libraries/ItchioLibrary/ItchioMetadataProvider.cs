using AngleSharp.Parser.Html;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ItchioLibrary
{
    public class ItchioMetadataProvider : LibraryMetadataProvider
    {
        private Butler butler;
        private IPlayniteAPI api;

        public ItchioMetadataProvider(IPlayniteAPI api)
        {
            butler = new Butler();
            this.api = api;
        }

        public override void Dispose()
        {
            butler.Dispose();
        }

        #region IMetadataProvider

        public override GameMetadata GetMetadata(Game game)
        {
            var tags = new HashSet<MetadataProperty>();
            var genres = new HashSet<MetadataProperty>();
            var features = new HashSet<MetadataProperty>();
            var gameData = new GameMetadata()
            {
                Links = new List<Link>(),
                Tags = tags,
                Genres = genres,
                Features = features
            };

            var itchGame = butler.GetGame(Convert.ToInt32(game.GameId));

            // Cover image
            if (!string.IsNullOrEmpty(itchGame.coverUrl))
            {
                gameData.CoverImage = new MetadataFile(itchGame.coverUrl);
            }

            if (!string.IsNullOrEmpty(itchGame.url))
            {
                gameData.Links.Add(new Link(
                    api.Resources.GetString("LOCCommonLinksStorePage"),
                    itchGame.url));
                var gamePageSrc = HttpDownloader.DownloadString(itchGame.url);
                var parser = new HtmlParser();
                var gamePage = parser.Parse(gamePageSrc);

                // Description
                gameData.Description = gamePage.QuerySelector(".formatted_description").InnerHtml;

                // Background
                var gameTheme = gamePage.QuerySelector("#game_theme").InnerHtml;
                var bckMatch = Regex.Match(gameTheme, @"background-image:\surl\((.+?)\)");
                if (bckMatch.Success)
                {
                    gameData.BackgroundImage = new MetadataFile(bckMatch.Groups[1].Value);
                }

                // Other info
                var infoPanel = gamePage.QuerySelector(".game_info_panel_widget");
                var fields = infoPanel.QuerySelectorAll("tr");
                gameData.Links.Add(new Link("PCGamingWiki", @"http://pcgamingwiki.com/w/index.php?search=" + game.Name));

                foreach (var field in fields)
                {
                    var name = field.QuerySelectorAll("td")[0].TextContent;
                    if (name == "Genre")
                    {
                        foreach (var item in field.QuerySelectorAll("a"))
                        {
                            tags.Add(new MetadataNameProperty(item.TextContent));
                        }

                        continue;
                    }

                    if (name == "Tags")
                    {
                        foreach (var item in field.QuerySelectorAll("a"))
                        {
                            if (item.TextContent == "Virtual Reality (VR)")
                            {
                                features.Add(new MetadataNameProperty("VR"));
                            }
                            else
                            {
                               tags.Add(new MetadataNameProperty(item.TextContent));
                            }
                        }

                        continue;
                    }

                    if (name == "Links")
                    {
                        foreach (var item in field.QuerySelectorAll("a"))
                        {
                            gameData.Links.Add(new Link(item.TextContent, item.Attributes["href"].Value));
                        }

                        continue;
                    }

                    if (name == "Author")
                    {
                        gameData.Developers = new HashSet<MetadataProperty> { new MetadataNameProperty(field.ChildNodes[1].TextContent) };
                    }

                    if (name == "Release date")
                    {
                        var strDate = field.QuerySelector("abbr").Attributes["title"].Value.Split('@')[0].Trim();
                        if (DateTime.TryParseExact(strDate, "d MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
                        {
                            gameData.ReleaseDate = new ReleaseDate(dateTime);
                        }
                    }
                }
            }

            return gameData;
        }

        #endregion IMetadataProvider
    }
}
