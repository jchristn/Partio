namespace Partio.Core.Database.Interfaces
{
    using Partio.Core.Models;

    /// <summary>
    /// Interface for completion endpoint database operations.
    /// </summary>
    public interface ICompletionEndpointMethods
    {
        /// <summary>Create a new completion endpoint.</summary>
        Task<CompletionEndpoint> CreateAsync(CompletionEndpoint endpoint, CancellationToken token = default);

        /// <summary>Read a completion endpoint by ID.</summary>
        Task<CompletionEndpoint?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>Update an existing completion endpoint.</summary>
        Task<CompletionEndpoint> UpdateAsync(CompletionEndpoint endpoint, CancellationToken token = default);

        /// <summary>Delete a completion endpoint by ID.</summary>
        Task DeleteByIdAsync(string id, CancellationToken token = default);

        /// <summary>Check if a completion endpoint exists by ID.</summary>
        Task<bool> ExistsByIdAsync(string id, CancellationToken token = default);

        /// <summary>Enumerate completion endpoints with pagination, scoped to a tenant.</summary>
        Task<EnumerationResult<CompletionEndpoint>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);

        /// <summary>Count completion endpoints in a tenant.</summary>
        Task<long> CountAsync(string tenantId, CancellationToken token = default);
    }
}
