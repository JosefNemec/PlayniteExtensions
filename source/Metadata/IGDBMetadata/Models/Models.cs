using Playnite.SDK;
using Playnite.Backend.IGDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IGDBMetadata
{
    public class IgdbImageSlectItem : ImageFileOption
    {
        public readonly IIgdbItem Image;

        public IgdbImageSlectItem(Artwork artwork)
        {
            Image = artwork;
            Path = IgdbMetadataPlugin.GetImageUrl(artwork.url, ImageSizes.screenshot_med);
        }

        public IgdbImageSlectItem(Screenshot screenshot)
        {
            Image = screenshot;
            Path = IgdbMetadataPlugin.GetImageUrl(screenshot.url, ImageSizes.screenshot_med);
        }
    }

    public class IgdbGameSelectItem : GenericItemOption
    {
        public readonly Game Game;

        public IgdbGameSelectItem(Game game)
        {
            Game = game;
            Name = IgdbSearchContext.GetSearchItemName(game);
            Description = IgdbSearchContext.GetSearchItemDescription(game);
        }
    }
}
