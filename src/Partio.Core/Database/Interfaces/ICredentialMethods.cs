namespace Partio.Core.Database.Interfaces
{
    using Partio.Core.Models;

    /// <summary>
    /// Interface for credential database operations.
    /// </summary>
    public interface ICredentialMethods
    {
        /// <summary>Create a new credential.</summary>
        Task<Credential> CreateAsync(Credential credential, CancellationToken token = default);

        /// <summary>Read a credential by ID.</summary>
        Task<Credential?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>Read a credential by bearer token.</summary>
        Task<Credential?> ReadByBearerTokenAsync(string bearerToken, CancellationToken token = default);

        /// <summary>Update an existing credential.</summary>
        Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default);

        /// <summary>Delete a credential by ID.</summary>
        Task DeleteByIdAsync(string id, CancellationToken token = default);

        /// <summary>Check if a credential exists by ID.</summary>
        Task<bool> ExistsByIdAsync(string id, CancellationToken token = default);

        /// <summary>Enumerate credentials with pagination and filtering, scoped to a tenant.</summary>
        Task<EnumerationResult<Credential>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);

        /// <summary>Count credentials in a tenant.</summary>
        Task<long> CountAsync(string tenantId, CancellationToken token = default);
    }
}
