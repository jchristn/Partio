namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class CompletionCallDetail
    {
        [JsonPropertyName("Url")]
        public string? Url { get; set; }

        [JsonPropertyName("Method")]
        public string? Method { get; set; }

        [JsonPropertyName("RequestHeaders")]
        public Dictionary<string, string>? RequestHeaders { get; set; }

        [JsonPropertyName("RequestBody")]
        public string? RequestBody { get; set; }

        [JsonPropertyName("StatusCode")]
        public int? StatusCode { get; set; }

        [JsonPropertyName("ResponseHeaders")]
        public Dictionary<string, string>? ResponseHeaders { get; set; }

        [JsonPropertyName("ResponseBody")]
        public string? ResponseBody { get; set; }

        [JsonPropertyName("ResponseTimeMs")]
        public long? ResponseTimeMs { get; set; }

        [JsonPropertyName("Success")]
        public bool Success { get; set; }

        [JsonPropertyName("Error")]
        public string? Error { get; set; }

        [JsonPropertyName("TimestampUtc")]
        public DateTime TimestampUtc { get; set; }
    }
}
