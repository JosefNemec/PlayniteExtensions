using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace GogLibrary
{
    public class Gog
    {
        public const string EnStoreLocaleString = "US_USD_en-US";

        public static string ClientExecPath
        {
            get
            {
                var path = InstallationPath;
                return string.IsNullOrEmpty(path) ? string.Empty : Path.Combine(path, "GalaxyClient.exe");
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

        public static string InstallationPath
        {
            get
            {
                RegistryKey key = null;
                try
                {
                    key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths");
                    if (key == null)
                    {
                        key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\GalaxyClient\paths");
                    }

                    if (key?.GetValueNames().Contains("client") == true)
                    {
                        return key.GetValue("client").ToString();
                    }

                    return string.Empty;
                }
                finally
                {
                    key?.Dispose();
                }
            }
        }

        public static bool IsRunning
        {
            get
            {
                // The Notifications Renderer process is used because other Galaxy related process can
                // be running in the background without the client itself being open for the user
                return Process.GetProcessesByName("GOG Galaxy Notifications Renderer")?.Any() == true;
            }
        }

        public static string ClientInstallationPath => Path.Combine(InstallationPath, "GalaxyClient.exe");
        public static string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Resources\gogicon.png");
    }
}
