using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Playnite;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace GogLibrary
{
    public class GogInstallController : InstallController
    {
        private CancellationTokenSource watcherToken;
        private readonly GogLibrary gogLibrary;
        private readonly string openGameViewUri;
        private readonly string gogGalaxyLockFilesPath;
        private FileSystemWatcher fileSystemWatcher;

        public GogInstallController(Game game, GogLibrary gogLibrary) : base(game)
        {
            this.gogLibrary = gogLibrary;
            openGameViewUri = @"goggalaxy://openGameView/" + Game.GameId;
            var programDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);
            gogGalaxyLockFilesPath = Path.Combine(programDataPath, @"GOG.com\Galaxy\lock-files");
            if (Gog.IsInstalled)
            {
                Name = "Install using Galaxy";
            }
            else
            {
                Name = "Dowload offline installer";
            }
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
            if (fileSystemWatcher != null)
            {
                fileSystemWatcher.EnableRaisingEvents = false;
                fileSystemWatcher.Changed -= OnFileChanged;
                fileSystemWatcher.Created -= OnFileChanged;
                fileSystemWatcher.Renamed -= OnFileChanged;
                fileSystemWatcher.Dispose();
                fileSystemWatcher = null;
            }
        }

        public override void Install(InstallActionArgs args)
        {
            if (HandleExtras())
            {
                return;
            }

            InitiateInstall();
            StartInstallWatcher();
        }

        private bool HandleExtras()
        {
            if (!Game.GameId.StartsWith(GogLibrary.ExtrasPrefix))
            {
                return false;
            }

            var url = Serialization.FromJsonFile<Dictionary<string,string>>(gogLibrary.ExtrasFile)[Game.GameId];
            ProcessStarter.StartUrl(url);
            InvokeOnInstallationCancelled(new GameInstallationCancelledEventArgs());
            return true;
        }

        private void InitiateInstall()
        {
            if (!Gog.IsInstalled)
            {
                ProcessStarter.StartUrl(@"https://www.gog.com/account");
                return;
            }

            if (!gogLibrary.SettingsViewModel.Settings.UseAutomaticGameInstalls)
            {
                ProcessStarter.StartUrl(openGameViewUri);
                return;
            }

            var clientPath = Gog.ClientInstallationPath;
            if (!FileSystem.FileExists(clientPath))
            {
                ProcessStarter.StartUrl(openGameViewUri);
                return;
            }

            if (Gog.IsRunning)
            {
                InstallGameWithCommand();
            }
            else if (FileSystem.DirectoryExists(gogGalaxyLockFilesPath))
            {
                // Install command only works when Galaxy core components are initialized. This can be detected when Galaxy
                // has created lock files in its program data directory
                InitializeFileSystemWatcher();
                ProcessStarter.StartProcess(clientPath);
            }
            else
            {
                ProcessStarter.StartUrl(openGameViewUri);
            }
        }

        private async void InstallGameWithCommand()
        {
            var clientPath = Gog.ClientInstallationPath;
            var arguments = string.Format(@"/gameId={0} /command=installGame", Game.GameId);
            ProcessStarter.StartProcess(clientPath, arguments);

            // The GOG Galaxy client can't handle two instructions in quick succession
            await Task.Delay(2500);
            ProcessStarter.StartUrl(openGameViewUri);
        }

        private void InitializeFileSystemWatcher()
        {
            fileSystemWatcher = new FileSystemWatcher(gogGalaxyLockFilesPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                Filter = "*.*",
                EnableRaisingEvents = true
            };

            fileSystemWatcher.Changed += OnFileChanged;
            fileSystemWatcher.Created += OnFileChanged;
            fileSystemWatcher.Renamed += OnFileChanged;
        }

        private void DisableFileSystemWatcher()
        {
            if (fileSystemWatcher != null)
            {
                fileSystemWatcher.EnableRaisingEvents = false;
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (e.Name == "GOG Galaxy Notifications Renderer.exe.lock")
            {
                DisableFileSystemWatcher();
                InstallGameWithCommand();
            }
        }

        public async void StartInstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            await Task.Run(async () =>
            {
                while (true)
                {
                    if (watcherToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var games = GogLibrary.GetInstalledGames();
                    if (games.ContainsKey(Game.GameId))
                    {
                        DisableFileSystemWatcher();
                        var game = games[Game.GameId];
                        var installInfo = new GameInstallationData()
                        {
                            InstallDirectory = game.InstallDirectory
                        };

                        InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
                        return;
                    }

                    await Task.Delay(10000);
                }
            });
        }
    }

    public class GogUninstallController : UninstallController
    {
        private CancellationTokenSource watcherToken;

        public GogUninstallController(Game game) : base(game)
        {
            Name = "Uninstall";
        }

        public override void Dispose()
        {
            watcherToken?.Cancel();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            Dispose();
            var uninstaller = Path.Combine(Game.InstallDirectory, "unins000.exe");
            if (!File.Exists(uninstaller))
            {
                throw new FileNotFoundException("Uninstaller not found.");
            }

            Process.Start(uninstaller);
            StartUninstallWatcher();
        }

        public async void StartUninstallWatcher()
        {
            watcherToken = new CancellationTokenSource();
            while (true)
            {
                if (watcherToken.IsCancellationRequested)
                {
                    return;
                }

                var games = GogLibrary.GetInstalledGames();
                if (!games.ContainsKey(Game.GameId))
                {
                    InvokeOnUninstalled(new GameUninstalledEventArgs());
                    return;
                }

                await Task.Delay(5000);
            }
        }
    }
}
