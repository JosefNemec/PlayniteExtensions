using AngleSharp.Parser.Html;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Metadata;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Steam;
using Steam.Models;
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
        private SteamGameMetadata currentMetadata;
        private readonly SteamApiClient apiClient;

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
            MetadataField.Name
        };

        public UniversalSteamMetadataProvider(MetadataRequestOptions options, UniversalSteamMetadata plugin)
        {
            this.options = options;
            this.plugin = plugin;
            apiClient = new SteamApiClient();
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
        }

        public override string GetName()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.GameInfo.Name;
            }

            return base.GetName();
        }

        public override string GetDescription()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.GameInfo.Description;
            }

            return base.GetDescription();
        }

        public override MetadataFile GetBackgroundImage()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                if (plugin.Settings.BackgroundSource == BackgroundSource.StoreScreenshot &&
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

            return base.GetBackgroundImage();
        }

        public override MetadataFile GetIcon()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.Icon;
            }

            return base.GetIcon();
        }

        public override MetadataFile GetCoverImage()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.CoverImage;
            }

            return base.GetCoverImage();
        }

        public override int? GetCommunityScore()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.GameInfo.CommunityScore;
            }

            return base.GetCommunityScore();
        }

        public override int? GetCriticScore()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.GameInfo.CriticScore;
            }

            return base.GetCriticScore();
        }

        public override List<string> GetDevelopers()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.GameInfo.Developers;
            }

            return base.GetDevelopers();
        }

        public override List<string> GetGenres()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.GameInfo.Genres;
            }

            return base.GetGenres();
        }

        public override List<Link> GetLinks()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.GameInfo.Links;
            }

            return base.GetLinks();
        }

        public override List<string> GetPublishers()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.GameInfo.Publishers;
            }

            return base.GetPublishers();
        }

        public override DateTime? GetReleaseDate()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.GameInfo.ReleaseDate;
            }

            return base.GetReleaseDate();
        }

        public override List<string> GetFeatures()
        {
            GetGameData();
            if (currentMetadata.GameInfo != null)
            {
                return currentMetadata.GameInfo.Features;
            }

            return base.GetFeatures();
        }

        internal void GetGameData()
        {
            if (currentMetadata != null)
            {
                return;
            }

            try
            {
                var metadataProvider = new MetadataProvider(apiClient);
                if (BuiltinExtensions.GetExtensionFromId(options.GameData.PluginId) == BuiltinExtension.SteamLibrary)
                {
                    var appId = uint.Parse(options.GameData.GameId);
                    currentMetadata = metadataProvider.GetGameMetadata(
                        appId,
                        plugin.Settings.BackgroundSource,
                        plugin.Settings.DownloadVerticalCovers);
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
                                plugin.Settings.BackgroundSource,
                                plugin.Settings.DownloadVerticalCovers);
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
                                    var store = WebApiClient.GetStoreAppDetail(appId);
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
                                plugin.Settings.BackgroundSource,
                                plugin.Settings.DownloadVerticalCovers);
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
            var resCopy = results.GetClone();
            resCopy.ForEach(a => a.Name = a.Name.Replace("'", ""));
            matchedGame = MatchFun(normalizedName, resCopy);
            if (matchedGame > 0)
            {
                return matchedGame;
            }

            // Try removing all ":" and "-"
            testName = Regex.Replace(normalizedName, @"\s*(:|-)\s*", " ");
            resCopy = results.GetClone();
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