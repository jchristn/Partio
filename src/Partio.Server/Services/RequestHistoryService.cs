namespace Partio.Server.Services
{
    using Partio.Core;
    using Partio.Core.Database;
    using Partio.Core.Models;
    using Partio.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Service for recording request history entries and persisting request/response bodies.
    /// </summary>
    public class RequestHistoryService
    {
        private readonly ServerSettings _Settings;
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[RequestHistory] ";
        private readonly SerializationHelper.Serializer _Serializer = new SerializationHelper.Serializer();

        /// <summary>
        /// Initialize a new RequestHistoryService.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public RequestHistoryService(ServerSettings settings, DatabaseDriverBase database, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));

            if (!Directory.Exists(_Settings.RequestHistory.Directory))
                Directory.CreateDirectory(_Settings.RequestHistory.Directory);
        }

        /// <summary>
        /// Create a request history entry at the start of a request.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="url">Request URL.</param>
        /// <param name="sourceIp">Requestor IP address.</param>
        /// <param name="auth">Authentication context.</param>
        /// <returns>The created entry.</returns>
        public async Task<RequestHistoryEntry> CreateEntryAsync(string method, string url, string? sourceIp, AuthContext? auth)
        {
            RequestHistoryEntry entry = new RequestHistoryEntry();
            entry.HttpMethod = method;
            entry.HttpUrl = url;
            entry.RequestorIp = sourceIp;

            if (auth != null)
            {
                entry.TenantId = auth.TenantId;
                entry.UserId = auth.UserId;
                entry.CredentialId = auth.CredentialId;
            }

            await _Database.RequestHistory.CreateAsync(entry).ConfigureAwait(false);
            return entry;
        }

        /// <summary>
        /// Update a request history entry with response details and persist bodies to filesystem.
        /// </summary>
        /// <param name="entry">The entry to update.</param>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="responseTimeMs">Response time in milliseconds.</param>
        /// <param name="requestBody">Request body string.</param>
        /// <param name="responseBody">Response body string.</param>
        /// <param name="requestHeaders">Request headers dictionary.</param>
        /// <param name="responseHeaders">Response headers dictionary.</param>
        /// <param name="embeddingCalls">Details of upstream embedding HTTP calls, if any.</param>
        public async Task UpdateWithResponseAsync(
            RequestHistoryEntry entry,
            int statusCode,
            long responseTimeMs,
            string? requestBody,
            string? responseBody,
            Dictionary<string, string>? requestHeaders = null,
            Dictionary<string, string>? responseHeaders = null,
            List<EmbeddingCallDetail>? embeddingCalls = null)
        {
            entry.HttpStatus = statusCode;
            entry.ResponseTimeMs = responseTimeMs;
            entry.CompletedUtc = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(requestBody))
            {
                entry.RequestBodyLength = requestBody.Length;
                if (requestBody.Length > _Settings.RequestHistory.MaxRequestBodyBytes)
                    requestBody = requestBody.Substring(0, _Settings.RequestHistory.MaxRequestBodyBytes);
            }

            if (!string.IsNullOrEmpty(responseBody))
            {
                entry.ResponseBodyLength = responseBody.Length;
                if (responseBody.Length > _Settings.RequestHistory.MaxResponseBodyBytes)
                    responseBody = responseBody.Substring(0, _Settings.RequestHistory.MaxResponseBodyBytes);
            }

            // Truncate inner call bodies
            if (embeddingCalls != null)
            {
                foreach (EmbeddingCallDetail call in embeddingCalls)
                {
                    if (!string.IsNullOrEmpty(call.RequestBody) && call.RequestBody.Length > _Settings.RequestHistory.MaxRequestBodyBytes)
                        call.RequestBody = call.RequestBody.Substring(0, _Settings.RequestHistory.MaxRequestBodyBytes);
                    if (!string.IsNullOrEmpty(call.ResponseBody) && call.ResponseBody.Length > _Settings.RequestHistory.MaxResponseBodyBytes)
                        call.ResponseBody = call.ResponseBody.Substring(0, _Settings.RequestHistory.MaxResponseBodyBytes);
                }
            }

            // Persist bodies to filesystem
            string objectKey = Guid.NewGuid().ToString();
            entry.ObjectKey = objectKey;

            Dictionary<string, object?> detail = new Dictionary<string, object?>
            {
                { "RequestHeaders", requestHeaders },
                { "RequestBody", requestBody },
                { "ResponseHeaders", responseHeaders },
                { "ResponseBody", responseBody },
                { "EmbeddingCalls", embeddingCalls }
            };

            string json = _Serializer.SerializeJson(detail, true);
            string filePath = Path.Combine(_Settings.RequestHistory.Directory, objectKey + ".json");

            try
            {
                await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "failed to write request history detail: " + ex.Message);
            }

            await _Database.RequestHistory.UpdateAsync(entry).ConfigureAwait(false);
        }

        /// <summary>
        /// Read the detail file for a request history entry.
        /// </summary>
        /// <param name="objectKey">Object key (filename without extension).</param>
        /// <returns>Detail JSON string, or null if not found.</returns>
        public async Task<string?> ReadDetailAsync(string objectKey)
        {
            string filePath = Path.Combine(_Settings.RequestHistory.Directory, objectKey + ".json");
            if (!File.Exists(filePath)) return null;
            return await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        }
    }
}
