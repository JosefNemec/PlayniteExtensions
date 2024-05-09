﻿using AmazonGamesLibrary.Models;
using Playnite.Common;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AmazonGamesLibrary
{
    public class AmazonGames
    {
        public static bool IsRunning => Process.GetProcessesByName("Amazon Games").Length > 0;

        public static string ClientExecPath
        {
            get
            {
                var path = InstallationPath;
                return string.IsNullOrEmpty(path) ? string.Empty : Path.Combine(path, "Amazon Games.exe");
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
                var program = Programs.GetUnistallProgramsList().
                    FirstOrDefault(a => a.DisplayName == "Amazon Games" && a.UninstallString?.Contains("Uninstall Amazon Games.exe") == true);
                if (program == null)
                {
                    return null;
                }

                return program.InstallLocation;
            }
        }

        public static string Icon => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");

        public static void StartClient()
        {
            ProcessStarter.StartProcess(ClientExecPath, string.Empty);
        }
        public static GameConfiguration GetGameConfiguration(string gameDir)
        {
            var configFile = Path.Combine(gameDir, GameConfiguration.ConfigFileName);
            if (File.Exists(configFile))
            {
                return Serialization.FromJsonFile<GameConfiguration>(configFile);
            }

            return null;
        }

        public static bool GetGameRequiresClient(GameConfiguration config)
        {
            return !config.Main.ClientId.IsNullOrEmpty() &&
                    config.Main.AuthScopes.HasItems();
        }
    }
}
