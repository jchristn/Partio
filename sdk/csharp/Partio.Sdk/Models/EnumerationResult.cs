namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class EnumerationResult<T>
    {
        [JsonPropertyName("Data")]
        public List<T> Data { get; set; } = new List<T>();

        [JsonPropertyName("ContinuationToken")]
        public string? ContinuationToken { get; set; }

        [JsonPropertyName("TotalCount")]
        public long? TotalCount { get; set; }

        [JsonPropertyName("HasMore")]
        public bool HasMore { get; set; }
    }
}
