#nullable enable
using System.Collections.Generic;

namespace SteamLibrary.Models
{
    // TODO move to playnite backend repo
    public class BackendSteamDbItemsRequest
    {
        public List<ulong>? AppIds { get; set; }
    }

    /// <summary>
    /// Playnite backend metadata for steam app
    /// </summary>
    public class BackendAppInfo
    {
        private string? type;
        public uint AppId { get; set; }

        public string? Name
        {
            get => name ?? $"Unknown App {AppId}";
            set => name = value;
        }

        /// <summary>
        /// Normalized to lowercase. Original metadata has "Game" and "game" values
        /// </summary>
        public string? Type
        {
            get => type?.ToLowerInvariant() ?? "unknown";
            set => type = value;
        }

        public bool IsGame => Type == "game";
        public bool IsFree => Type == "demo" || Type == "beta";
        public bool IsApp => Type == "application";
        public bool IsMedia => Type == "music" || Type == "video";
        public bool IsTool => Type == "tool";
        public bool IsUseless => Type == "config" || Type == "dlc" || Type == "unknown";

        public Dictionary<string, string>? LocalizedNames { get; set; }

        public void LocalizeName(string lang)
        {
            if (LocalizedNames?.TryGetValue(lang, out var appName) == true && !string.IsNullOrWhiteSpace(appName))
            {
                Name = appName;
            }
        }

        private string? name;
    }
}
