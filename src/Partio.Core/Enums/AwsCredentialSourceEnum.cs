namespace Partio.Core.Enums
{
    /// <summary>
    /// Where an AWS Bedrock endpoint obtains the credentials used to sign requests.
    /// </summary>
    public enum AwsCredentialSourceEnum
    {
        /// <summary>Access key ID, secret access key, and optional session token stored on the endpoint.</summary>
        Static,
        /// <summary>AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY, and AWS_SESSION_TOKEN read from the Partio server environment.</summary>
        Environment
    }
}
