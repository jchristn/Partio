namespace Partio.Core.ThirdParty
{
    using System.Text;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for the OpenAI API.
    /// </summary>
    public class OpenAiEmbeddingClient : EmbeddingClientBase
    {
        private readonly SerializationHelper.Serializer _Serializer = new SerializationHelper.Serializer();

        /// <summary>
        /// Initialize a new OpenAiEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">OpenAI API endpoint URL.</param>
        /// <param name="apiKey">API key (required for OpenAI).</param>
        /// <param name="logging">Logging module.</param>
        public OpenAiEmbeddingClient(string endpoint, string? apiKey, LoggingModule logging)
            : base(endpoint, apiKey, logging)
        {
            _Header = "[OpenAiEmbedding] ";

            if (!string.IsNullOrEmpty(apiKey))
            {
                _HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
            }
        }

        /// <inheritdoc />
        public override async Task<List<float>> EmbedAsync(string text, string model, CancellationToken token = default)
        {
            List<List<float>> results = await EmbedBatchAsync(new List<string> { text }, model, token).ConfigureAwait(false);
            return results.Count > 0 ? results[0] : new List<float>();
        }

        /// <inheritdoc />
        public override async Task<List<List<float>>> EmbedBatchAsync(List<string> texts, string model, CancellationToken token = default)
        {
            string url = _Endpoint.TrimEnd('/') + "/v1/embeddings";

            Dictionary<string, object> requestBody = new Dictionary<string, object>
            {
                { "model", model },
                { "input", texts }
            };

            string json = _Serializer.SerializeJson(requestBody, false);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            _Logging.Debug(_Header + "POST " + url);

            HttpResponseMessage response = await _HttpClient.PostAsync(url, content, token).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OpenAI embedding request failed with status {(int)response.StatusCode}: {responseBody}");
            }

            Dictionary<string, object>? responseObj = _Serializer.DeserializeJson<Dictionary<string, object>>(responseBody);
            if (responseObj == null || !responseObj.ContainsKey("data"))
            {
                throw new Exception("OpenAI embedding response missing 'data' field.");
            }

            List<List<float>> embeddings = new List<List<float>>();

            // Parse the data array from the response
            string dataJson = _Serializer.SerializeJson(responseObj["data"], false);
            List<Dictionary<string, object>>? dataList = _Serializer.DeserializeJson<List<Dictionary<string, object>>>(dataJson);

            if (dataList != null)
            {
                foreach (Dictionary<string, object> item in dataList)
                {
                    if (item.ContainsKey("embedding"))
                    {
                        string embJson = _Serializer.SerializeJson(item["embedding"], false);
                        List<float>? embVector = _Serializer.DeserializeJson<List<float>>(embJson);
                        embeddings.Add(embVector ?? new List<float>());
                    }
                }
            }

            return embeddings;
        }
    }
}
