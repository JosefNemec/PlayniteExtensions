using Newtonsoft.Json;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SteamLibrary.SteamShared
{
    public class SteamTagNamer
    {
        public static Dictionary<string, string> Languages = new Dictionary<string, string>
        {
            {"schinese","简体中文 (Simplified Chinese)"},
            {"tchinese","繁體中文 (Traditional Chinese)"},
            {"japanese","日本語 (Japanese)"},
            {"koreana","한국어 (Korean)"},
            {"thai","ไทย (Thai)"},
            {"bulgarian","Български (Bulgarian)"},
            {"czech","Čeština (Czech)"},
            {"danish","Dansk (Danish)"},
            {"german","Deutsch (German)"},
            {"english","English"},
            {"spanish","Español - España (Spanish - Spain)"},
            {"latam","Español - Latinoamérica (Spanish - Latin America)"},
            {"greek","Ελληνικά (Greek)"},
            {"french","Français (French)"},
            {"italian","Italiano (Italian)"},
            {"hungarian","Magyar (Hungarian)"},
            {"dutch","Nederlands (Dutch)"},
            {"norwegian","Norsk (Norwegian)"},
            {"polish","Polski (Polish)"},
            {"portuguese","Português (Portuguese)"},
            {"brazilian","Português - Brasil (Portuguese - Brazil)"},
            {"romanian","Română (Romanian)"},
            {"russian","Русский (Russian)"},
            {"finnish","Suomi (Finnish)"},
            {"swedish","Svenska (Swedish)"},
            {"turkish","Türkçe (Turkish)"},
            {"vietnamese","Tiếng Việt (Vietnamese)"},
            {"ukrainian","Українська (Ukrainian)"},
        };

        private readonly string pluginUserDataPath;
        private readonly string languageKey;
        private readonly IDownloader downloader;
        private ILogger logger = LogManager.GetLogger();

        public SteamTagNamer(string pluginUserDataPath, string languageKey, IDownloader downloader)
        {
            this.pluginUserDataPath = pluginUserDataPath;
            this.languageKey = languageKey;
            this.downloader = downloader;
        }

        private string GetTagNameFilePath()
        {
            return $@"{pluginUserDataPath}\TagLocalization\{languageKey}.json";
        }

        private string GetPackedTagNameFilePath()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return $@"{dir}\SteamShared\TagLocalization\{languageKey}.json";
        }

        public Dictionary<int, string> GetTagNames()
        {
            string fileName = GetTagNameFilePath();
            if (!File.Exists(fileName))
            {
                logger.Trace($"No new tag names found for '{languageKey}'");
                fileName = GetPackedTagNameFilePath();
            }

            if (!File.Exists(fileName))
            {
                logger.Warn($"No tag names found for language '{languageKey}'");
                return new Dictionary<int, string>();
            }

            string fileContent = File.ReadAllText(fileName);
            var tagNames = JsonConvert.DeserializeObject<Dictionary<int, string>>(fileContent);
            return tagNames;
        }

        private static Regex tagNameRegex = new Regex(@"data-param=""tags"" data-value=""(?<id>[0-9]+)"" data-loc=""(?<name>.+?)""", RegexOptions.Compiled);

        public Dictionary<int, string> UpdateAndGetTagNames()
        {
            string url = "https://store.steampowered.com/search/?l=" + languageKey;
            var content = downloader.DownloadString(url);
            var matches = tagNameRegex.Matches(content);

            var output = new Dictionary<int, string>();
            foreach (var match in matches.Cast<Match>())
            {
                int id = int.Parse(match.Groups["id"].Value);
                if (!output.ContainsKey(id))
                {
                    string name = match.Groups["name"].Value;
                    output.Add(id, name);
                }
            }

            string serialized = JsonConvert.SerializeObject(output);
            File.WriteAllText(GetTagNameFilePath(), serialized, Encoding.Unicode);

            return output;
        }
    }
}
