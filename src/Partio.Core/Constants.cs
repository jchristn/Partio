namespace Partio.Core
{
    /// <summary>
    /// Application-wide constants for Partio.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// ASCII art logo displayed at startup.
        /// </summary>
        public static readonly string Logo =
            @"

                  _   _       
 _ __   __ _ _ __| |_(_) ___  
| '_ \ / _` | '__| __| |/ _ \ 
| |_) | (_| | |  | |_| | (_) |
| .__/ \__,_|_|   \__|_|\___/ 
|_|                           

";

        /// <summary>
        /// Current version string.
        /// </summary>
        public static readonly string Version = "0.1.0";

        /// <summary>
        /// Settings filename.
        /// </summary>
        public static readonly string SettingsFilename = "partio.json";

        /// <summary>
        /// Default log directory.
        /// </summary>
        public static readonly string LogDirectory = "./logs/";

        /// <summary>
        /// Default log filename.
        /// </summary>
        public static readonly string LogFilename = "partio.log";

        /// <summary>
        /// JSON content type.
        /// </summary>
        public static readonly string JsonContentType = "application/json";

        /// <summary>
        /// HTML content type.
        /// </summary>
        public static readonly string HtmlContentType = "text/html";

        /// <summary>
        /// Authorization header name.
        /// </summary>
        public static readonly string AuthorizationHeader = "Authorization";

        /// <summary>
        /// Bearer token prefix.
        /// </summary>
        public static readonly string BearerPrefix = "Bearer ";

        /// <summary>
        /// ID prefix for tenants.
        /// </summary>
        public static readonly string TenantIdPrefix = "ten_";

        /// <summary>
        /// ID prefix for users.
        /// </summary>
        public static readonly string UserIdPrefix = "usr_";

        /// <summary>
        /// ID prefix for credentials.
        /// </summary>
        public static readonly string CredentialIdPrefix = "cred_";

        /// <summary>
        /// ID prefix for embedding endpoints.
        /// </summary>
        public static readonly string EmbeddingEndpointIdPrefix = "ep_";

        /// <summary>
        /// ID prefix for request history entries.
        /// </summary>
        public static readonly string RequestHistoryIdPrefix = "req_";

        /// <summary>
        /// Default request history directory.
        /// </summary>
        public static readonly string RequestHistoryDirectory = "./request-history/";

        /// <summary>
        /// Default database filename.
        /// </summary>
        public static readonly string DatabaseFilename = "partio.db";

        /// <summary>
        /// Response header for the embedding endpoint ID.
        /// </summary>
        public static readonly string EndpointIdHeader = "X-Partio-Endpoint-Id";

        /// <summary>
        /// Response header for the model name.
        /// </summary>
        public static readonly string ModelHeader = "X-Model";
    }
}
