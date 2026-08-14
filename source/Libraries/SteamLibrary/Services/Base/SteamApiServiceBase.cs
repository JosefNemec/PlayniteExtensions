using Playnite.SDK;
using Playnite.SDK.Data;
using SteamLibrary.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace SteamLibrary.Services.Base
{
    public abstract class SteamApiServiceBase
    {
        protected ILogger logger = LogManager.GetLogger();

        public TResponse Get<TResponse>(string baseUrl, Dictionary<string, string> parameters, SteamApiRetrySettings retrySettings = null) where TResponse : class
        {
            retrySettings ??= new SteamApiRetrySettings();
            var urlStringBuilder = new StringBuilder(baseUrl);
            if (parameters != null && parameters.Count > 0)
            {
                bool firstParameter = true;
                urlStringBuilder.Append('?');
                foreach (var parameter in parameters)
                {
                    urlStringBuilder.Append(firstParameter ? '?' : '&');
                    urlStringBuilder.Append(parameter.Key);
                    urlStringBuilder.Append('=');
                    urlStringBuilder.Append(Uri.EscapeUriString(parameter.Value));
                    firstParameter = false;
                }
            }

            var url = urlStringBuilder.ToString();

            using (var webClient = new WebClient())
            {
                webClient.Encoding = Encoding.UTF8;
                int currentRetry = 0;
                while (true)
                {
                    try
                    {
                        var responseString = webClient.DownloadString(url);
                        var responseRoot = Serialization.FromJson<SteamApiResponseRoot<TResponse>>(responseString);
                        return responseRoot?.response;
                    }
                    catch (WebException wex) when (wex.Response is HttpWebResponse response
                                                   && retrySettings.MaxRetries > currentRetry
                                                   && retrySettings.StatusCodeWhitelistContains(response.StatusCode))
                    {
                        logger.Info($"{baseUrl} returned {response.StatusCode}, retrying after {retrySettings.DelaySeconds} seconds");
                        Thread.Sleep(1_000 * retrySettings.DelaySeconds);
                        currentRetry++;
                    }
                }
            }
        }

        internal static DateTime? GetLastPlayedDateTime(long unixEpochSeconds)
        {
            if (unixEpochSeconds == 0)
                return null;

            var lastPlayed = DateTimeOffset.FromUnixTimeSeconds(unixEpochSeconds).LocalDateTime;

            // Some games last played before Steam started storing last played times (about 2008?) report as last played 1970-01-02 00:00:00 UTC
            return lastPlayed.Year < 2004 ? null : lastPlayed;
        }
    }

    public class SteamApiRetrySettings
    {
        public int MaxRetries { get; } = 0;
        public int DelaySeconds { get; } = 5;
        public List<int> StatusCodeWhitelist { get; } = new List<int>();

        public SteamApiRetrySettings(int maxRetries, int delaySeconds, params int[] statusCodeWhitelist)
        {
            MaxRetries = maxRetries;
            DelaySeconds = delaySeconds;
            StatusCodeWhitelist.AddRange(statusCodeWhitelist);
        }

        public SteamApiRetrySettings() { }

        public bool StatusCodeWhitelistContains(HttpStatusCode statusCode)
        {
            return StatusCodeWhitelist.Count == 0
                   || StatusCodeWhitelist.Contains((int)statusCode);
        }
    }
}
