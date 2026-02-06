namespace Partio.Core
{
    using System.Security.Cryptography;

    /// <summary>
    /// Generates k-sortable unique IDs with semantic prefixes using PrettyId.
    /// </summary>
    public static class IdGenerator
    {
        private static readonly string _AlphanumericChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private static readonly PrettyId.IdGenerator _Generator = new PrettyId.IdGenerator();

        /// <summary>
        /// Generate a new tenant ID with prefix 'ten_'.
        /// </summary>
        /// <returns>A k-sortable unique ID.</returns>
        public static string NewTenantId()
        {
            return _Generator.Generate(Constants.TenantIdPrefix, 48);
        }

        /// <summary>
        /// Generate a new user ID with prefix 'usr_'.
        /// </summary>
        /// <returns>A k-sortable unique ID.</returns>
        public static string NewUserId()
        {
            return _Generator.Generate(Constants.UserIdPrefix, 48);
        }

        /// <summary>
        /// Generate a new credential ID with prefix 'cred_'.
        /// </summary>
        /// <returns>A k-sortable unique ID.</returns>
        public static string NewCredentialId()
        {
            return _Generator.Generate(Constants.CredentialIdPrefix, 48);
        }

        /// <summary>
        /// Generate a new embedding endpoint ID with prefix 'ep_'.
        /// </summary>
        /// <returns>A k-sortable unique ID.</returns>
        public static string NewEmbeddingEndpointId()
        {
            return _Generator.Generate(Constants.EmbeddingEndpointIdPrefix, 48);
        }

        /// <summary>
        /// Generate a new request history ID with prefix 'req_'.
        /// </summary>
        /// <returns>A k-sortable unique ID.</returns>
        public static string NewRequestHistoryId()
        {
            return _Generator.Generate(Constants.RequestHistoryIdPrefix, 48);
        }

        /// <summary>
        /// Generate a 64-character random alphanumeric bearer token.
        /// </summary>
        /// <returns>A 64-character random alphanumeric token.</returns>
        public static string NewBearerToken()
        {
            char[] token = new char[64];
            byte[] randomBytes = RandomNumberGenerator.GetBytes(64);

            for (int i = 0; i < 64; i++)
            {
                token[i] = _AlphanumericChars[randomBytes[i] % _AlphanumericChars.Length];
            }

            return new string(token);
        }
    }
}
