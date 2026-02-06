namespace Partio.Sdk.Models
{
    using System.Text.Json.Serialization;

    public class EnumerationRequest
    {
        [JsonPropertyName("MaxResults")]
        public int MaxResults { get; set; } = 100;

        [JsonPropertyName("ContinuationToken")]
        public string? ContinuationToken { get; set; }

        [JsonPropertyName("Order")]
        public string Order { get; set; } = "CreatedDescending";

        [JsonPropertyName("NameFilter")]
        public string? NameFilter { get; set; }

        [JsonPropertyName("LabelFilter")]
        public string? LabelFilter { get; set; }

        [JsonPropertyName("TagKeyFilter")]
        public string? TagKeyFilter { get; set; }

        [JsonPropertyName("TagValueFilter")]
        public string? TagValueFilter { get; set; }

        [JsonPropertyName("ActiveFilter")]
        public bool? ActiveFilter { get; set; }
    }
}
