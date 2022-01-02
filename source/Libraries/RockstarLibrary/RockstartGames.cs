using Playnite.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RockstarGamesLibrary
{
    public class RockstarGame
    {
        public string Name { get; set; }
        public string Executable { get; set; }
        public string TitleId { get; set; }
    }

    public class RockstarGames
    {
        public static readonly List<RockstarGame> Games = new List<RockstarGame>
        {
            new RockstarGame
            {
                Name = "Grand Theft Auto V",
                Executable = "PlayGTAV.exe",
                TitleId = "gta5"
            },
            new RockstarGame
            {
                Name = "Red Dead Redemption 2",
                Executable = "RDR2.exe",
                TitleId = "rdr2"
            },
            new RockstarGame
            {
                Name = "L.A. Noire",
                Executable = "LANoire.exe",
                TitleId = "lanoire"
            },
            new RockstarGame
            {
                Name = "Max Payne 3",
                Executable = "MaxPayne3.exe",
                TitleId = "mp3"
            },
            new RockstarGame
            {
                Name = "L.A. Noire: The VR Case Files",
                Executable = "LANoireVR.exe",
                TitleId = "lanoirevr"
            },
            new RockstarGame
            {
                Name = "Grand Theft Auto: San Andreas",
                Executable = "gta_sa.exe",
                TitleId = "gtasa"
            },
            new RockstarGame
            {
                Name = "Grand Theft Auto III",
                Executable = "gta3.exe",
                TitleId = "gta3"
            },
            new RockstarGame
            {
                Name = "Grand Theft Auto: Vice City",
                Executable = "gta-vc.exe",
                TitleId = "gtavc"
            },
            new RockstarGame
            {
                Name = "Bully: Scholarship Edition",
                Executable = "Bully.exe",
                TitleId = "bully"
            },
            new RockstarGame
            {
                Name = "Grand Theft Auto IV",
                Executable = "GTAIV.exe",
                TitleId = "gta4"
            },
            new RockstarGame
            {
                Name = "Grand Theft Auto III: The Definitive Edition",
                Executable = "Gameface/Binaries/Win64/LibertyCity.exe",
                TitleId = "gta3unreal"
            },
            new RockstarGame
            {
                Name = "Grand Theft Auto: Vice City – The Definitive Edition",
                Executable = "Gameface/Binaries/Win64/ViceCity.exe",
                TitleId = "gtavcunreal"
            },
            new RockstarGame
            {
                Name = "Grand Theft Auto: San Andreas – The Definitive Edition",
                Executable = "Gameface/Binaries/Win64/SanAndreas.exe",
                TitleId = "gtasaunreal"
            }
        };

        public static bool IsRunning
        {
            get
            {
                return RunningProcessesCount > 0;
            }
        }

        public static int RunningProcessesCount
        {
            get
            {
                var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ClientExecPath));
                return processes?.Any() == true ? processes.Count() : 0;
            }
        }

        public static string ClientExecPath
        {
            get
            {
                var path = InstallationPath;
                return string.IsNullOrEmpty(path) ? string.Empty : Path.Combine(path, "Launcher.exe");
            }
        }

        public static string InstallationPath
        {
            get
            {
                var progs = Programs.GetUnistallProgramsList().FirstOrDefault(a => a.DisplayName == "Rockstar Games Launcher" == true);
                if (progs == null)
                {
                    return string.Empty;
                }
                else
                {
                    return progs.InstallLocation.Trim(new char[] { '"' });
                }
            }
        }

        public static bool IsInstalled
        {
            get
            {
                var clientExe = ClientExecPath;
                return !string.IsNullOrEmpty(clientExe) && File.Exists(clientExe);
            }
        }

        public static string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");

        public static void StartClient()
        {
            ProcessStarter.StartProcess(ClientExecPath, string.Empty);
        }
    }
}
