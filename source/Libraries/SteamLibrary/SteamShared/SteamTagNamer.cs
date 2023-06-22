using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SteamLibrary.SteamShared
{
    public class SteamTagNamer
    {
        private static string extensionFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private readonly Plugin plugin;
        private readonly SharedSteamSettings settings;
        private readonly IDownloader downloader;
        private ILogger logger = LogManager.GetLogger();

        public SteamTagNamer(Plugin plugin, SharedSteamSettings settings, IDownloader downloader)
        {
            this.plugin = plugin;
            this.settings = settings;
            this.downloader = downloader;
        }

        private string GetTagNameFilePath()
        {
            return $@"{plugin.GetPluginUserDataPath()}\tagnames-{settings?.LanguageKey}.json";
        }

        private string GetPackedTagNameFilePath()
        {
            return $@"{extensionFolder}\SteamShared\TagLocalization\{settings?.LanguageKey}.json";
        }

        public Dictionary<int, string> GetTagNames()
        {
            string fileName = GetTagNameFilePath();
            if (!File.Exists(fileName))
            {
                logger.Trace($"No new tag names found for '{settings?.LanguageKey}'");
                fileName = GetPackedTagNameFilePath();

                if (!File.Exists(fileName))
                {
                    logger.Warn($"No tag names found for language '{settings?.LanguageKey}'");
                    return new Dictionary<int, string>();
                }
            }
            var content = GetFileContents(fileName);

            return content.response.tags.ToDictionary(x => x.tagid, x => x.name);
        }

        private SteamTagFile GetFileContents()
        {
            return GetFileContents(GetTagNameFilePath()) ?? GetFileContents(GetPackedTagNameFilePath());
        }

        private SteamTagFile GetFileContents(string path)
        {
            if (!File.Exists(path))
                return null;

            logger.Debug("Opening " + path);
            try
            {
                var content = File.ReadAllText(path, Encoding.UTF8);
                return Serialization.FromJson<SteamTagFile>(content);
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to read " + path);
                throw;
            }
        }

        public Dictionary<int, string> UpdateAndGetTagNames()
        {
            var existingFile = GetFileContents();

            string url = $"https://api.steampowered.com/IStoreService/GetTagList/v1?language={settings?.LanguageKey}&have_version_hash={existingFile?.response?.version_hash}";

            logger.Debug("Downloading " + url);
            var content = downloader.DownloadString(url);

            if (!string.IsNullOrWhiteSpace(content))
            {
                File.WriteAllText(GetTagNameFilePath(), content, Encoding.UTF8);
            }

            var tagFile = GetFileContents();
            return tagFile.response.tags.ToDictionary(x => x.tagid, x => x.name);
        }

        public string GetFinalTagName(string tagName)
        {
            tagName = HttpUtility.HtmlDecode(tagName).Trim();
            string computedTagName = settings.UseTagPrefix ? $"{settings.TagPrefix}{tagName}" : tagName;
            return computedTagName;
        }
    }

    public class SteamTagFile
    {
        public SteamTagResponse response { get; set; } = new SteamTagResponse();
    }

    public class SteamTagResponse
    {
        public string version_hash { get; set; }
        public List<SteamTag> tags { get; set; } = new List<SteamTag>();
    }

    public class SteamTag
    {
        public int tagid { get; set; }
        public string name { get; set; }
    }
}
