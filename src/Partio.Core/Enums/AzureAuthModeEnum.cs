namespace Partio.Core.Enums
{
    /// <summary>
    /// How an Azure OpenAI endpoint authenticates.
    /// </summary>
    public enum AzureAuthModeEnum
    {
        /// <summary>The endpoint ApiKey is sent in the api-key header.</summary>
        ApiKey,
        /// <summary>The endpoint ApiKey is an Entra ID (Azure AD) access token sent as a bearer token.</summary>
        BearerToken
    }
}
