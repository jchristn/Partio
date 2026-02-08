namespace Partio.Core.ThirdParty
{
    using System.Collections.Concurrent;
    using System.Text;
    using System.Text.RegularExpressions;
    using SyslogLogging;

    /// <summary>
    /// Embedding client for the Ollama API.
    /// </summary>
    public class OllamaEmbeddingClient : EmbeddingClientBase
    {
        private static readonly ConcurrentDictionary<string, Dictionary<string, object>> _ModelInfoCache = new ConcurrentDictionary<string, Dictionary<string, object>>();
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

            // Try batch request first
            Dictionary<string, object> batchRequestBody = new Dictionary<string, object>
            {
                { "model", model },
                { "input", texts }
            };

            string batchJson = _Serializer.SerializeJson(batchRequestBody, false);
            StringContent batchContent = new StringContent(batchJson, Encoding.UTF8, "application/json");

            _Logging.Debug(_Header + "POST " + url + " (batch of " + texts.Count + ")");

            EmbeddingHttpResult batchResult = await PostAndRecordAsync(url, batchContent, batchJson, token).ConfigureAwait(false);
            HttpResponseMessage batchResponse = batchResult.Response;
            string batchResponseBody = batchResult.ResponseBody;

            if (batchResponse.IsSuccessStatusCode)
            {
                return ParseEmbeddingsResponse(batchResponseBody);
            }

            // If batch failed due to context length, fall back to individual requests
            if ((int)batchResponse.StatusCode == 400 && batchResponseBody.Contains("context length"))
            {
                _Logging.Warn(_Header + "batch embed failed due to context length, falling back to individual requests");
                return await EmbedIndividuallyAsync(texts, model, url, token).ConfigureAwait(false);
            }

            throw new Exception($"Ollama embedding request failed with status {(int)batchResponse.StatusCode}: {batchResponseBody}");
        }

        /// <inheritdoc />
        public override async Task<int?> GetModelContextLengthAsync(string model, CancellationToken token = default)
        {
            Dictionary<string, object>? info = await GetModelInfoAsync(model, token).ConfigureAwait(false);
            if (info == null) return null;

            // Prefer num_ctx from the parameters field (actual runtime limit)
            int? numCtx = ParseNumCtxFromParameters(info);
            if (numCtx.HasValue)
            {
                _Logging.Info(_Header + "model " + model + " num_ctx: " + numCtx.Value);
                return numCtx.Value;
            }

            // Fall back to {arch}.context_length from model_info (architecture max)
            int? archContextLength = ParseArchContextLength(info);
            if (archContextLength.HasValue)
            {
                _Logging.Info(_Header + "model " + model + " architecture context_length: " + archContextLength.Value);
                return archContextLength.Value;
            }

            return null;
        }

        /// <summary>
        /// Retrieve the full model information from Ollama's /api/show endpoint.
        /// Results are cached in memory by endpoint and model name.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Parsed response dictionary, or null on failure.</returns>
        public async Task<Dictionary<string, object>?> GetModelInfoAsync(string model, CancellationToken token = default)
        {
            string cacheKey = _Endpoint + "|" + model;

            if (_ModelInfoCache.TryGetValue(cacheKey, out Dictionary<string, object>? cached))
                return cached;

            try
            {
                string url = _Endpoint.TrimEnd('/') + "/api/show";

                Dictionary<string, object> requestBody = new Dictionary<string, object>
                {
                    { "model", model }
                };

                string json = _Serializer.SerializeJson(requestBody, false);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                _Logging.Debug(_Header + "POST " + url + " (model info for " + model + ")");

                HttpResponseMessage response = await _HttpClient.PostAsync(url, content, token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;

                string responseBody = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                Dictionary<string, object>? responseObj = _Serializer.DeserializeJson<Dictionary<string, object>>(responseBody);
                if (responseObj == null) return null;

                _ModelInfoCache[cacheKey] = responseObj;
                return responseObj;
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to retrieve model info: " + ex.Message);
                return null;
            }
        }

        private async Task<List<List<float>>> EmbedIndividuallyAsync(List<string> texts, string model, string url, CancellationToken token)
        {
            List<List<float>> embeddings = new List<List<float>>();

            foreach (string text in texts)
            {
                Dictionary<string, object> requestBody = new Dictionary<string, object>
                {
                    { "model", model },
                    { "input", text }
                };

                string json = _Serializer.SerializeJson(requestBody, false);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

                _Logging.Debug(_Header + "POST " + url);

                EmbeddingHttpResult result = await PostAndRecordAsync(url, content, json, token).ConfigureAwait(false);
                HttpResponseMessage response = result.Response;
                string responseBody = result.ResponseBody;

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ollama embedding request failed with status {(int)response.StatusCode}: {responseBody}");
                }

                List<List<float>> parsed = ParseEmbeddingsResponse(responseBody);
                if (parsed.Count > 0)
                {
                    embeddings.Add(parsed[0]);
                }
            }

            return embeddings;
        }

        private int? ParseNumCtxFromParameters(Dictionary<string, object> info)
        {
            if (!info.ContainsKey("parameters")) return null;

            string? parameters = info["parameters"]?.ToString();
            if (string.IsNullOrEmpty(parameters)) return null;

            // Parameters is a newline-delimited string like "num_ctx                        256"
            Match match = Regex.Match(parameters, @"num_ctx\s+(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int numCtx))
            {
                return numCtx;
            }

            return null;
        }

        private int? ParseArchContextLength(Dictionary<string, object> info)
        {
            if (!info.ContainsKey("model_info")) return null;

            string modelInfoJson = _Serializer.SerializeJson(info["model_info"], false);
            Dictionary<string, object>? modelInfo = _Serializer.DeserializeJson<Dictionary<string, object>>(modelInfoJson);
            if (modelInfo == null) return null;

            foreach (KeyValuePair<string, object> kvp in modelInfo)
            {
                if (kvp.Key.EndsWith(".context_length") && kvp.Value != null)
                {
                    if (int.TryParse(kvp.Value.ToString(), out int contextLength))
                    {
                        return contextLength;
                    }
                }
            }

            return null;
        }

        private List<List<float>> ParseEmbeddingsResponse(string responseBody)
        {
            Dictionary<string, object>? responseObj = _Serializer.DeserializeJson<Dictionary<string, object>>(responseBody);
            if (responseObj == null || !responseObj.ContainsKey("embeddings"))
            {
                throw new Exception("Ollama embedding response missing 'embeddings' field.");
            }

            string embeddingsJson = _Serializer.SerializeJson(responseObj["embeddings"], false);
            List<List<float>>? parsedEmbeddings = _Serializer.DeserializeJson<List<List<float>>>(embeddingsJson);

            return parsedEmbeddings ?? new List<List<float>>();
        }
    }
}
