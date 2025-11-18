using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PlayniteExtensions.Common
{
    public class BackendResponseBase
    {
        public string Error { get; set; }
    }

    public class BackendDataResponse<T> : BackendResponseBase
    {
        public T Data { get; set; }
    }

    public class BackendClient : IDisposable
    {
        private readonly HttpClient httpClient;

        public BackendClient(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentNullException(nameof(endpoint));

            if (!endpoint.EndsWith("/"))
                endpoint += '/';

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

        public async Task<T> PostRequest<T>(string url, object payload)
        {
            HttpContent content = null;
            if (payload != null)
                content = new StringContent(Serialization.ToJson(payload), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(url, content);
            var strResponse = await response.Content.ReadAsStringAsync();
            CheckResponse(response, strResponse);
            var data = Serialization.FromJson<BackendDataResponse<T>>(strResponse);
            if (data == null)
                return default;

            return data.Data;
        }

        public async Task<T> GetRequest<T>(string url)
        {
            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            CheckResponse(response, content);
            var data = Serialization.FromJson<BackendDataResponse<T>>(content);
            if (data == null)
                return default;

            return data.Data;
        }

        private static void CheckResponse(HttpResponseMessage message, string content)
        {
            BackendResponseBase response = null;
            if (!string.IsNullOrWhiteSpace(content))
                Serialization.TryFromJson(content, out response);

            if (!message.IsSuccessStatusCode)
                throw new Exception($"Server returned failure {message.StatusCode}: {response?.Error}");

            if (!string.IsNullOrEmpty(response?.Error))
                throw new Exception(response.Error);
        }

    }
}