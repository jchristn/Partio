namespace Partio.Core.Proxy
{
    using System;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.ThirdParty;

    /// <summary>
    /// Policy helpers for the transparent completion proxy (<c>/v1.0/proxy/{endpointId}/...</c>).
    /// The proxy performs no request/response translation: an endpoint speaks exactly one provider
    /// dialect (its <see cref="ApiFormatEnum"/>), and only that dialect's native sub-paths are relayed.
    /// A request for another provider's sub-path is rejected rather than forwarded, keeping the proxy a
    /// completion relay rather than an open reverse proxy.
    /// </summary>
    public static class ProxyPathPolicy
    {
        /// <summary>
        /// Normalize a captured proxy sub-path by trimming surrounding slashes and whitespace so it can
        /// be compared against the per-format allow-list. Any query string must already be removed.
        /// </summary>
        /// <param name="subpath">Raw sub-path captured after <c>/v1.0/proxy/{endpointId}/</c>.</param>
        /// <returns>The normalized sub-path (for example <c>api/chat</c> or <c>v1/chat/completions</c>).</returns>
        public static string Normalize(string? subpath)
        {
            if (string.IsNullOrWhiteSpace(subpath)) return string.Empty;
            return subpath.Trim().Trim('/');
        }

        /// <summary>
        /// Determine whether a native sub-path is permitted for the supplied provider dialect. The proxy
        /// only relays completion/inference operations (chat, generate, completions, embeddings) plus the
        /// benign model-discovery endpoints; every other path is rejected.
        /// </summary>
        /// <param name="format">The endpoint's provider dialect.</param>
        /// <param name="subpath">Raw or normalized sub-path.</param>
        /// <returns><c>true</c> when the path may be forwarded to the upstream provider.</returns>
        public static bool IsAllowed(ApiFormatEnum format, string? subpath)
        {
            string p = Normalize(subpath);
            if (p.Length == 0) return false;

            switch (format)
            {
                case ApiFormatEnum.Ollama:
                case ApiFormatEnum.OllamaCloud:
                    return p == "api/chat"
                        || p == "api/generate"
                        || p == "api/embed"
                        || p == "api/embeddings"
                        || p == "api/tags"
                        || p == "api/show"
                        || p == "api/ps"
                        || p == "api/version";

                case ApiFormatEnum.OpenAI:
                case ApiFormatEnum.vLLM:
                    return p == "v1/chat/completions"
                        || p == "v1/completions"
                        || p == "v1/embeddings"
                        || p == "v1/models"
                        || p.StartsWith("v1/models/", StringComparison.Ordinal);

                case ApiFormatEnum.xAI:
                    // xAI's native Responses API plus its OpenAI-compatible chat, embeddings, and model catalogs.
                    return p == "v1/responses"
                        || p == "v1/chat/completions"
                        || p == "v1/embeddings"
                        || p == "v1/models"
                        || p.StartsWith("v1/models/", StringComparison.Ordinal)
                        || p == "v1/language-models"
                        || p == "v1/embedding-models";

                case ApiFormatEnum.Gemini:
                    // Gemini encodes the model and operation in the path
                    // (for example v1beta/models/gemini-1.5-flash:generateContent), so match by prefix.
                    return p == "v1beta/models" || p.StartsWith("v1beta/models/", StringComparison.Ordinal);

                case ApiFormatEnum.AzureOpenAI:
                    // Deployment-scoped routes (openai/deployments/{deployment}/...) and the v1 GA routes.
                    return IsAzureDeploymentPath(p)
                        || p == "openai/models"
                        || p == "openai/v1/chat/completions"
                        || p == "openai/v1/embeddings"
                        || p == "openai/v1/responses"
                        || p == "openai/v1/models";

                case ApiFormatEnum.Anthropic:
                    return p == "v1/messages"
                        || p == "v1/messages/count_tokens"
                        || p == "v1/models"
                        || p.StartsWith("v1/models/", StringComparison.Ordinal);

                case ApiFormatEnum.Cohere:
                    return p == "v2/chat"
                        || p == "v2/embed"
                        || p == "v1/models"
                        || p.StartsWith("v1/models/", StringComparison.Ordinal);

                default:
                    // Bedrock and Vertex AI requests must be signed per request, so they are never relayed.
                    // TEI and VoyageAI are embedding-only and cannot back a completion endpoint.
                    return false;
            }
        }

        /// <summary>
        /// Human-readable description of the sub-paths permitted for a dialect, used in 404 error bodies
        /// so a caller who targets the wrong provider's path gets an actionable message.
        /// </summary>
        /// <param name="format">The endpoint's provider dialect.</param>
        /// <returns>A comma-separated list of allowed native sub-paths.</returns>
        public static string DescribeAllowed(ApiFormatEnum format)
        {
            switch (format)
            {
                case ApiFormatEnum.Ollama:
                case ApiFormatEnum.OllamaCloud:
                    return "api/chat, api/generate, api/embed, api/embeddings, api/tags, api/show, api/ps, api/version";
                case ApiFormatEnum.OpenAI:
                case ApiFormatEnum.vLLM:
                    return "v1/chat/completions, v1/completions, v1/embeddings, v1/models, v1/models/{model}";
                case ApiFormatEnum.Gemini:
                    return "v1beta/models, v1beta/models/{model}:{method}";
                case ApiFormatEnum.xAI:
                    return "v1/responses, v1/chat/completions, v1/embeddings, v1/models, v1/models/{model}, v1/language-models, v1/embedding-models";
                case ApiFormatEnum.AzureOpenAI:
                    return "openai/deployments/{deployment}/chat/completions, openai/deployments/{deployment}/completions, openai/deployments/{deployment}/embeddings, openai/models, openai/v1/chat/completions, openai/v1/embeddings, openai/v1/responses, openai/v1/models";
                case ApiFormatEnum.Anthropic:
                    return "v1/messages, v1/messages/count_tokens, v1/models, v1/models/{model}";
                case ApiFormatEnum.Cohere:
                    return "v2/chat, v2/embed, v1/models, v1/models/{model}";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Build the absolute upstream URL for a proxied request by joining the endpoint base URL, the
        /// normalized native sub-path, and the original query string (if any).
        /// </summary>
        /// <param name="endpointBaseUrl">The endpoint's configured base URL (for example http://localhost:11434).</param>
        /// <param name="subpath">Native sub-path to append.</param>
        /// <param name="queryString">Original query string, with or without a leading '?'. May be null/empty.</param>
        /// <returns>The absolute upstream URL to forward to.</returns>
        public static string BuildUpstreamUrl(string endpointBaseUrl, string? subpath, string? queryString)
        {
            string baseUrl = (endpointBaseUrl ?? string.Empty).TrimEnd('/');
            string p = Normalize(subpath);
            string url = baseUrl + "/" + p;

            if (!string.IsNullOrEmpty(queryString))
                url += queryString!.StartsWith("?", StringComparison.Ordinal) ? queryString : "?" + queryString;

            return url;
        }

        /// <summary>
        /// Resolve the primary upstream authentication header Partio injects on the caller's behalf, based on the
        /// provider dialect. Gemini uses <c>x-goog-api-key</c>, Anthropic uses <c>x-api-key</c>, Azure OpenAI uses
        /// <c>api-key</c>, and other providers use <c>Authorization: Bearer</c>. Returns null when the endpoint has
        /// no API key configured. Use <see cref="ResolveAuthHeaders"/> for the full header set.
        /// </summary>
        /// <param name="format">The endpoint's provider dialect.</param>
        /// <param name="apiKey">The endpoint's configured upstream API key, if any.</param>
        /// <returns>The header name and value to inject, or null when no key is configured.</returns>
        public static ProxyAuthHeader? ResolveAuthHeader(ApiFormatEnum format, string? apiKey)
        {
            List<ProxyAuthHeader> headers = ResolveAuthHeaders(format, apiKey, null);
            return headers.Count > 0 ? headers[0] : null;
        }

        /// <summary>
        /// Resolve every upstream authentication header Partio injects for an endpoint, including provider
        /// companions such as <c>anthropic-version</c>. Returns an empty list when no credential applies.
        /// </summary>
        /// <param name="format">The endpoint's provider dialect.</param>
        /// <param name="apiKey">The endpoint's configured upstream API key, if any.</param>
        /// <param name="settings">The endpoint's provider settings, if any.</param>
        /// <returns>The headers to inject.</returns>
        public static List<ProxyAuthHeader> ResolveAuthHeaders(ApiFormatEnum format, string? apiKey, ProviderSettings? settings)
        {
            List<ProxyAuthHeader> headers = new List<ProxyAuthHeader>();
            foreach (KeyValuePair<string, string> header in ProviderDefaults.ResolveAuthHeaders(format, apiKey, settings))
                headers.Add(new ProxyAuthHeader(header.Key, header.Value));
            return headers;
        }

        /// <summary>
        /// Append the endpoint's Azure OpenAI api-version to a query string that does not already carry one.
        /// Other dialects return the query string unchanged.
        /// </summary>
        /// <param name="format">The endpoint's provider dialect.</param>
        /// <param name="queryString">Original query string, with or without a leading '?'. May be null/empty.</param>
        /// <param name="settings">The endpoint's provider settings, if any.</param>
        /// <returns>The query string to forward.</returns>
        public static string? ApplyRequiredQuery(ApiFormatEnum format, string? queryString, ProviderSettings? settings)
        {
            if (format != ApiFormatEnum.AzureOpenAI) return queryString;
            if (!string.IsNullOrEmpty(queryString) && queryString.IndexOf("api-version=", StringComparison.OrdinalIgnoreCase) >= 0)
                return queryString;

            string apiVersion = "api-version=" + Uri.EscapeDataString(settings?.AzureOpenAI?.ApiVersion ?? ProviderDefaults.AzureOpenAiApiVersion);
            if (string.IsNullOrEmpty(queryString) || queryString == "?") return "?" + apiVersion;
            return (queryString.StartsWith("?", StringComparison.Ordinal) ? queryString : "?" + queryString) + "&" + apiVersion;
        }

        private static bool IsAzureDeploymentPath(string p)
        {
            const string prefix = "openai/deployments/";
            if (!p.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string rest = p.Substring(prefix.Length);
            int slash = rest.IndexOf('/');
            if (slash <= 0) return false;
            string operation = rest.Substring(slash + 1);
            return operation == "chat/completions" || operation == "completions" || operation == "embeddings";
        }
    }

    /// <summary>
    /// An upstream authentication header (name and value) that Partio injects when forwarding a proxied request.
    /// </summary>
    public readonly struct ProxyAuthHeader
    {
        /// <summary>Header name (for example <c>Authorization</c> or <c>x-goog-api-key</c>).</summary>
        public string Name { get; }

        /// <summary>Header value (for example <c>Bearer sk-...</c>).</summary>
        public string Value { get; }

        /// <summary>Initialize a new <see cref="ProxyAuthHeader"/>.</summary>
        /// <param name="name">Header name.</param>
        /// <param name="value">Header value.</param>
        public ProxyAuthHeader(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}
