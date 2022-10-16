using Microsoft.Win32;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OriginLibrary
{
    public class Origin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        public static readonly string LibraryOpenUri = "origin2://library/open";

        public static bool IsRunning
        {
            get
            {
                return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ClientExecPath))?.Any() == true;
            }
        }

        public static string ClientExecPath
        {
            get
            {
                using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var key = root.OpenSubKey(@"SOFTWARE\Origin"))
                {
                    var values = key?.GetValueNames();
                    if (values?.Contains("OriginPath") == true)
                    {
                        return key.GetValue("OriginPath").ToString();
                    }
                    else if (values?.Contains("ClientPath") == true)
                    {
                        return key.GetValue("ClientPath").ToString();
                    }
                }

                return string.Empty;
            }
        }

        public static string InstallationPath
        {
            get
            {
                var path = ClientExecPath;
                if (!string.IsNullOrEmpty(path))
                {
                    return Path.GetDirectoryName(path);
                }

                return string.Empty;
            }
        }

        public static bool IsInstalled
        {
            get
            {
                if (string.IsNullOrEmpty(ClientExecPath) || !File.Exists(ClientExecPath))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        public static string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"Resources\originicon.png");

        public static void StartClient()
        {
            ProcessStarter.StartProcess(ClientExecPath, string.Empty);
        }

        public static bool GetGameRequiresOrigin(string installDir)
        {
            if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
            {
                return false;
            }

            var fileEnumerator = new SafeFileEnumerator(installDir, "Activation.*", SearchOption.AllDirectories);
            return fileEnumerator.Any() == true;
        }

        public static bool GetGameUsesEasyAntiCheat(string installDir)
        {
            if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
            {
                return false;
            }

            var fileEnumerator = new SafeFileEnumerator(installDir, "EasyAntiCheat*.dll", SearchOption.AllDirectories);
            return fileEnumerator.Any() == true;
        }
    }
}
