namespace Partio.Core.ThirdParty
{
    using System.Text;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for the Ollama API.
    /// </summary>
    public class OllamaEmbeddingClient : EmbeddingClientBase
    {
        private readonly SerializationHelper.Serializer _Serializer = new SerializationHelper.Serializer();

        /// <summary>
        /// Initialize a new OllamaEmbeddingClient.
        /// </summary>
        /// <param name="endpoint">Ollama server endpoint URL.</param>
        /// <param name="apiKey">API key (nullable, typically not needed for Ollama).</param>
        /// <param name="logging">Logging module.</param>
        public OllamaEmbeddingClient(string endpoint, string? apiKey, LoggingModule logging)
            : base(endpoint, apiKey, logging)
        {
            _Header = "[OllamaEmbedding] ";

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
            string url = _Endpoint.TrimEnd('/') + "/api/embed";

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
                throw new Exception($"Ollama embedding request failed with status {(int)response.StatusCode}: {responseBody}");
            }

            Dictionary<string, object>? responseObj = _Serializer.DeserializeJson<Dictionary<string, object>>(responseBody);
            if (responseObj == null || !responseObj.ContainsKey("embeddings"))
            {
                throw new Exception("Ollama embedding response missing 'embeddings' field.");
            }

            List<List<float>> embeddings = new List<List<float>>();

            // Parse the embeddings from the JSON response
            string embeddingsJson = _Serializer.SerializeJson(responseObj["embeddings"], false);
            List<List<float>>? parsedEmbeddings = _Serializer.DeserializeJson<List<List<float>>>(embeddingsJson);

            if (parsedEmbeddings != null)
            {
                embeddings = parsedEmbeddings;
            }

            return embeddings;
        }
    }
}
