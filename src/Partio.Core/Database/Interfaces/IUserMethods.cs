namespace Partio.Core.Database.Interfaces
{
    using Partio.Core.Models;

    /// <summary>
    /// Interface for user database operations.
    /// </summary>
    public interface IUserMethods
    {
        /// <summary>Create a new user.</summary>
        Task<UserMaster> CreateAsync(UserMaster user, CancellationToken token = default);

        /// <summary>Read a user by ID.</summary>
        Task<UserMaster?> ReadByIdAsync(string id, CancellationToken token = default);

        /// <summary>Read a user by email within a tenant.</summary>
        Task<UserMaster?> ReadByEmailAsync(string tenantId, string email, CancellationToken token = default);

        /// <summary>Update an existing user.</summary>
        Task<UserMaster> UpdateAsync(UserMaster user, CancellationToken token = default);

        /// <summary>Delete a user by ID.</summary>
        Task DeleteByIdAsync(string id, CancellationToken token = default);

        /// <summary>Check if a user exists by ID.</summary>
        Task<bool> ExistsByIdAsync(string id, CancellationToken token = default);

        /// <summary>Enumerate users with pagination and filtering, scoped to a tenant.</summary>
        Task<EnumerationResult<UserMaster>> EnumerateAsync(string tenantId, EnumerationRequest request, CancellationToken token = default);

        /// <summary>Count users in a tenant.</summary>
        Task<long> CountAsync(string tenantId, CancellationToken token = default);
    }
}
