using Newtonsoft.Json;
using Playnite.SDK;
using Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UniversalSteamMetadata
{
    public class UniversalSteamMetadataSettings : ISettings
    {
        private readonly UniversalSteamMetadata plugin;
        private UniversalSteamMetadataSettings editingClone;

        public bool DownloadVerticalCovers { get; set; } = true;

        public BackgroundSource BackgroundSource { get; set; } = BackgroundSource.Image;

        public UniversalSteamMetadataSettings()
        {
        }

        public UniversalSteamMetadataSettings(UniversalSteamMetadata plugin)
        {
            this.plugin = plugin;
            var savedSettings = plugin.LoadPluginSettings<UniversalSteamMetadataSettings>();

            if (savedSettings != null)
            {
                DownloadVerticalCovers = savedSettings.DownloadVerticalCovers;
                BackgroundSource = savedSettings.BackgroundSource;
            }
        }

        public void BeginEdit()
        {
            editingClone = this.GetClone();
        }

        public void CancelEdit()
        {
            LoadValues(editingClone);
        }

        private void LoadValues(UniversalSteamMetadataSettings source)
        {
            source.CopyProperties(this, false, null, true);
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}