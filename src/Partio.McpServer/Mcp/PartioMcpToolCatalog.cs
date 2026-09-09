namespace Partio.McpServer.Mcp
{
    using Partio.McpServer.Settings;
    using Partio.Sdk;
    using Partio.Sdk.Models;
    using SyslogLogging;
    using Voltaic.Core;
    using Voltaic.Mcp;

    /// <summary>
    /// Defines and registers the Partio MCP tool surface. Each tool parses its arguments from
    /// <see cref="RpcParameters"/>, calls Partio over the REST SDK, and returns a bounded structured result.
    /// </summary>
    public class PartioMcpToolCatalog
    {
        private readonly PartioClient _Client;
        private readonly McpServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[McpTools] ";

        /// <summary>
        /// Initialize a new PartioMcpToolCatalog.
        /// </summary>
        /// <param name="client">Partio REST SDK client used for outbound calls.</param>
        /// <param name="settings">MCP server settings.</param>
        /// <param name="logging">Logging module.</param>
        public PartioMcpToolCatalog(PartioClient client, McpServerSettings settings, LoggingModule logging)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Register every Partio tool on the supplied MCP server.
        /// </summary>
        /// <param name="server">The MCP HTTP server to register tools on.</param>
        public void RegisterAll(McpHttpServer server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.RegisterTool(
                "partio_capabilities",
                "Return the Partio MCP server version, the connected Partio server health, the MCP protocol version, and the list of available tools. Requires only authentication.",
                SchemaNoArgs(),
                (parameters, token) => CapabilitiesAsync(token));

            RegisterEndpointTools(
                server,
                "completion",
                id => _Client.GetCompletionEndpointAsync(id),
                req => _Client.EnumerateCompletionEndpointsAsync(req),
                ep => _Client.CreateCompletionEndpointAsync(ep),
                (id, ep) => _Client.UpdateCompletionEndpointAsync(id, ep),
                id => _Client.DeleteCompletionEndpointAsync(id));

            RegisterEmbeddingTools(server);
            RegisterInferenceTools(server);
        }

        // ---------------- Completion endpoint tools ----------------

        private void RegisterEndpointTools(
            McpHttpServer server,
            string kind,
            Func<string, Task<CompletionEndpoint?>> get,
            Func<EnumerationRequest, Task<EnumerationResult<CompletionEndpoint>?>> enumerate,
            Func<CompletionEndpoint, Task<CompletionEndpoint?>> create,
            Func<string, CompletionEndpoint, Task<CompletionEndpoint?>> update,
            Func<string, Task> delete)
        {
            server.RegisterTool(
                "partio_enumerate_completion_endpoints",
                "List completion (inference) endpoints for the authenticated tenant as a bounded summary list. Supports maxResults (clamped), continuationToken paging, and an optional name search.",
                SchemaEnumerate(),
                async (parameters, token) =>
                {
                    EnumerationRequest req = BuildEnumerationRequest(parameters);
                    EnumerationResult<CompletionEndpoint>? result = await enumerate(req).ConfigureAwait(false);
                    return McpToolCallResult.FromStructured(new
                    {
                        Data = (result?.Data ?? new List<CompletionEndpoint>()).Select(SummarizeCompletion).ToList(),
                        result?.ContinuationToken,
                        result?.TotalCount,
                        HasMore = result?.HasMore ?? false
                    });
                });

            server.RegisterTool(
                "partio_get_completion_endpoint",
                "Fetch a single completion endpoint by id, including MaxConcurrentRequests and MaxQueueDepth.",
                SchemaId(),
                async (parameters, token) =>
                {
                    string id = RequireString(parameters, "id");
                    CompletionEndpoint? ep = await get(id).ConfigureAwait(false);
                    if (ep == null) return Error("Completion endpoint '" + id + "' was not found.");
                    return McpToolCallResult.FromStructured(ep);
                });

            server.RegisterTool(
                "partio_create_completion_endpoint",
                "Create a completion endpoint. Accepts the full endpoint definition including MaxConcurrentRequests and MaxQueueDepth (integer, default 0, clamped >= 0).",
                SchemaCompletionEndpoint(false),
                async (parameters, token) =>
                {
                    CompletionEndpoint ep = NonNull(parameters).Deserialize<CompletionEndpoint>() ?? new CompletionEndpoint();
                    CompletionEndpoint? created = await create(ep).ConfigureAwait(false);
                    return McpToolCallResult.FromStructured(created);
                });

            server.RegisterTool(
                "partio_update_completion_endpoint",
                "Update an existing completion endpoint by id. Accepts the full endpoint definition including MaxConcurrentRequests and MaxQueueDepth.",
                SchemaCompletionEndpoint(true),
                async (parameters, token) =>
                {
                    string id = RequireString(parameters, "id");
                    CompletionEndpoint ep = NonNull(parameters).Deserialize<CompletionEndpoint>() ?? new CompletionEndpoint();
                    CompletionEndpoint? updated = await update(id, ep).ConfigureAwait(false);
                    if (updated == null) return Error("Completion endpoint '" + id + "' was not found.");
                    return McpToolCallResult.FromStructured(updated);
                });

            server.RegisterTool(
                "partio_delete_completion_endpoint",
                "Delete a completion endpoint by id.",
                SchemaId(),
                async (parameters, token) =>
                {
                    string id = RequireString(parameters, "id");
                    await delete(id).ConfigureAwait(false);
                    return McpToolCallResult.FromStructured(new { Deleted = true, Id = id });
                });
        }

        // ---------------- Embedding endpoint tools ----------------

        private void RegisterEmbeddingTools(McpHttpServer server)
        {
            server.RegisterTool(
                "partio_enumerate_embedding_endpoints",
                "List embedding endpoints for the authenticated tenant as a bounded summary list. Supports maxResults (clamped), continuationToken paging, and an optional name search.",
                SchemaEnumerate(),
                async (parameters, token) =>
                {
                    EnumerationRequest req = BuildEnumerationRequest(parameters);
                    EnumerationResult<EmbeddingEndpoint>? result = await _Client.EnumerateEndpointsAsync(req).ConfigureAwait(false);
                    return McpToolCallResult.FromStructured(new
                    {
                        Data = (result?.Data ?? new List<EmbeddingEndpoint>()).Select(SummarizeEmbedding).ToList(),
                        result?.ContinuationToken,
                        result?.TotalCount,
                        HasMore = result?.HasMore ?? false
                    });
                });

            server.RegisterTool(
                "partio_get_embedding_endpoint",
                "Fetch a single embedding endpoint by id, including MaxConcurrentRequests and MaxQueueDepth.",
                SchemaId(),
                async (parameters, token) =>
                {
                    string id = RequireString(parameters, "id");
                    EmbeddingEndpoint? ep = await _Client.GetEndpointAsync(id).ConfigureAwait(false);
                    if (ep == null) return Error("Embedding endpoint '" + id + "' was not found.");
                    return McpToolCallResult.FromStructured(ep);
                });

            server.RegisterTool(
                "partio_create_embedding_endpoint",
                "Create an embedding endpoint. Accepts the full endpoint definition including MaxConcurrentRequests and MaxQueueDepth (integer, default 0, clamped >= 0).",
                SchemaEmbeddingEndpoint(false),
                async (parameters, token) =>
                {
                    EmbeddingEndpoint ep = NonNull(parameters).Deserialize<EmbeddingEndpoint>() ?? new EmbeddingEndpoint();
                    EmbeddingEndpoint? created = await _Client.CreateEndpointAsync(ep).ConfigureAwait(false);
                    return McpToolCallResult.FromStructured(created);
                });

            server.RegisterTool(
                "partio_update_embedding_endpoint",
                "Update an existing embedding endpoint by id. Accepts the full endpoint definition including MaxConcurrentRequests and MaxQueueDepth.",
                SchemaEmbeddingEndpoint(true),
                async (parameters, token) =>
                {
                    string id = RequireString(parameters, "id");
                    EmbeddingEndpoint ep = NonNull(parameters).Deserialize<EmbeddingEndpoint>() ?? new EmbeddingEndpoint();
                    EmbeddingEndpoint? updated = await _Client.UpdateEndpointAsync(id, ep).ConfigureAwait(false);
                    if (updated == null) return Error("Embedding endpoint '" + id + "' was not found.");
                    return McpToolCallResult.FromStructured(updated);
                });

            server.RegisterTool(
                "partio_delete_embedding_endpoint",
                "Delete an embedding endpoint by id.",
                SchemaId(),
                async (parameters, token) =>
                {
                    string id = RequireString(parameters, "id");
                    await _Client.DeleteEndpointAsync(id).ConfigureAwait(false);
                    return McpToolCallResult.FromStructured(new { Deleted = true, Id = id });
                });
        }

        // ---------------- Inference companion tools ----------------

        private void RegisterInferenceTools(McpHttpServer server)
        {
            server.RegisterTool(
                "partio_summarize",
                "Summarize text through a completion endpoint (no chunking or embedding).",
                SchemaFreeform(),
                async (parameters, token) =>
                {
                    SummarizeRequest req = NonNull(parameters).Deserialize<SummarizeRequest>() ?? new SummarizeRequest();
                    SummarizeResponse? resp = await _Client.SummarizeAsync(req).ConfigureAwait(false);
                    return McpToolCallResult.FromStructured(resp);
                });

            server.RegisterTool(
                "partio_chunk",
                "Chunk a semantic cell into text chunks without embedding them.",
                SchemaFreeform(),
                async (parameters, token) =>
                {
                    ChunkRequest req = NonNull(parameters).Deserialize<ChunkRequest>() ?? new ChunkRequest();
                    ChunkResponse? resp = await _Client.ChunkAsync(req).ConfigureAwait(false);
                    return McpToolCallResult.FromStructured(resp);
                });

            server.RegisterTool(
                "partio_embed",
                "Embed one or more input strings through an embedding endpoint without chunking.",
                SchemaFreeform(),
                async (parameters, token) =>
                {
                    EmbedRequest req = NonNull(parameters).Deserialize<EmbedRequest>() ?? new EmbedRequest();
                    EmbedResponse? resp = await _Client.EmbedAsync(req).ConfigureAwait(false);
                    return McpToolCallResult.FromStructured(resp);
                });
        }

        // ---------------- Handlers / helpers ----------------

        private async Task<object> CapabilitiesAsync(CancellationToken token)
        {
            Dictionary<string, string>? health = null;
            try
            {
                health = await _Client.HealthAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "capabilities health probe failed: " + ex.Message);
            }

            return McpToolCallResult.FromStructured(new
            {
                Server = "partio-mcp",
                Version = typeof(PartioMcpToolCatalog).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                ProtocolVersion = "2025-06-18",
                PartioEndpoint = _Settings.PartioEndpoint,
                PartioHealth = health,
                MaxResults = _Settings.MaxResults,
                Tools = new[]
                {
                    "partio_capabilities",
                    "partio_enumerate_completion_endpoints", "partio_get_completion_endpoint",
                    "partio_create_completion_endpoint", "partio_update_completion_endpoint", "partio_delete_completion_endpoint",
                    "partio_enumerate_embedding_endpoints", "partio_get_embedding_endpoint",
                    "partio_create_embedding_endpoint", "partio_update_embedding_endpoint", "partio_delete_embedding_endpoint",
                    "partio_summarize", "partio_chunk", "partio_embed"
                }
            });
        }

        private EnumerationRequest BuildEnumerationRequest(RpcParameters? parameters)
        {
            RpcParameters p = NonNull(parameters);
            EnumerationRequest req = new EnumerationRequest();

            int max = _Settings.MaxResults;
            if (p.ContainsProperty("maxResults"))
            {
                long? requested = p.GetInt64("maxResults");
                if (requested.HasValue && requested.Value > 0 && requested.Value < max) max = (int)requested.Value;
            }
            req.MaxResults = max;

            if (p.ContainsProperty("continuationToken"))
                req.ContinuationToken = p.GetString("continuationToken");

            if (p.ContainsProperty("search"))
                req.NameFilter = p.GetString("search");

            return req;
        }

        private static object SummarizeCompletion(CompletionEndpoint ep)
        {
            return new
            {
                ep.Id,
                ep.Name,
                ep.Model,
                ep.Endpoint,
                ep.ApiFormat,
                ep.Active,
                ep.MaxConcurrentRequests,
                ep.MaxQueueDepth
            };
        }

        private static object SummarizeEmbedding(EmbeddingEndpoint ep)
        {
            return new
            {
                ep.Id,
                ep.Name,
                ep.Model,
                ep.Endpoint,
                ep.ApiFormat,
                ep.Active,
                ep.MaxConcurrentRequests,
                ep.MaxQueueDepth
            };
        }

        private static RpcParameters NonNull(RpcParameters? parameters)
        {
            return parameters ?? RpcParameters.FromObject(new { });
        }

        private static string RequireString(RpcParameters? parameters, string name)
        {
            RpcParameters p = NonNull(parameters);
            if (!p.ContainsProperty(name))
                throw new ArgumentException("Required parameter '" + name + "' is missing.");
            string? value = p.GetString(name);
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Required parameter '" + name + "' is missing.");
            return value;
        }

        private static McpToolCallResult Error(string message)
        {
            McpToolCallResult result = McpToolCallResult.FromText(message);
            result.IsError = true;
            return result;
        }

        // ---------------- Input schemas ----------------

        private static object SchemaNoArgs()
        {
            return new { type = "object", properties = new { }, additionalProperties = false };
        }

        private static object SchemaId()
        {
            return new
            {
                type = "object",
                properties = new { id = new { type = "string", description = "Endpoint identifier." } },
                required = new[] { "id" }
            };
        }

        private static object SchemaEnumerate()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    maxResults = new { type = "integer", description = "Maximum results to return (clamped by the server)." },
                    continuationToken = new { type = "string", description = "Continuation token from a previous page." },
                    search = new { type = "string", description = "Optional case-insensitive name filter." }
                }
            };
        }

        private static object SchemaFreeform()
        {
            return new { type = "object", additionalProperties = true };
        }

        private static object SchemaCompletionEndpoint(bool includeId)
        {
            return EndpointSchema(includeId);
        }

        private static object SchemaEmbeddingEndpoint(bool includeId)
        {
            return EndpointSchema(includeId);
        }

        private static object EndpointSchema(bool includeId)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                ["Name"] = new { type = "string" },
                ["Endpoint"] = new { type = "string", description = "Upstream provider base URL." },
                ["ApiFormat"] = new { type = "string", description = "Ollama, OpenAI, vLLM, or Gemini." },
                ["ApiKey"] = new { type = "string" },
                ["Model"] = new { type = "string" },
                ["Active"] = new { type = "boolean" },
                ["MaximumTimeoutMs"] = new { type = "integer" },
                ["MaxConcurrentRequests"] = new { type = "integer", description = "Maximum concurrent upstream requests (>= 1)." },
                ["MaxQueueDepth"] = new { type = "integer", description = "Requests allowed to wait for a slot once MaxConcurrentRequests is reached; 0 rejects immediately with 429. Clamped >= 0." }
            };

            List<string> required = new List<string> { "Endpoint", "Model" };
            if (includeId)
            {
                properties["id"] = new { type = "string", description = "Identifier of the endpoint to update." };
                required.Insert(0, "id");
            }

            return new
            {
                type = "object",
                properties,
                required = required.ToArray(),
                additionalProperties = true
            };
        }
    }
}
