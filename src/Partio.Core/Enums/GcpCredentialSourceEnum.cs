namespace Partio.Core.Enums
{
    /// <summary>
    /// Where a Google Vertex AI endpoint obtains OAuth bearer tokens.
    /// </summary>
    public enum GcpCredentialSourceEnum
    {
        /// <summary>A service-account key JSON stored on the endpoint; tokens are minted and refreshed automatically.</summary>
        ServiceAccountJson,
        /// <summary>Application Default Credentials on the Partio server (GOOGLE_APPLICATION_CREDENTIALS or the metadata server).</summary>
        ApplicationDefault,
        /// <summary>A static access token stored in the endpoint ApiKey (for example from gcloud auth print-access-token). Tokens expire.</summary>
        AccessToken
    }
}
