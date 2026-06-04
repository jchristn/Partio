namespace Partio.Core.ThirdParty
{
    /// <summary>
    /// Result of a recorded HTTP call to an upstream embedding endpoint.
    /// </summary>
    public class EmbeddingHttpResult
    {
        /// <summary>
        /// The HTTP response message, if a caller explicitly supplies one.
        /// Partio-owned provider calls dispose responses before returning and leave this null.
        /// </summary>
        public HttpResponseMessage? Response { get; set; }

        /// <summary>
        /// HTTP response status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Indicates whether the response status code was successful.
        /// </summary>
        public bool IsSuccessStatusCode { get; set; }

        /// <summary>
        /// Response headers captured before the HTTP response was disposed.
        /// </summary>
        public Dictionary<string, string> ResponseHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The response body as a string.
        /// </summary>
        public string ResponseBody { get; set; } = string.Empty;
    }
}
