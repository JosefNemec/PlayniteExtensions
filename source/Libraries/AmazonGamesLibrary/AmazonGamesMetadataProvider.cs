using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmazonGamesLibrary
{
    public class AmazonGamesMetadataProvider : LibraryMetadataProvider
    {
        public override GameMetadata GetMetadata(Game game)
        {
            var gameInfo = new GameMetadata
            {
                Links = new List<Link>()
            };

            gameInfo.Links.Add(new Link("PCGamingWiki", @"http://pcgamingwiki.com/w/index.php?search=" + game.Name));

            var program = AmazonGames.GetUninstallRecord(game.GameId);
            if (program != null)
            {
                gameInfo.Name = StringExtensions.NormalizeGameName(program.DisplayName);
                if (!string.IsNullOrEmpty(program.DisplayIcon) && File.Exists(program.DisplayIcon))
                {
                    gameInfo.Icon = new MetadataFile(program.DisplayIcon);
                }
            }

            return gameInfo;
        }
    }
}
