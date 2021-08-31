using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UplayLibrary
{
    public class UplayMetadataProvider : LibraryMetadataProvider
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly List<Models.ProductInformation> productInfo;

        public UplayMetadataProvider()
        {
            try
            {
                productInfo = Uplay.GetLocalProductCache();
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to get Uplay product info from cache.");
            }
        }

        public override GameMetadata GetMetadata(Game game)
        {
            var gameInfo = new GameMetadata
            {
                Links = new List<Link>()
            };

            gameInfo.Links.Add(new Link("PCGamingWiki", @"http://pcgamingwiki.com/w/index.php?search=" + gameInfo.Name));
            var prod = productInfo?.FirstOrDefault(a => a.uplay_id.ToString() == game.GameId);
            if (prod != null)
            {
                if (!prod.root.icon_image.IsNullOrEmpty())
                {
                    gameInfo.Icon = new MetadataFile(prod.root.icon_image);
                }

                if (!prod.root.thumb_image.IsNullOrEmpty())
                {
                    gameInfo.CoverImage = new MetadataFile(prod.root.thumb_image);
                }

                if (!prod.root.background_image.IsNullOrEmpty())
                {
                    gameInfo.BackgroundImage = new MetadataFile(prod.root.background_image);
                }
            }
            else
            {
                var program = Programs.GetUnistallProgramsList().FirstOrDefault(a => a.RegistryKeyName == "Uplay Install " + game.GameId);
                if (program != null)
                {
                    if (!string.IsNullOrEmpty(program.DisplayIcon) && File.Exists(program.DisplayIcon))
                    {
                        gameInfo.Icon = new MetadataFile(program.DisplayIcon);
                    }
                }
            }

            return gameInfo;
        }
    }
}
