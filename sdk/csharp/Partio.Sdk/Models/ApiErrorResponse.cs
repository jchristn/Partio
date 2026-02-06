namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class ApiErrorResponse
    {
        [JsonPropertyName("Error")]
        public string Error { get; set; } = string.Empty;

        [JsonPropertyName("Message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("StatusCode")]
        public int StatusCode { get; set; }

        [JsonPropertyName("TimestampUtc")]
        public DateTime TimestampUtc { get; set; }
    }
}
