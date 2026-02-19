namespace Partio.Core.ThirdParty
{
    using System.Text;
    using SyslogLogging;

    /// <summary>
    /// Completion client for the OpenAI-compatible API (/v1/chat/completions).
    /// </summary>
    public class OpenAiCompletionClient : CompletionClientBase
    {
        private readonly SerializationHelper.Serializer _Serializer = new SerializationHelper.Serializer();

        /// <summary>
        /// Initialize a new OpenAiCompletionClient.
        /// </summary>
        /// <param name="endpoint">OpenAI API endpoint URL.</param>
        /// <param name="apiKey">API key (required for OpenAI).</param>
        /// <param name="logging">Logging module.</param>
        public OpenAiCompletionClient(string endpoint, string? apiKey, LoggingModule logging)
            : base(endpoint, apiKey, logging)
        {
            _Header = "[OpenAiCompletion] ";

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
            CancellationToken token = default,
            string? systemPrompt = null)
        {
            string url = _Endpoint.TrimEnd('/') + "/v1/chat/completions";

            List<Dictionary<string, string>> messages = new List<Dictionary<string, string>>();
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new Dictionary<string, string> { { "role", "system" }, { "content", systemPrompt } });
            }
            messages.Add(new Dictionary<string, string> { { "role", "user" }, { "content", prompt } });

            Dictionary<string, object> requestBody = new Dictionary<string, object>
            {
                { "model", model },
                { "messages", messages },
                { "max_tokens", maxTokens }
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
            if (responseObj == null || !responseObj.ContainsKey("choices"))
            {
                _Logging.Warn(_Header + "completion response missing 'choices' field");
                return null;
            }

            // Parse choices[0].message.content
            string choicesJson = _Serializer.SerializeJson(responseObj["choices"], false);
            List<Dictionary<string, object>>? choices = _Serializer.DeserializeJson<List<Dictionary<string, object>>>(choicesJson);

            if (choices == null || choices.Count == 0)
            {
                _Logging.Warn(_Header + "completion response has empty choices array");
                return null;
            }

            if (!choices[0].ContainsKey("message"))
            {
                _Logging.Warn(_Header + "completion response choice missing 'message' field");
                return null;
            }

            string messageJson = _Serializer.SerializeJson(choices[0]["message"], false);
            Dictionary<string, object>? message = _Serializer.DeserializeJson<Dictionary<string, object>>(messageJson);

            if (message == null || !message.ContainsKey("content"))
            {
                _Logging.Warn(_Header + "completion response message missing 'content' field");
                return null;
            }

            string? completionText = message["content"]?.ToString();
            return string.IsNullOrWhiteSpace(completionText) ? null : completionText.Trim();
        }
    }
}
