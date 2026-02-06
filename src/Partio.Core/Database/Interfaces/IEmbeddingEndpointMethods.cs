namespace Partio.Core.Database.Interfaces
{
    using Partio.Core.Models;

    /// <summary>
    /// Interface for embedding endpoint database operations.
    /// </summary>
    public interface IEmbeddingEndpointMethods
    {
        /// <summary>Create a new embedding endpoint.</summary>
        Task<EmbeddingEndpoint> CreateAsync(EmbeddingEndpoint endpoint, CancellationToken token = default);

        /// <summary>Read an embedding endpoint by ID.</summary>
        Task<EmbeddingEndpoint?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>Read an embedding endpoint by model name within a tenant.</summary>
        Task<EmbeddingEndpoint?> ReadByModelAsync(string tenantId, string model, CancellationToken token = default);

        /// <summary>Update an existing embedding endpoint.</summary>
        Task<EmbeddingEndpoint> UpdateAsync(EmbeddingEndpoint endpoint, CancellationToken token = default);

        /// <summary>Delete an embedding endpoint by ID.</summary>
        Task DeleteByIdAsync(string id, CancellationToken token = default);

        /// <summary>Check if an embedding endpoint exists by ID.</summary>
        Task<bool> ExistsByIdAsync(string id, CancellationToken token = default);

        /// <summary>Enumerate embedding endpoints with pagination, scoped to a tenant.</summary>
        Task<EnumerationResult<EmbeddingEndpoint>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);

        /// <summary>Count embedding endpoints in a tenant.</summary>
        Task<long> CountAsync(string tenantId, CancellationToken token = default);
    }
}
