using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Igdb = PlayniteServices.IGDB;

namespace IGDBMetadata
{
    public class ResponseBase
    {
        public string Error { get; set; }
    }

    public class DataResponse<T> : ResponseBase
    {
        public T Data { get; set; }
    }

    public class IgdbClient : IDisposable
    {
        private readonly HttpClient httpClient;

        public IgdbClient(string endpoint)
        {
            if (endpoint.IsNullOrWhiteSpace())
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (!endpoint.EndsWith("/"))
            {
                endpoint += '/';
            }

            httpClient = new HttpClient
            {
                Timeout = new TimeSpan(0, 0, 30),
                BaseAddress = new Uri(endpoint)
            };
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }

        private async Task<T> PostRequest<T>(string url, object payload)
        {
            HttpContent content = null;
            if (payload != null)
            {
                content = new StringContent(Serialization.ToJson(payload), Encoding.UTF8, "application/json");
            }

            var response = await httpClient.PostAsync(url, content);
            var strResponse = await response.Content.ReadAsStringAsync();
            CheckResponse(response, strResponse);
            var data = Serialization.FromJson<DataResponse<T>>(strResponse);
            if (data == null)
            {
                return default;
            }

            return data.Data;
        }

        private async Task<T> GetRequest<T>(string url)
        {
            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            CheckResponse(response, content);
            var data = Serialization.FromJson<DataResponse<T>>(content);
            if (data == null)
            {
                return default;
            }

            return data.Data;
        }

        private static void CheckResponse(HttpResponseMessage message, string content)
        {
            ResponseBase response = null;
            if (!content.IsNullOrWhiteSpace())
            {
                Serialization.TryFromJson(content, out response, out var _);
            }

            if (!message.IsSuccessStatusCode)
            {
                throw new Exception($"Server returned failure {message.StatusCode}: {response?.Error}");
            }

            if (response?.Error.IsNullOrEmpty() == false)
            {
                throw new Exception(response.Error);
            }
        }

        public async Task<List<Igdb.Game>> SearchGames(Igdb.SearchRequest request)
        {
            return await PostRequest<List<Igdb.Game>>("igdb/search", request);
        }

        public async Task<Igdb.Game> GetMetadata(Igdb.MetadataRequest request)
        {
            return await PostRequest<Igdb.Game>("igdb/metadata", request);
        }

        public async Task<Igdb.Game> GetGame(ulong gameId)
        {
            return await GetRequest<Igdb.Game>($"igdb/game/{gameId}");
        }
    }
}
