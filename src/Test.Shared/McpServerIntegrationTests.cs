namespace Test.Shared
{
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using Touchstone.Core;

    /// <summary>
    /// Integration suite for the Partio MCP server (<c>partio-mcp</c>, built on Voltaic 2.x). The suite's
    /// hooks start a self-hosted Partio server and the MCP server as a separate process, then drive the MCP
    /// server over raw HTTP the way real clients do: the plain JSON-RPC endpoint (<c>/rpc</c>), the
    /// handshake-era Streamable HTTP flow (<c>initialize</c> + session on <c>/mcp</c>), and the stateless
    /// <c>2026-07-28</c> sequence Claude Code 2.1.x sends (<c>server/discover</c>, <c>tools/list</c>,
    /// <c>tools/call</c>). Cases cover both directions of each Voltaic 2.0 behavior change: only Partio's
    /// tools are published, <c>ping</c> returns <c>{}</c>, tools are reachable only through
    /// <c>tools/call</c>, and <c>additionalProperties: false</c> schemas are enforced.
    /// </summary>
    public static class McpServerIntegrationTests
    {
        private const string SuiteId = "Mcp";
        private const string HandshakeVersion = "2025-11-25";
        private const string StatelessVersion = "2026-07-28";

        /// <summary>Every tool the Partio MCP server registers, and nothing else.</summary>
        public static readonly IReadOnlyList<string> ExpectedTools = new List<string>
        {
            "partio_capabilities",
            "partio_chunk",
            "partio_create_completion_endpoint",
            "partio_create_embedding_endpoint",
            "partio_delete_completion_endpoint",
            "partio_delete_embedding_endpoint",
            "partio_embed",
            "partio_enumerate_completion_endpoints",
            "partio_enumerate_embedding_endpoints",
            "partio_get_completion_endpoint",
            "partio_get_embedding_endpoint",
            "partio_proxy",
            "partio_summarize",
            "partio_update_completion_endpoint",
            "partio_update_embedding_endpoint"
        };

        private static readonly string[] _VoltaicDemoTools = new[] { "ping", "echo", "getTime", "getSessions", "getClients" };

        private static readonly HttpClient _Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        private static SelfHostedMcpServerEnvironment? _Mcp;
        private static string _AdminKey = "partioadmin";
        private static string _TestToken = "default";

        /// <summary>
        /// Build the MCP integration suite. The before hook starts Partio and the MCP server; the after hook
        /// stops both.
        /// </summary>
        /// <returns>The MCP integration suite descriptor.</returns>
        public static TestSuiteDescriptor SelfHostedSuite()
        {
            SelfHostedPartioTestEnvironment? partio = null;

            return new TestSuiteDescriptor(
                SuiteId,
                "Partio MCP server integration (self-hosted, Voltaic 2.x)",
                BuildCases(),
                beforeSuiteAsync: async ct =>
                {
                    partio = await SelfHostedPartioTestEnvironment.StartAsync(ct).ConfigureAwait(false);
                    _AdminKey = partio.AdminKey;
                    _TestToken = partio.TestToken;
                    _Mcp = await SelfHostedMcpServerEnvironment.StartAsync(partio.Endpoint, partio.AdminKey, ct).ConfigureAwait(false);
                },
                afterSuiteAsync: async ct =>
                {
                    if (_Mcp != null)
                    {
                        await _Mcp.DisposeAsync().ConfigureAwait(false);
                        _Mcp = null;
                    }

                    if (partio != null)
                    {
                        await partio.DisposeAsync().ConfigureAwait(false);
                        partio = null;
                    }
                });
        }

        private static List<TestCaseDescriptor> BuildCases()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            // Positive: transport and protocol surface
            cases.Add(TestCaseFactory.Async(SuiteId, "Health GET / without credentials", TestHealthAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Ping on /rpc returns empty object without credentials", TestPingRpcAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Ping on /mcp returns empty object without credentials", TestPingMcpAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Handshake initialize and tools/list on /mcp", TestHandshakeFlowAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "tools/list on /rpc lists exactly the Partio tools", TestToolsListRpcAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Stateless Claude Code sequence on /mcp", () => TestStatelessSequenceAsync("/mcp")));
            cases.Add(TestCaseFactory.Async(SuiteId, "Stateless Claude Code sequence on /rpc", () => TestStatelessSequenceAsync("/rpc")));

            // Positive: tool behavior
            cases.Add(TestCaseFactory.Async(SuiteId, "partio_capabilities matches tools/list", TestCapabilitiesAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Completion endpoint create/get/enumerate/delete via tools/call", TestCompletionCrudAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Tenant credential token is accepted and runs as the caller", TestTenantTokenAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Endpoint schemas accept extra endpoint fields", TestEndpointSchemaAllowsExtraFieldsAsync));

            // Negative: authentication
            cases.Add(TestCaseFactory.Async(SuiteId, "tools/list without bearer is 401", TestMissingBearerAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "tools/call with invalid bearer is 401", TestInvalidBearerAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Stateless tools/list without bearer is 401", TestStatelessMissingBearerAsync));

            // Negative: Voltaic 2.0 breaking changes
            cases.Add(TestCaseFactory.Async(SuiteId, "Bare tool method call returns -32601", TestBareToolCallAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Voltaic demo tools are not callable", TestDemoToolsNotCallableAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Undeclared argument to partio_capabilities is rejected", TestAdditionalPropertiesRejectedAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Missing required argument is rejected", TestMissingRequiredArgumentAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Wrong argument type is rejected", TestWrongArgumentTypeAsync));
            cases.Add(TestCaseFactory.Async(SuiteId, "Unknown completion endpoint returns tool error", TestUnknownEndpointAsync));

            return cases;
        }

        // ---------------- Positive: transport and protocol surface ----------------

        private static async Task TestHealthAsync()
        {
            using HttpResponseMessage response = await _Http.GetAsync(Mcp.BaseUrl + "/").ConfigureAwait(false);
            Check.True(response.IsSuccessStatusCode, "GET / should succeed without credentials but returned " + (int)response.StatusCode);
        }

        private static async Task TestPingRpcAsync()
        {
            McpResponse response = await PostAsync("/rpc", Body("ping", 1, null), null).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.OK, response.StatusCode, "ping should bypass authentication. Body: " + response.Body);
            AssertEmptyObjectResult(response, "ping");
        }

        private static async Task TestPingMcpAsync()
        {
            McpResponse response = await PostAsync("/mcp", Body("ping", 1, null), null, mcpAccept: true).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.OK, response.StatusCode, "ping on /mcp should bypass authentication. Body: " + response.Body);
            AssertEmptyObjectResult(response, "ping");
        }

        private static async Task TestHandshakeFlowAsync()
        {
            string auth = Bearer(_AdminKey);

            McpResponse init = await PostAsync("/mcp", Body("initialize", 1, new
            {
                protocolVersion = HandshakeVersion,
                capabilities = new { },
                clientInfo = new { name = "partio-tests", version = "1.0.0" }
            }), auth, mcpAccept: true).ConfigureAwait(false);

            Check.Equal(HttpStatusCode.OK, init.StatusCode, "initialize should succeed. Body: " + init.Body);
            JsonElement result = RequireResult(init, "initialize");
            Check.Equal("partio-mcp", result.GetProperty("serverInfo").GetProperty("name").GetString());
            Check.Equal(HandshakeVersion, result.GetProperty("protocolVersion").GetString());
            Check.True(result.GetProperty("capabilities").TryGetProperty("tools", out _), "initialize should advertise the tools capability.");
            Check.False(string.IsNullOrEmpty(init.SessionId), "initialize on /mcp should issue an MCP-Session-Id.");

            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                ["MCP-Session-Id"] = init.SessionId!,
                ["MCP-Protocol-Version"] = HandshakeVersion
            };

            McpResponse initialized = await PostAsync("/mcp", NotificationBody("notifications/initialized"), auth, mcpAccept: true, headers: headers).ConfigureAwait(false);
            Check.True((int)initialized.StatusCode is >= 200 and < 300, "notifications/initialized should be accepted. Status: " + (int)initialized.StatusCode);

            McpResponse tools = await PostAsync("/mcp", Body("tools/list", 2, new { }), auth, mcpAccept: true, headers: headers).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.OK, tools.StatusCode, "tools/list should succeed. Body: " + tools.Body);
            AssertExactlyPartioTools(RequireResult(tools, "tools/list"));

            McpResponse call = await PostAsync("/mcp", ToolCallBody(3, "partio_capabilities", new { }), auth, mcpAccept: true, headers: headers).ConfigureAwait(false);
            JsonElement callResult = RequireResult(call, "tools/call partio_capabilities");
            Check.False(IsToolError(callResult), "partio_capabilities should not report a tool error. Body: " + call.Body);
            Check.Equal("partio-mcp", callResult.GetProperty("structuredContent").GetProperty("Server").GetString());
        }

        private static async Task TestToolsListRpcAsync()
        {
            McpResponse response = await PostAsync("/rpc", Body("tools/list", 1, new { }), Bearer(_AdminKey)).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.OK, response.StatusCode, "tools/list should succeed. Body: " + response.Body);
            AssertExactlyPartioTools(RequireResult(response, "tools/list"));
        }

        private static async Task TestStatelessSequenceAsync(string path)
        {
            string auth = Bearer(_AdminKey);

            // 1. server/discover: the first request Claude Code 2.1.x sends; it never calls initialize.
            McpResponse discover = await PostStatelessAsync(path, "server/discover", "discover-1", null, null, auth).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.OK, discover.StatusCode, "server/discover should succeed on " + path + ". Body: " + discover.Body);
            JsonElement discoverResult = RequireResult(discover, "server/discover");
            Check.Equal("complete", discoverResult.GetProperty("resultType").GetString());
            Check.Contains(StatelessVersion, discoverResult.GetProperty("supportedVersions").EnumerateArray().Select(v => v.GetString() ?? "").ToList());
            Check.Null(discover.SessionId, "Stateless responses carry no session id.");

            // 2. tools/list: must carry resultType/ttlMs/cacheScope and exactly Partio's tools.
            McpResponse tools = await PostStatelessAsync(path, "tools/list", 0, null, null, auth).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.OK, tools.StatusCode, "stateless tools/list should succeed on " + path + ". Body: " + tools.Body);
            JsonElement toolsResult = RequireResult(tools, "tools/list");
            Check.Equal("complete", toolsResult.GetProperty("resultType").GetString());
            Check.True(toolsResult.TryGetProperty("ttlMs", out _), "stateless tools/list should carry ttlMs.");
            Check.True(toolsResult.TryGetProperty("cacheScope", out _), "stateless tools/list should carry cacheScope.");
            AssertExactlyPartioTools(toolsResult);

            // 3. tools/call: the caller's token must reach the tool (it calls Partio as the caller).
            McpResponse call = await PostStatelessAsync(
                path,
                "tools/call",
                1,
                new Dictionary<string, object?> { ["name"] = "partio_enumerate_completion_endpoints", ["arguments"] = new { maxResults = 5 } },
                "partio_enumerate_completion_endpoints",
                auth).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.OK, call.StatusCode, "stateless tools/call should succeed on " + path + ". Body: " + call.Body);
            JsonElement callResult = RequireResult(call, "tools/call");
            Check.Equal("complete", callResult.GetProperty("resultType").GetString());
            Check.False(IsToolError(callResult), "enumerate should not report a tool error. Body: " + call.Body);
            Check.True(callResult.GetProperty("structuredContent").GetProperty("Data").GetArrayLength() >= 1, "The default completion endpoint should be listed.");
        }

        // ---------------- Positive: tool behavior ----------------

        private static async Task TestCapabilitiesAsync()
        {
            JsonElement structured = await CallToolStructuredAsync("partio_capabilities", new { }, _AdminKey).ConfigureAwait(false);
            Check.Equal("partio-mcp", structured.GetProperty("Server").GetString());
            Check.Equal(HandshakeVersion, structured.GetProperty("ProtocolVersion").GetString());

            List<string> advertised = structured.GetProperty("Tools").EnumerateArray().Select(t => t.GetString() ?? "").OrderBy(n => n, StringComparer.Ordinal).ToList();
            Check.Equal<string>(ExpectedTools, advertised, "partio_capabilities should advertise exactly the tools tools/list returns.");
            Check.True(structured.TryGetProperty("PartioHealth", out JsonElement health) && health.ValueKind == JsonValueKind.Object, "partio_capabilities should report Partio health.");
        }

        private static async Task TestCompletionCrudAsync()
        {
            string name = "mcp-test-" + Guid.NewGuid().ToString("N").Substring(0, 8);

            JsonElement created = await CallToolStructuredAsync("partio_create_completion_endpoint", new
            {
                TenantId = "default",
                Name = name,
                Endpoint = "http://127.0.0.1:1",
                ApiFormat = "Ollama",
                Model = "mcp-model",
                MaxConcurrentRequests = 3,
                MaxQueueDepth = 5,
                ContextSize = 4096
            }, _AdminKey).ConfigureAwait(false);

            string id = created.GetProperty("Id").GetString() ?? "";
            Check.False(string.IsNullOrEmpty(id), "create should return an id.");
            Check.Equal(5, created.GetProperty("MaxQueueDepth").GetInt32());

            // Timestamps are server-set: never DateTime.MinValue, even though the tool passes an SDK model.
            DateTime createdUtc = created.GetProperty("CreatedUtc").GetDateTime().ToUniversalTime();
            Check.True((DateTime.UtcNow - createdUtc).Duration() < TimeSpan.FromMinutes(2), "CreatedUtc should be server-set near now but was " + createdUtc.ToString("o"));

            try
            {
                JsonElement fetched = await CallToolStructuredAsync("partio_get_completion_endpoint", new { id }, _AdminKey).ConfigureAwait(false);
                Check.Equal(name, fetched.GetProperty("Name").GetString());
                Check.Equal(3, fetched.GetProperty("MaxConcurrentRequests").GetInt32());
                Check.Equal(4096, fetched.GetProperty("ContextSize").GetInt32());

                JsonElement page = await CallToolStructuredAsync("partio_enumerate_completion_endpoints", new { search = name, maxResults = 10 }, _AdminKey).ConfigureAwait(false);
                Check.Contains(page.GetProperty("Data").EnumerateArray().ToList(), e => e.GetProperty("Id").GetString() == id, "enumerate with search should find the new endpoint.");
            }
            finally
            {
                JsonElement deleted = await CallToolStructuredAsync("partio_delete_completion_endpoint", new { id }, _AdminKey).ConfigureAwait(false);
                Check.True(deleted.GetProperty("Deleted").GetBoolean(), "delete should report Deleted=true.");
            }

            McpResponse gone = await CallToolAsync("partio_get_completion_endpoint", new { id }, _AdminKey).ConfigureAwait(false);
            Check.True(gone.HasError || IsToolError(RequireResult(gone, "tools/call")), "get after delete should fail. Body: " + gone.Body);
        }

        private static async Task TestTenantTokenAsync()
        {
            // A non-admin tenant credential passes MCP authentication (Partio accepts it) ...
            JsonElement capabilities = await CallToolStructuredAsync("partio_capabilities", new { }, _TestToken).ConfigureAwait(false);
            Check.Equal("partio-mcp", capabilities.GetProperty("Server").GetString());

            // ... and tools run as that caller, not as the configured admin fallback key: endpoint management
            // is admin-only in Partio, so the same call that succeeds for the admin key is refused here.
            McpResponse response = await CallToolAsync("partio_enumerate_embedding_endpoints", new { maxResults = 5 }, _TestToken).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.OK, response.StatusCode, "the tenant token should be authenticated. Body: " + response.Body);
            Check.True(response.HasError, "an admin-only tool should fail for a tenant token. Body: " + response.Body);
            Check.Contains("Admin access required", response.Body);
        }

        private static async Task TestEndpointSchemaAllowsExtraFieldsAsync()
        {
            // The endpoint schemas declare additionalProperties: true, so full endpoint definitions with fields
            // beyond the declared ones (Labels, Tags, ...) must pass Voltaic 2.x validation.
            JsonElement created = await CallToolStructuredAsync("partio_create_embedding_endpoint", new
            {
                TenantId = "default",
                Name = "mcp-extra-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Endpoint = "http://127.0.0.1:1",
                ApiFormat = "Ollama",
                Model = "mcp-embed",
                Labels = new[] { "mcp" },
                Tags = new Dictionary<string, string> { ["origin"] = "mcp-tests" }
            }, _AdminKey).ConfigureAwait(false);

            string id = created.GetProperty("Id").GetString() ?? "";
            Check.False(string.IsNullOrEmpty(id), "create should return an id.");
            Check.Equal("mcp-tests", created.GetProperty("Tags").GetProperty("origin").GetString());
            await CallToolStructuredAsync("partio_delete_embedding_endpoint", new { id }, _AdminKey).ConfigureAwait(false);
        }

        // ---------------- Negative: authentication ----------------

        private static async Task TestMissingBearerAsync()
        {
            McpResponse response = await PostAsync("/rpc", Body("tools/list", 1, new { }), null).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.Unauthorized, response.StatusCode, "tools/list without a bearer must be rejected. Body: " + response.Body);
        }

        private static async Task TestInvalidBearerAsync()
        {
            McpResponse response = await PostAsync("/rpc", ToolCallBody(1, "partio_capabilities", new { }), Bearer("not-a-real-token")).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.Unauthorized, response.StatusCode, "an invalid bearer must be rejected before any tool runs. Body: " + response.Body);
        }

        private static async Task TestStatelessMissingBearerAsync()
        {
            McpResponse response = await PostStatelessAsync("/mcp", "tools/list", 0, null, null, null).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.Unauthorized, response.StatusCode, "stateless tools/list without a bearer must be rejected. Body: " + response.Body);
        }

        // ---------------- Negative: Voltaic 2.0 breaking changes ----------------

        private static async Task TestBareToolCallAsync()
        {
            // Voltaic 2.x: RegisterTool no longer also registers a bare JSON-RPC method.
            McpResponse response = await PostAsync("/rpc", Body("partio_capabilities", 1, new { }), Bearer(_AdminKey)).ConfigureAwait(false);
            Check.Equal(-32601, RequireErrorCode(response, "bare partio_capabilities"));
        }

        private static async Task TestDemoToolsNotCallableAsync()
        {
            foreach (string demo in _VoltaicDemoTools)
            {
                McpResponse response = await CallToolAsync(demo, new { }, _AdminKey).ConfigureAwait(false);
                Check.Equal(-32602, RequireErrorCode(response, "tools/call " + demo));
                Check.Contains("was not found", response.ErrorMessage ?? "", "tools/call " + demo + " should report the tool as not found.");
            }

            // getSessions/getClients are gone as bare methods too.
            foreach (string method in new[] { "getSessions", "getClients", "echo", "getTime" })
            {
                McpResponse response = await PostAsync("/rpc", Body(method, 1, new { }), Bearer(_AdminKey)).ConfigureAwait(false);
                Check.Equal(-32601, RequireErrorCode(response, "bare " + method));
            }
        }

        private static async Task TestAdditionalPropertiesRejectedAsync()
        {
            // partio_capabilities declares additionalProperties: false, which Voltaic 2.x now enforces.
            McpResponse response = await CallToolAsync("partio_capabilities", new { verbose = true }, _AdminKey).ConfigureAwait(false);
            Check.Equal(-32602, RequireErrorCode(response, "partio_capabilities with an undeclared argument"));
            Check.Contains("unexpected property 'verbose'", response.ErrorMessage ?? "");
        }

        private static async Task TestMissingRequiredArgumentAsync()
        {
            McpResponse response = await CallToolAsync("partio_get_completion_endpoint", new { }, _AdminKey).ConfigureAwait(false);
            Check.Equal(-32602, RequireErrorCode(response, "partio_get_completion_endpoint without id"));
        }

        private static async Task TestWrongArgumentTypeAsync()
        {
            McpResponse response = await CallToolAsync("partio_enumerate_completion_endpoints", new { maxResults = "ten" }, _AdminKey).ConfigureAwait(false);
            Check.Equal(-32602, RequireErrorCode(response, "partio_enumerate_completion_endpoints with a string maxResults"));
        }

        private static async Task TestUnknownEndpointAsync()
        {
            McpResponse response = await CallToolAsync("partio_get_completion_endpoint", new { id = "cep_does_not_exist_" + Guid.NewGuid().ToString("N") }, _AdminKey).ConfigureAwait(false);
            Check.True(response.HasError || IsToolError(RequireResult(response, "tools/call")), "an unknown id should fail. Body: " + response.Body);
        }

        // ---------------- Helpers ----------------

        private static SelfHostedMcpServerEnvironment Mcp => _Mcp ?? throw new InvalidOperationException("The MCP server environment was not started.");

        private static string Bearer(string token) => "Bearer " + token;

        private static async Task<McpResponse> CallToolAsync(string name, object arguments, string token)
        {
            return await PostAsync("/rpc", ToolCallBody(1, name, arguments), Bearer(token)).ConfigureAwait(false);
        }

        private static async Task<JsonElement> CallToolStructuredAsync(string name, object arguments, string token)
        {
            McpResponse response = await CallToolAsync(name, arguments, token).ConfigureAwait(false);
            Check.Equal(HttpStatusCode.OK, response.StatusCode, "tools/call " + name + " should succeed. Body: " + response.Body);
            JsonElement result = RequireResult(response, "tools/call " + name);
            Check.False(IsToolError(result), "tools/call " + name + " reported a tool error. Body: " + response.Body);
            Check.True(result.TryGetProperty("structuredContent", out JsonElement structured), "tools/call " + name + " should return structuredContent. Body: " + response.Body);
            return structured.Clone();
        }

        private static string Body(string method, object id, object? parameters)
        {
            Dictionary<string, object?> body = new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["method"] = method, ["id"] = id };
            if (parameters != null) body["params"] = parameters;
            return JsonSerializer.Serialize(body);
        }

        private static string NotificationBody(string method)
        {
            return JsonSerializer.Serialize(new { jsonrpc = "2.0", method });
        }

        private static string ToolCallBody(object id, string name, object arguments)
        {
            return Body("tools/call", id, new { name, arguments });
        }

        private static Task<McpResponse> PostStatelessAsync(string path, string method, object id, Dictionary<string, object?>? parameters, string? nameHeader, string? authorization)
        {
            Dictionary<string, object?> withMeta = parameters != null ? new Dictionary<string, object?>(parameters) : new Dictionary<string, object?>();
            withMeta["_meta"] = new Dictionary<string, object?>
            {
                ["io.modelcontextprotocol/protocolVersion"] = StatelessVersion,
                ["io.modelcontextprotocol/clientInfo"] = new { name = "claude-code", version = "2.1.281" },
                ["io.modelcontextprotocol/clientCapabilities"] = new { }
            };

            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                ["MCP-Protocol-Version"] = StatelessVersion,
                ["Mcp-Method"] = method
            };
            if (nameHeader != null) headers["Mcp-Name"] = nameHeader;

            return PostAsync(path, Body(method, id, withMeta), authorization, mcpAccept: true, headers: headers);
        }

        private static async Task<McpResponse> PostAsync(string path, string body, string? authorization, bool mcpAccept = false, IDictionary<string, string>? headers = null)
        {
            using HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, Mcp.BaseUrl + path);
            message.Content = new StringContent(body, Encoding.UTF8, "application/json");

            if (mcpAccept)
            {
                message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            }

            if (authorization != null)
                message.Headers.TryAddWithoutValidation("Authorization", authorization);

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                    message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            using HttpResponseMessage response = await _Http.SendAsync(message).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            string? sessionId = response.Headers.TryGetValues("MCP-Session-Id", out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
            return new McpResponse(response.StatusCode, responseBody, sessionId);
        }

        private static JsonElement RequireResult(McpResponse response, string context)
        {
            JsonElement? root = response.Json;
            if (root == null || !root.Value.TryGetProperty("result", out JsonElement result))
                throw new Exception(context + " should return a JSON-RPC result. Status: " + (int)response.StatusCode + " Body: " + response.Body);
            return result;
        }

        private static int RequireErrorCode(McpResponse response, string context)
        {
            JsonElement? root = response.Json;
            if (root == null || !root.Value.TryGetProperty("error", out JsonElement error))
                throw new Exception(context + " should return a JSON-RPC error. Status: " + (int)response.StatusCode + " Body: " + response.Body);
            return error.GetProperty("code").GetInt32();
        }

        private static bool IsToolError(JsonElement result)
        {
            return result.TryGetProperty("isError", out JsonElement isError) && isError.ValueKind == JsonValueKind.True;
        }

        private static void AssertEmptyObjectResult(McpResponse response, string context)
        {
            JsonElement result = RequireResult(response, context);
            Check.Equal(JsonValueKind.Object, result.ValueKind, context + " should return an object, not \"pong\". Body: " + response.Body);
            Check.Equal(0, result.EnumerateObject().Count(), context + " should return {}. Body: " + response.Body);
        }

        private static void AssertExactlyPartioTools(JsonElement toolsResult)
        {
            List<string> names = toolsResult.GetProperty("tools").EnumerateArray()
                .Select(t => t.GetProperty("name").GetString() ?? "")
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            foreach (string demo in _VoltaicDemoTools)
                Check.False(names.Contains(demo), "tools/list must not publish Voltaic's '" + demo + "' tool.");

            Check.Equal<string>(ExpectedTools, names, "tools/list should return exactly the Partio tools.");

            JsonElement capabilities = toolsResult.GetProperty("tools").EnumerateArray().First(t => t.GetProperty("name").GetString() == "partio_capabilities");
            Check.True(capabilities.TryGetProperty("inputSchema", out _), "Each tool should publish an inputSchema.");
        }

        /// <summary>
        /// An HTTP response from the MCP server. The body is plain JSON on <c>/rpc</c> and stateless
        /// requests, and may be a Server-Sent Events stream on the handshake-era <c>/mcp</c> path;
        /// <see cref="Json"/> resolves either to the JSON-RPC response object.
        /// </summary>
        private sealed class McpResponse
        {
            public McpResponse(HttpStatusCode statusCode, string body, string? sessionId)
            {
                StatusCode = statusCode;
                Body = body;
                SessionId = sessionId;
                Json = Parse(body);
            }

            public HttpStatusCode StatusCode { get; }

            public string Body { get; }

            public string? SessionId { get; }

            public JsonElement? Json { get; }

            public bool HasError => Json != null && Json.Value.TryGetProperty("error", out _);

            public string? ErrorMessage =>
                Json != null && Json.Value.TryGetProperty("error", out JsonElement error) && error.TryGetProperty("message", out JsonElement message)
                    ? message.GetString()
                    : null;

            private static JsonElement? Parse(string body)
            {
                if (string.IsNullOrWhiteSpace(body)) return null;

                string trimmed = body.TrimStart();
                if (trimmed.StartsWith("{", StringComparison.Ordinal))
                    return TryParse(trimmed);

                // SSE: take the last data line that holds a JSON-RPC response.
                JsonElement? last = null;
                foreach (string line in body.Split('\n'))
                {
                    string candidate = line.TrimEnd('\r');
                    if (!candidate.StartsWith("data:", StringComparison.Ordinal)) continue;
                    JsonElement? parsed = TryParse(candidate.Substring("data:".Length).Trim());
                    if (parsed != null && (parsed.Value.TryGetProperty("result", out _) || parsed.Value.TryGetProperty("error", out _)))
                        last = parsed;
                }

                return last;
            }

            private static JsonElement? TryParse(string json)
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(json);
                    return document.RootElement.Clone();
                }
                catch (JsonException)
                {
                    return null;
                }
            }
        }
    }
}
