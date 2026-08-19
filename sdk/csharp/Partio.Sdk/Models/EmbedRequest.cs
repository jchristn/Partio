namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for embedding one or more input strings without chunking.
    /// </summary>
    public class EmbedRequest
    {
        [JsonPropertyName("EndpointId")]
        public string EndpointId { get; set; } = string.Empty;

        [JsonPropertyName("Input")]
        public List<string> Input { get; set; } = new List<string>();

        [JsonPropertyName("L2Normalization")]
        public bool L2Normalization { get; set; } = false;
    }
}
