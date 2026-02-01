using Playnite.Common;
using Playnite.Common.Web;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteExtensions.Common;
using Steam;
using Steam.Models;
using SteamLibrary.SteamShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace UniversalSteamMetadata
{
    public class UniversalSteamMetadataProvider : OnDemandMetadataProvider
    {
        public class SteamImageOption : ImageFileOption
        {
            public string Image { get; set; }
        }

        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly MetadataRequestOptions options;
        private readonly UniversalSteamMetadata plugin;
        private readonly IDownloader downloader;
        private SteamGameMetadata currentMetadata;
        private readonly SteamApiClient apiClient;
        private readonly WebApiClient webApiClient;

        public override List<MetadataField> AvailableFields { get; } = new List<MetadataField>
        {
            MetadataField.Description,
            MetadataField.BackgroundImage,
            MetadataField.CommunityScore,
            MetadataField.CoverImage,
            MetadataField.CriticScore,
            MetadataField.Developers,
            MetadataField.Genres,
            MetadataField.Icon,
            MetadataField.Links,
            MetadataField.Publishers,
            MetadataField.ReleaseDate,
            MetadataField.Features,
            MetadataField.Name,
            MetadataField.Platform,
            MetadataField.Series,
            MetadataField.Tags,
        };

        public UniversalSteamMetadataProvider(MetadataRequestOptions options, UniversalSteamMetadata plugin, IDownloader downloader)
        {
            this.options = options;
            this.plugin = plugin;
            this.downloader = downloader;
            apiClient = new SteamApiClient(plugin.SettingsViewModel.Settings);
            webApiClient = new WebApiClient();
        }

        public override void Dispose()
        {
            try
            {
                apiClient.Logout();
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to logout Steam client.");
            }

            webApiClient.Dispose();
        }

        public override string GetName(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.Name;
            }

            return base.GetName(args);
        }

        public override string GetDescription(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.Description;
            }

            return base.GetDescription(args);
        }

        public override MetadataFile GetBackgroundImage(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                if (plugin.SettingsViewModel.Settings.BackgroundSource == BackgroundSource.StoreScreenshot &&
                    options.IsBackgroundDownload == false &&
                    currentMetadata.StoreDetails?.screenshots?.Count > 1)
                {
                    var selection = new List<ImageFileOption>();
                    foreach (var screen in currentMetadata.StoreDetails.screenshots)
                    {
                        selection.Add(new SteamImageOption
                        {
                            Path = Regex.Replace(screen.path_thumbnail, "\\?.*$", ""),
                            Image = Regex.Replace(screen.path_full, "\\?.*$", "")
                        });
                    }

                    var selected = plugin.PlayniteApi.Dialogs.ChooseImageFile(
                        selection,
                        "selectImage") as SteamImageOption;
                    if (selected != null)
                    {
                        return new MetadataFile(selected.Image);
                    }
                }
                else
                {
                    return currentMetadata.BackgroundImage;
                }
            }

            return base.GetBackgroundImage(args);
        }

        public override MetadataFile GetIcon(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.Icon;
            }

            return base.GetIcon(args);
        }

        public override MetadataFile GetCoverImage(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.CoverImage;
            }

            return base.GetCoverImage(args);
        }

        public override int? GetCommunityScore(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.CommunityScore;
            }

            return base.GetCommunityScore(args);
        }

        public override int? GetCriticScore(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.CriticScore;
            }

            return base.GetCriticScore(args);
        }

        public override IEnumerable<MetadataProperty> GetDevelopers(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.Developers;
            }

            return base.GetDevelopers(args);
        }

        public override IEnumerable<MetadataProperty> GetGenres(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.Genres;
            }

            return base.GetGenres(args);
        }

        public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.Links;
            }

            return base.GetLinks(args);
        }

        public override IEnumerable<MetadataProperty> GetPublishers(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.Publishers;
            }

            return base.GetPublishers(args);
        }

        public override ReleaseDate? GetReleaseDate(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.ReleaseDate;
            }

            return base.GetReleaseDate(args);
        }

        public override IEnumerable<MetadataProperty> GetFeatures(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.Features;
            }

            return base.GetFeatures(args);
        }

        public override IEnumerable<MetadataProperty> GetSeries(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.Series;
            }

            return base.GetSeries(args);
        }

        public override IEnumerable<MetadataProperty> GetPlatforms(GetMetadataFieldArgs args)
        {
            GetGameData();
            return currentMetadata?.Platforms ?? base.GetPlatforms(args);
        }

        public override IEnumerable<MetadataProperty> GetTags(GetMetadataFieldArgs args)
        {
            GetGameData();
            if (currentMetadata != null)
            {
                return currentMetadata.Tags;
            }

            return base.GetTags(args);
        }

        internal void GetGameData()
        {
            if (currentMetadata != null)
            {
                return;
            }

            try
            {
                var metadataProvider = new MetadataProvider(apiClient, webApiClient, new SteamTagNamer(plugin, plugin.SettingsViewModel.Settings, downloader), plugin.SettingsViewModel.Settings);
                if (BuiltinExtensions.GetExtensionFromId(options.GameData.PluginId) == BuiltinExtension.SteamLibrary)
                {
                    var appId = uint.Parse(options.GameData.GameId);
                    currentMetadata = metadataProvider.GetGameMetadata(
                        appId,
                        plugin.SettingsViewModel.Settings.BackgroundSource,
                        plugin.SettingsViewModel.Settings.DownloadVerticalCovers);
                }
                else
                {
                    if (options.IsBackgroundDownload)
                    {
                        var matchedId = GetMatchingGame(options.GameData);
                        if (matchedId > 0)
                        {
                            currentMetadata = metadataProvider.GetGameMetadata(
                                matchedId,
                                plugin.SettingsViewModel.Settings.BackgroundSource,
                                plugin.SettingsViewModel.Settings.DownloadVerticalCovers);
                        }
                        else
                        {
                            currentMetadata = new SteamGameMetadata();
                        }
                    }
                    else
                    {
                        var selectedGame = plugin.PlayniteApi.Dialogs.ChooseItemWithSearch(null, (a) =>
                        {
                            if (uint.TryParse(a, out var appId))
                            {
                                try
                                {
                                    var store = webApiClient.GetStoreAppDetail(appId, plugin.SettingsViewModel.Settings.LanguageKey);
                                    return new List<GenericItemOption> { new StoreSearchResult
                                    {
                                        GameId = appId,
                                        Name = store.name
                                    }};
                                }
                                catch (Exception e)
                                {
                                    logger.Error(e, $"Failed to get Steam app info {appId}");
                                    return new List<GenericItemOption>();
                                }
                            }
                            else
                            {
                                try
                                {
                                    var name = StringExtensions.NormalizeGameName(a);
                                    return new List<GenericItemOption>(UniversalSteamMetadata.GetSearchResults(name));
                                }
                                catch (Exception e)
                                {
                                    logger.Error(e, $"Failed to get Steam search data for {a}");
                                    return new List<GenericItemOption>();
                                }
                            }
                        }, options.GameData.Name, string.Empty);

                        if (selectedGame == null)
                        {
                            currentMetadata = new SteamGameMetadata();
                        }
                        else
                        {
                            currentMetadata = metadataProvider.GetGameMetadata(
                                ((StoreSearchResult)selectedGame).GameId,
                                plugin.SettingsViewModel.Settings.BackgroundSource,
                                plugin.SettingsViewModel.Settings.DownloadVerticalCovers);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.Error(e, $"Failed to get Steam metadata.");
                currentMetadata = new SteamGameMetadata();
            }
        }

        internal uint MatchFun(string matchName, List<StoreSearchResult> list)
        {
            var res = list.FirstOrDefault(a => string.Equals(matchName, a.Name, StringComparison.InvariantCultureIgnoreCase));
            if (res != null)
            {
                return res.GameId;
            }

            return 0;
        }

        internal string ReplaceNumsForRomans(Match m)
        {
            return Roman.To(int.Parse(m.Value));
        }

        internal uint GetMatchingGame(Game gameInfo)
        {
            var normalizedName = StringExtensions.NormalizeGameName(gameInfo.Name);
            var results = UniversalSteamMetadata.GetSearchResults(normalizedName);

            var alphanumericKey = GameNameMatcher.ToAlphanumericLower(gameInfo.Name);
            var matchingEntry = results.FirstOrDefault(x => GameNameMatcher.ToAlphanumericLower(x.Name) == alphanumericKey);
            if (matchingEntry != null)
            {
                return matchingEntry.GameId;
            }

            var gameKey = GameNameMatcher.ToGameKey(gameInfo.Name);
            matchingEntry = results.FirstOrDefault(x => GameNameMatcher.ToGameKey(x.Name) == gameKey);
            if (matchingEntry != null)
            {
                return matchingEntry.GameId;
            }

            results.ForEach(a => a.Name = StringExtensions.NormalizeGameName(a.Name));

            string testName = string.Empty;
            uint matchedGame = 0;

            // Direct comparison
            matchedGame = MatchFun(normalizedName, results);
            if (matchedGame > 0)
            {
                return matchedGame;
            }

            // Try replacing roman numerals: 3 => III
            testName = Regex.Replace(normalizedName, @"\d+", ReplaceNumsForRomans);
            matchedGame = MatchFun(testName, results);
            if (matchedGame > 0)
            {
                return matchedGame;
            }

            // Try adding The
            testName = "The " + normalizedName;
            matchedGame = MatchFun(testName, results);
            if (matchedGame > 0)
            {
                return matchedGame;
            }

            // Try chaning & / and
            testName = Regex.Replace(normalizedName, @"\s+and\s+", " & ", RegexOptions.IgnoreCase);
            matchedGame = MatchFun(testName, results);
            if (matchedGame > 0)
            {
                return matchedGame;
            }

            // Try removing apostrophes
            var resCopy = Serialization.GetClone(results);
            resCopy.ForEach(a => a.Name = a.Name.Replace("'", ""));
            matchedGame = MatchFun(normalizedName, resCopy);
            if (matchedGame > 0)
            {
                return matchedGame;
            }

            // Try removing all ":" and "-"
            testName = Regex.Replace(normalizedName, @"\s*(:|-)\s*", " ");
            resCopy = Serialization.GetClone(results);
            foreach (var res in resCopy)
            {
                res.Name = Regex.Replace(res.Name, @"\s*(:|-)\s*", " ");
            }

            matchedGame = MatchFun(testName, resCopy);
            if (matchedGame > 0)
            {
                return matchedGame;
            }

            // Try without subtitle
            var testResult = results.FirstOrDefault(a =>
            {
                if (!string.IsNullOrEmpty(a.Name) && a.Name.Contains(":"))
                {
                    return string.Equals(normalizedName, a.Name.Split(':')[0], StringComparison.InvariantCultureIgnoreCase);
                }

                return false;
            });

            if (testResult != null)
            {
                return testResult.GameId;
            }

            return 0;
        }
    }
}