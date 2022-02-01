using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HumbleLibrary.Models
{
    public class Settings
    {
        public int throttleSpeed { get; set; }
        public string downloadLocation { get; set; }
        public string locale { get; set; }
        public List<string> notificationTypes { get; set; }
        public string updateChannel { get; set; }
        public object curtime { get; set; }
        public bool isMaximized { get; set; }
    }

    public class Downloads
    {
    }

    public class CarouselContent
    {
        public List<string> thumbnail { get; set; }
        public List<string> screenshot { get; set; }

        [SerializationPropertyName("asm-demo-machine-name")]
        public List<object> AsmDemoMachineName { get; set; }

        [SerializationPropertyName("youtube-link")]
        public List<string> YoutubeLink { get; set; }
    }

    public class Publisher
    {
        [SerializationPropertyName("publisher-name")]
        public string PublisherName { get; set; }

        [SerializationPropertyName("publisher-url")]
        public string PublisherUrl { get; set; }
    }

    public class Developer
    {
        [SerializationPropertyName("developer-name")]
        public string DeveloperName { get; set; }

        [SerializationPropertyName("developer-url")]
        public string DeveloperUrl { get; set; }
    }

    public class GameCollection4
    {
        public string machineName { get; set; }
        public string gameName { get; set; }
        public string imagePath { get; set; }
        public string status { get; set; }
        public Downloads downloads { get; set; }
        public int downloadPercentage { get; set; }
        public string downloadSpeed { get; set; }
        public string downloadEta { get; set; }
        public int? downloadedVersion { get; set; }
        public int latestBuildVersion { get; set; }
        public bool isPaused { get; set; }
        public object error { get; set; }
        public bool isAvailable { get; set; }
        public string lastPlayed { get; set; }
        public string downloadMachineName { get; set; }
        public object fileSize { get; set; }
        public long downloadFileSize { get; set; }
        public string descriptionText { get; set; }
        public CarouselContent carouselContent { get; set; }
        public List<Publisher> publishers { get; set; }
        public string youtubeLink { get; set; }
        public List<Developer> developers { get; set; }
        public int popularity { get; set; }
        public long dateAdded { get; set; }
        public string troveCategory { get; set; }
        public long? dateEnded { get; set; }
        public string downloadFilePath { get; set; }
        public string executablePath { get; set; }
        public long? downloadTotalBytes { get; set; }
        public List<object> dependencies { get; set; }
    }

    public class WindowBounds
    {
        public int x { get; set; }
        public int y { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }

    public class DownloadManager
    {
        public object activeDownload { get; set; }
        public List<object> downloadQueue { get; set; }
    }

    public class Homepage
    {
        public List<object> banners { get; set; }
    }

    public class HumbleAppConfig
    {
        public Settings settings { get; set; }
        public Homepage homepage { get; set; }

        [SerializationPropertyName("game-collection-4")]
        public List<GameCollection4> GameCollection4 { get; set; }

        [SerializationPropertyName("download-manager")]
        public DownloadManager DownloadManager { get; set; }
    }
}
