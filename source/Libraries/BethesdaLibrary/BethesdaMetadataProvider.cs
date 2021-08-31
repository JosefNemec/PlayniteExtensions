using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BethesdaLibrary
{
    public class BethesdaMetadataProvider : LibraryMetadataProvider
    {
        public override GameMetadata GetMetadata(Game game)
        {
            var program = Bethesda.GetBethesdaInstallEntried().FirstOrDefault(a => a.UninstallString?.Contains($"uninstall/{game.GameId}") == true);
            if (program == null)
            {
                return null;
            }

            var gameInfo = new GameMetadata
            {
                Name = StringExtensions.NormalizeGameName(program.DisplayName),
                Links = new List<Link>(),
            };

            gameInfo.Links.Add(new Link("PCGamingWiki", @"http://pcgamingwiki.com/w/index.php?search=" + gameInfo.Name));
            if (!string.IsNullOrEmpty(program.DisplayIcon) && File.Exists(program.DisplayIcon))
            {
                gameInfo.Icon = new MetadataFile(program.DisplayIcon);
            }

            return gameInfo;
        }
    }
}
