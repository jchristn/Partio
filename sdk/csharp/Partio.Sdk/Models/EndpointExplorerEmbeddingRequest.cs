namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class EndpointExplorerEmbeddingRequest
    {
        [JsonPropertyName("EndpointId")]
        public string EndpointId { get; set; } = string.Empty;

        [JsonPropertyName("Input")]
        public string Input { get; set; } = string.Empty;

        [JsonPropertyName("L2Normalization")]
        public bool L2Normalization { get; set; } = false;
    }
}
