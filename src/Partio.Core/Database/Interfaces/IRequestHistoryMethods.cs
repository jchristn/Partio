namespace Partio.Core.Database.Interfaces
{
    using Partio.Core.Models;

    /// <summary>
    /// Interface for request history database operations.
    /// </summary>
    public interface IRequestHistoryMethods
    {
        /// <summary>Create a new request history entry.</summary>
        Task<RequestHistoryEntry> CreateAsync(RequestHistoryEntry entry, CancellationToken token = default);

        /// <summary>Update a request history entry.</summary>
        Task<RequestHistoryEntry> UpdateAsync(RequestHistoryEntry entry, CancellationToken token = default);

        /// <summary>Read a request history entry by ID.</summary>
        Task<RequestHistoryEntry?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>Enumerate request history with pagination, scoped to a tenant.</summary>
        Task<EnumerationResult<RequestHistoryEntry>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);

        /// <summary>Enumerate all request history with pagination (no tenant filter).</summary>
        Task<EnumerationResult<RequestHistoryEntry>> EnumerateAllAsync(EnumerationRequest request, CancellationToken token = default);

        /// <summary>Delete a request history entry by ID.</summary>
        Task DeleteByIdAsync(string id, CancellationToken token = default);

        /// <summary>Delete expired request history entries.</summary>
        Task DeleteExpiredAsync(DateTime cutoff, CancellationToken token = default);

        /// <summary>Get object keys of expired entries for filesystem cleanup.</summary>
        Task<List<string>> GetExpiredObjectKeysAsync(DateTime cutoff, CancellationToken token = default);

        /// <summary>Count request history entries in a tenant.</summary>
        Task<long> CountAsync(string tenantId, CancellationToken token = default);
    }
}
