namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Result of a transparent proxy call (<c>/v1.0/proxy/{endpointId}/...</c>). The proxy relays the
    /// upstream provider's response verbatim, so the status code and body are exactly what the provider
    /// returned. Unlike the typed API methods, a non-2xx status does NOT raise a <see cref="PartioException"/>
    /// — inspect <see cref="StatusCode"/> and <see cref="Body"/> directly.
    /// </summary>
    public class ProxyResponse
    {
        /// <summary>Upstream HTTP status code, passed through verbatim.</summary>
        [JsonPropertyName("StatusCode")]
        public int StatusCode { get; set; }

        /// <summary>Upstream <c>Content-Type</c>, if any.</summary>
        [JsonPropertyName("ContentType")]
        public string? ContentType { get; set; }

        /// <summary>Raw upstream response body.</summary>
        [JsonPropertyName("Body")]
        public string Body { get; set; } = string.Empty;

        /// <summary>Response headers returned by the proxy (including passed-through upstream headers).</summary>
        [JsonPropertyName("Headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>Whether the upstream returned a 2xx status.</summary>
        [JsonIgnore]
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
    }
}
