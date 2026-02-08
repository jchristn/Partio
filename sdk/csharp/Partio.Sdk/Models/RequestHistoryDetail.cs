namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class RequestHistoryDetail
    {
        [JsonPropertyName("RequestHeaders")]
        public Dictionary<string, string>? RequestHeaders { get; set; }

        [JsonPropertyName("RequestBody")]
        public string? RequestBody { get; set; }

        [JsonPropertyName("ResponseHeaders")]
        public Dictionary<string, string>? ResponseHeaders { get; set; }

        [JsonPropertyName("ResponseBody")]
        public string? ResponseBody { get; set; }

        [JsonPropertyName("EmbeddingCalls")]
        public List<EmbeddingCallDetail>? EmbeddingCalls { get; set; }
    }
}
