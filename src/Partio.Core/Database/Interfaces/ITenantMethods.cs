namespace Partio.Core.Database.Interfaces
{
    using Partio.Core.Models;

    /// <summary>
    /// Interface for tenant database operations.
    /// </summary>
    public interface ITenantMethods
    {
        /// <summary>Create a new tenant.</summary>
        Task<TenantMetadata> CreateAsync(TenantMetadata tenant, CancellationToken token = default);

        /// <summary>Read a tenant by ID.</summary>
        Task<TenantMetadata?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>Update an existing tenant.</summary>
        Task<TenantMetadata> UpdateAsync(TenantMetadata tenant, CancellationToken token = default);

        /// <summary>Delete a tenant by ID.</summary>
        Task DeleteByIdAsync(string id, CancellationToken token = default);

        /// <summary>Check if a tenant exists by ID.</summary>
        Task<bool> ExistsByIdAsync(string id, CancellationToken token = default);

        /// <summary>Enumerate tenants with pagination and filtering.</summary>
        Task<EnumerationResult<TenantMetadata>> EnumerateAsync(EnumerationRequest request, CancellationToken token = default);

        /// <summary>Count total tenants.</summary>
        Task<long> CountAsync(CancellationToken token = default);
    }
}
