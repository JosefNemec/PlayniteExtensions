using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Igdb = PlayniteServices.IGDB;

namespace IGDBMetadata
{
    public class IgdbSearchContext : SearchContext
    {
        private readonly IgdbClient client;

        public IgdbSearchContext(IgdbClient client)
        {
            this.client = client;
            Delay = 500;
            Label = "IGDB";
        }

        public static string GetSearchItemName(Igdb.Game game)
        {
            var desc = game.name;
            if (game.first_release_date > 0)
            {
                desc += $" ({DateTimeOffset.FromUnixTimeSeconds(game.first_release_date).Year})";
            }

            return desc;
        }

        public static string GetSearchItemDescription(Igdb.Game game)
        {
            var desc = string.Empty;
#if DEBUG
            desc += $"{game.id} - ";
#endif

            var developers = game.involved_companies_expanded?.
                Where(a => a.developer && !a.supporting && !a.porting && a.company_expanded != null).
                Select(a => a.company_expanded.name).
                Distinct().
                ToList();
            if (developers.HasItems())
            {
                desc += string.Join(", ", developers!);
            }

            return desc;
        }

        public override IEnumerable<SearchItem> GetSearchResults(GetSearchResultsArgs args)
        {
            if (args.SearchTerm.IsNullOrWhiteSpace())
            {
                return null;
            }

            try
            {
                var result = new List<SearchItem>();
                foreach (var game in client.SearchGames(new Igdb.SearchRequest(args.SearchTerm)).GetAwaiter().GetResult())
                {
                    var item = new SearchItem(
                        GetSearchItemName(game),
                        new SearchItemAction(
                            "Open page",
                            () =>  Process.Start(game.url.IsNullOrWhiteSpace() ? @"https://www.igdb.com" : game.url!)));
                    item.Description = GetSearchItemDescription(game);
                    if (game.cover_expanded?.url.IsNullOrWhiteSpace() == false)
                    {
                        item.Icon = IgdbMetadataPlugin.GetImageUrl(game.cover_expanded.url, ImageSizes.thumb);
                    }

                    result.Add(item);
                }

                return result;
            }
            catch (Exception e)
            {
                return new List<SearchItem> { new SearchItem("Failed to search for games", null) { Description = e.Message } };
            }
        }
    }
}
