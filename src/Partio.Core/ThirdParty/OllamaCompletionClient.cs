namespace Partio.Core.ThirdParty
{
    using System.Text;
    using SyslogLogging;

    /// <summary>
    /// Completion client for the Ollama API (/api/generate).
    /// </summary>
    public class OllamaCompletionClient : CompletionClientBase
    {
        private readonly SerializationHelper.Serializer _Serializer = new SerializationHelper.Serializer();

        /// <summary>
        /// Initialize a new OllamaCompletionClient.
        /// </summary>
        /// <param name="endpoint">Ollama server endpoint URL.</param>
        /// <param name="apiKey">API key (nullable, typically not needed for Ollama).</param>
        /// <param name="logging">Logging module.</param>
        public OllamaCompletionClient(string endpoint, string? apiKey, LoggingModule logging)
            : base(endpoint, apiKey, logging)
        {
            _Header = "[OllamaCompletion] ";

            if (!string.IsNullOrEmpty(apiKey))
            {
                _HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
            }
        }

        /// <inheritdoc />
        public override async Task<string?> GenerateCompletionAsync(
            string prompt,
            string model,
            int maxTokens,
            int timeoutMs,
            CancellationToken token = default)
        {
            string url = _Endpoint.TrimEnd('/') + "/api/generate";

            Dictionary<string, object> requestBody = new Dictionary<string, object>
            {
                { "model", model },
                { "prompt", prompt },
                { "stream", false },
                { "options", new Dictionary<string, object> { { "num_predict", maxTokens } } }
            };

            string json = _Serializer.SerializeJson(requestBody, false);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

            _Logging.Debug(_Header + "POST " + url);

            CompletionHttpResult result = await PostAndRecordAsync(url, content, json, timeoutMs, token).ConfigureAwait(false);
            HttpResponseMessage response = result.Response;
            string responseBody = result.ResponseBody;

            if (!response.IsSuccessStatusCode)
            {
                _Logging.Warn(_Header + "completion request failed with status " + (int)response.StatusCode + ": " + responseBody);
                return null;
            }

            Dictionary<string, object>? responseObj = _Serializer.DeserializeJson<Dictionary<string, object>>(responseBody);
            if (responseObj == null || !responseObj.ContainsKey("response"))
            {
                _Logging.Warn(_Header + "completion response missing 'response' field");
                return null;
            }

            string? completionText = responseObj["response"]?.ToString();
            return string.IsNullOrWhiteSpace(completionText) ? null : completionText.Trim();
        }
    }
}
