using Playnite.SDK.Data;
using PlayniteServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Igdb = PlayniteServices.Controllers.IGDB;

namespace IGDBMetadata
{
    public class DataResponse<T>
    {
        public string Error { get; set; }
        public T Data { get; set; }
    }

    public class IgdbClient : IDisposable
    {
        private readonly HttpClient httpClient;

        public IgdbClient(string endpoint)
        {
            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(endpoint);
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }

        private async Task<T> PostRequest<T>(string url, object payload)
        {
            var content = new StringContent(Serialization.ToJson(payload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(url, content);
            var data = Serialization.FromJson<DataResponse<T>>(await response.Content.ReadAsStringAsync());
            if (!data.Error.IsNullOrEmpty())
            {
                throw new Exception(data.Error);
            }

            return data.Data!;
        }

        private async Task<T> GetRequest<T>(string url)
        {
            var response = await httpClient.GetAsync(url);
            var data = Serialization.FromJson<DataResponse<T>>(await response.Content.ReadAsStringAsync());
            if (!data.Error.IsNullOrEmpty())
            {
                throw new Exception(data.Error);
            }

            return data.Data!;
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
