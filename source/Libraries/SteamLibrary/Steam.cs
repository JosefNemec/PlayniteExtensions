using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SteamLibrary
{
    public class Steam
    {
        public static string LoginUsersPath
        {
            get => Path.Combine(InstallationPath, "config", "loginusers.vdf");
        }

        public static string ClientExecPath
        {
            get
            {
                var path = InstallationPath;
                return string.IsNullOrEmpty(path) ? string.Empty : Path.Combine(path, "steam.exe");
            }
        }

        public static string InstallationPath
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key?.GetValueNames().Contains("SteamPath") == true)
                    {
                        return key.GetValue("SteamPath")?.ToString().Replace('/', '\\') ?? string.Empty;
                    }
                }

                return string.Empty;
            }
        }

        public static string ModInstallPath
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key?.GetValueNames().Contains("ModInstallPath") == true)
                    {
                        return key.GetValue("ModInstallPath")?.ToString().Replace('/', '\\') ?? string.Empty;
                    }
                }

                return string.Empty;
            }
        }

        public static string SourceModInstallPath
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key?.GetValueNames().Contains("SourceModInstallPath") == true)
                    {
                        return key.GetValue("SourceModInstallPath")?.ToString().Replace('/', '\\') ?? string.Empty;
                    }
                }

                return string.Empty;
            }
        }

        public static bool IsInstalled
        {
            get
            {
                if (string.IsNullOrEmpty(InstallationPath) || !Directory.Exists(InstallationPath))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        public static string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Resources\steamicon.png");
    }
}
