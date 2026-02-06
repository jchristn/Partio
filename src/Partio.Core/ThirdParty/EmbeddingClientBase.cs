namespace Partio.Core.ThirdParty
{
    using SyslogLogging;

    /// <summary>
    /// Abstract base class for embedding API clients.
    /// </summary>
    public abstract class EmbeddingClientBase : IDisposable
    {
        /// <summary>
        /// Logging module.
        /// </summary>
        protected readonly LoggingModule _Logging;

        /// <summary>
        /// Endpoint URL.
        /// </summary>
        protected readonly string _Endpoint;

        /// <summary>
        /// API key (nullable).
        /// </summary>
        protected readonly string? _ApiKey;

        /// <summary>
        /// HTTP client for API requests.
        /// </summary>
        protected readonly HttpClient _HttpClient;

        /// <summary>
        /// Header prefix for log messages.
        /// </summary>
        protected string _Header = "[EmbeddingClient] ";

        /// <summary>
        /// Initialize a new EmbeddingClientBase.
        /// </summary>
        /// <param name="endpoint">Endpoint URL.</param>
        /// <param name="apiKey">API key (nullable).</param>
        /// <param name="logging">Logging module.</param>
        protected EmbeddingClientBase(string endpoint, string? apiKey, LoggingModule logging)
        {
            _Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _ApiKey = apiKey;
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _HttpClient = new HttpClient();
        }

        /// <summary>
        /// Generate embeddings for a single text.
        /// </summary>
        /// <param name="text">Input text.</param>
        /// <param name="model">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Embedding vector.</returns>
        public abstract Task<List<float>> EmbedAsync(string text, string model, CancellationToken token = default);

        /// <summary>
        /// Generate embeddings for a batch of texts.
        /// </summary>
        /// <param name="texts">Input texts.</param>
        /// <param name="model">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of embedding vectors.</returns>
        public abstract Task<List<List<float>>> EmbedBatchAsync(List<string> texts, string model, CancellationToken token = default);

        /// <summary>
        /// Apply L2 normalization to an embedding vector.
        /// </summary>
        /// <param name="embeddings">Embedding vector to normalize.</param>
        /// <returns>L2-normalized embedding vector.</returns>
        public List<float> NormalizeL2(List<float> embeddings)
        {
            if (embeddings == null || embeddings.Count == 0) return embeddings ?? new List<float>();

            double magnitude = 0.0;
            foreach (float val in embeddings)
            {
                magnitude += (double)val * val;
            }
            magnitude = Math.Sqrt(magnitude);

            if (magnitude == 0.0) return embeddings;

            List<float> normalized = new List<float>(embeddings.Count);
            foreach (float val in embeddings)
            {
                normalized.Add((float)(val / magnitude));
            }

            return normalized;
        }

        /// <summary>
        /// Dispose of HTTP client resources.
        /// </summary>
        public void Dispose()
        {
            _HttpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
