namespace Partio.Core.ThirdParty
{
    /// <summary>
    /// Result of a recorded HTTP call to an upstream embedding endpoint.
    /// </summary>
    public class EmbeddingHttpResult
    {
        /// <summary>
        /// The HTTP response message.
        /// </summary>
        public HttpResponseMessage Response { get; set; } = null!;

        /// <summary>
        /// The response body as a string.
        /// </summary>
        public string ResponseBody { get; set; } = string.Empty;
    }
}
