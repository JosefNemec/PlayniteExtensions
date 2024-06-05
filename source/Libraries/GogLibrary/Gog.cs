using AngleSharp;
using AngleSharp.Dom.Html;
using Microsoft.Win32;
using Playnite.Common.Web;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

        public static string GetLoginUrl()
        {
            var clientId = GetClientIdFromMainScript(GetMainScriptUrl());
            return $"https://login.gog.com/auth?client_id={clientId}&response_type=code&redirect_uri=https%3A%2F%2Fwww.gog.com%2Fon_login_success%3FreturnTo%3D%2Fen%2F";
        }

        private static string GetMainScriptUrl()
        {
            var context = BrowsingContext.New(Configuration.Default);
            var doc = context.OpenAsync("https://www.gog.com/").Result;
            var scriptElement = doc.QuerySelector("script[src^=\"https://store-static-modular.gog-statics.com/en/main.\"]");
            if (scriptElement == null)
            {
                throw new Exception("Script element not found on GOG main page");
            }
            return scriptElement.GetAttribute("src");
        }

        private static string GetClientIdFromMainScript(string mainScriptUrl)
        {
            var mainScript = HttpDownloader.DownloadString(mainScriptUrl);
            Regex clientIdRegex = new Regex(@"\bclient_id=(?<id>[0-9]+)", RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
            var match = clientIdRegex.Match(mainScript);
            if (!match.Success)
            {
                throw new Exception("No client ID found in main script");
            }
            return match.Groups["id"].Value;
        }
    }
}
