using Microsoft.Win32;
using Playnite.Common;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HumbleLibrary
{
    public class HumbleClient : LibraryClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static string humbleExeName = "Humble App.exe";
        public static string HumbleConfigPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Humble App", "config.json");

        public override string Icon => GetIcon();
        public override bool IsInstalled => GetIsClientInstalled();

        public override void Open()
        {
            if (IsInstalled)
            {
                ProcessStarter.StartProcess(Path.Combine(GetClientInstallPath(), humbleExeName));
            }
            else
            {
                throw new Exception("Humble App installation not found.");
            }
        }

        public override void Shutdown()
        {
            // Humble doesn't react properly to termination signal, so waiting for them to add support for external graceful shutdown.
            throw new NotImplementedException();
        }

        public static string GetIcon()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Resources\icon.png");
        }

        public static string GetClientInstallPath()
        {
            string getInstallPath(RegistryView view)
            {
                string path = null;
                using (var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view))
                using (var installKey = root.OpenSubKey(@"SOFTWARE\2f793df2-2969-529d-b0c0-7960ed40d70e"))
                {
                    path = installKey?.GetValue("InstallLocation")?.ToString();
                }

                if (path.IsNullOrEmpty())
                {
                    using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (var installKey = root.OpenSubKey(@"SOFTWARE\2f793df2-2969-529d-b0c0-7960ed40d70e"))
                    {
                        path = installKey?.GetValue("InstallLocation")?.ToString();
                    }
                }

                return path;
            }

            var installPath = getInstallPath(RegistryView.Registry32);
            if (installPath.IsNullOrEmpty())
            {
                installPath = getInstallPath(RegistryView.Registry64);
            }

            return installPath;
        }

        public static bool GetIsClientInstalled()
        {
            var installDir = GetClientInstallPath();
            if (installDir.IsNullOrEmpty())
            {
                return false;
            }
            else
            {
                return File.Exists(Path.Combine(installDir, humbleExeName));
            }
        }

        public static Version GetClientVersion()
        {
            if (GetIsClientInstalled())
            {
                var exePath = Path.Combine(GetClientInstallPath(), humbleExeName);
                if (Version.TryParse(FileVersionInfo.GetVersionInfo(exePath).ProductVersion, out var version))
                {
                    return version;
                }
            }

            return default;
        }
    }
}