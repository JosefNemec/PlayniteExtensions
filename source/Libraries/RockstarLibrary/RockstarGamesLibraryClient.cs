using Playnite.Common;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RockstarGamesLibrary
{
    public class RockstarGamesLibraryClient : LibraryClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override string Icon => RockstarGames.Icon;

        public override bool IsInstalled => RockstarGames.IsInstalled;

        public override void Open()
        {
            RockstarGames.StartClient();
        }

        public override void Shutdown()
        {
            // Signal the main one, not the web render process.
            var mainProc = Process.GetProcessesByName("SocialClubHelper").FirstOrDefault(a => !a.GetCommandLine().Contains("--type="));
            if (mainProc == null)
            {
                logger.Info("Rockstar client is no longer running, no need to shut it down.");
                return;
            }

            var procRes = ProcessStarter.StartProcessWait(CmdLineTools.TaskKill, $"/pid {mainProc.Id}", null, out var stdOut, out var stdErr);
            if (procRes != 0)
            {
                logger.Error($"Failed to close Rockstar client: {procRes}, {stdErr}");
            }
        }
    }
}