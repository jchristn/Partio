namespace Partio.Server
{
    using System.Diagnostics;
    using System.Collections.Concurrent;
    using System.Runtime.Loader;
    using System.Text.RegularExpressions;
    using Partio.Core.Chunking;
    using Partio.Core.Database;
    using Partio.Core.Database.Sqlite;
    using Partio.Core.Database.Postgresql;
    using Partio.Core.Database.Mysql;
    using Partio.Core.Database.Sqlserver;
    using Partio.Core.Enums;
    using Partio.Core.Exceptions;
    using Partio.Core.Models;
    using Partio.Core.Serialization;
    using Partio.Core.Settings;
    using Partio.Core.Summarization;
    using Partio.Core.ThirdParty;
    using Partio.Core.Tokenization;
    using Partio.Server.Models;
    using Partio.Server.Services;
    using SyslogLogging;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using Constants = Partio.Core.Constants;
    using ApiErrorResponse = Partio.Core.Models.ApiErrorResponse;

    /// <summary>
    /// Main entry point for the Partio server.
    /// </summary>
    public class PartioServer
    {
        private static ServerSettings _Settings = null!;
        private static LoggingModule _Logging = null!;
        private static DatabaseDriverBase _Database = null!;
        private static AuthenticationService _AuthService = null!;
        private static RequestHistoryService? _RequestHistoryService;
        private static RequestHistoryCleanupService? _CleanupService;
        private static SharedHealthCheckCoordinator? _SharedHealthCheckCoordinator;
        private static EmbeddingHealthCheckService? _HealthCheckService;
        private static CompletionHealthCheckService? _CompletionHealthCheckService;
        private static ModelLoadService _ModelLoadService = null!;
        private static ChunkingEngine _ChunkingEngine = null!;
        private static TokenizationProfileResolver _TokenizationResolver = null!;
        private static PartioSerializer _Serializer = new PartioSerializer();
        private static SerializationHelper.Serializer _JsonSerializer = new SerializationHelper.Serializer();
        private static DateTime _StartTimeUtc = DateTime.UtcNow;
        private static string _Header = "[PartioServer] ";
        private static ConcurrentDictionary<string, AuthContext> _AuthContexts = new ConcurrentDictionary<string, AuthContext>();
        private static ConcurrentDictionary<string, InFlightRequest> _InFlightRequests = new ConcurrentDictionary<string, InFlightRequest>();
        private static ConcurrentDictionary<string, CancellationToken> _RequestTokens = new ConcurrentDictionary<string, CancellationToken>();
        private static bool _ShuttingDown = false;

        /// <summary>
        /// Application entry point.
        /// </summary>
        public static async Task Main(string[] args)
        {
            Console.WriteLine(Constants.Logo);
            Console.WriteLine("  Partio v" + Constants.Version);
            Console.WriteLine();

            // 1. Load settings
            _Settings = LoadSettings();

            // 2. Initialize logging
            _Logging = InitializeLogging(_Settings);
            _Logging.Info(_Header + "starting Partio v" + Constants.Version);

            // 3. Create and initialize database
            _Database = CreateDatabaseDriver(_Settings, _Logging);
            await _Database.InitializeAsync().ConfigureAwait(false);
            _Logging.Info(_Header + "database initialized (" + _Settings.Database.Type + ")");

            // 4. First run initialization
            await InitializeFirstRunAsync().ConfigureAwait(false);
            await ReconcileDefaultEmbeddingEndpointAsync().ConfigureAwait(false);

            // 5. Initialize services
            _AuthService = new AuthenticationService(_Settings, _Database, _Logging);
            _ChunkingEngine = new ChunkingEngine(_Logging);
            _TokenizationResolver = new TokenizationProfileResolver(_Settings, _Logging);

            // 6. Request history
            if (_Settings.RequestHistory.Enabled)
            {
                _RequestHistoryService = new RequestHistoryService(_Settings, _Database, _Logging);
                _CleanupService = new RequestHistoryCleanupService(_Settings, _Database, _Logging);
                _CleanupService.Start();
                _Logging.Info(_Header + "request history enabled");
            }

            // 6b. Health check services
            _SharedHealthCheckCoordinator = new SharedHealthCheckCoordinator(_Logging);
            _HealthCheckService = new EmbeddingHealthCheckService(_Database, _Logging, _TokenizationResolver, _SharedHealthCheckCoordinator);
            await _HealthCheckService.StartAsync().ConfigureAwait(false);
            _CompletionHealthCheckService = new CompletionHealthCheckService(_Database, _Logging, _SharedHealthCheckCoordinator);
            await _CompletionHealthCheckService.StartAsync().ConfigureAwait(false);
            _ModelLoadService = new ModelLoadService(_Logging, CreateEmbeddingClient, CreateCompletionClient);

            // 7. Initialize Watson
            WebserverSettings webSettings = new WebserverSettings(
                _Settings.Rest.Hostname,
                _Settings.Rest.Port,
                _Settings.Rest.Ssl);
            Webserver server = new Webserver(webSettings, async (ctx) =>
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = Constants.JsonContentType;
                await ctx.Response.Send("{\"Error\":\"NotFound\",\"Message\":\"Route not found\",\"StatusCode\":404}").ConfigureAwait(false);
            });
            server.Serializer = _Serializer;

            // OpenAPI / Swagger
            server.UseOpenApi(settings =>
            {
                settings.Info = new OpenApiInfo
                {
                    Title = "Partio API",
                    Version = Constants.Version,
                    Description = "Multi-tenant semantic cell processing with chunking and embedding."
                };
                settings.Tags = new List<OpenApiTag>
                {
                    new OpenApiTag { Name = "Health", Description = "Health check endpoints" },
                    new OpenApiTag { Name = "Process", Description = "Chunk and embed semantic cells" },
                    new OpenApiTag { Name = "Explorer", Description = "Exercise configured embedding and inference endpoints through Partio" },
                    new OpenApiTag { Name = "Tenants", Description = "Tenant management (admin)" },
                    new OpenApiTag { Name = "Users", Description = "User management (admin)" },
                    new OpenApiTag { Name = "Credentials", Description = "Credential management (admin)" },
                    new OpenApiTag { Name = "Embedding Endpoints", Description = "Embedding endpoint management (admin)" },
                    new OpenApiTag { Name = "Completion Endpoints", Description = "Completion/inference endpoint management (admin)" },
                    new OpenApiTag { Name = "Model Loading", Description = "Load or warm configured provider models (admin)" },
                    new OpenApiTag { Name = "Requests", Description = "Request history (admin)" }
                };
                settings.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
                {
                    ["Bearer"] = new OpenApiSecurityScheme { Type = "http", Scheme = "bearer", BearerFormat = "token", Description = "Bearer token authentication. Use an admin API key or credential bearer token." }
                };
            });

            #region Routes

            server.Routes.AuthenticateApiRequest = async (HttpContextBase ctx) =>
            {
                string? authHeader = ctx.Request.RetrieveHeaderValue(Constants.AuthorizationHeader);
                string? token = null;
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith(Constants.BearerPrefix, StringComparison.OrdinalIgnoreCase))
                    token = authHeader.Substring(Constants.BearerPrefix.Length).Trim();

                AuthContext authCtx = await _AuthService.AuthenticateBearerAsync(token ?? string.Empty).ConfigureAwait(false);
                string connId = ctx.Guid.ToString();
                _AuthContexts[connId] = authCtx;
                ctx.Metadata = authCtx;

                AuthResult result = new AuthResult();
                result.AuthenticationResult = authCtx.IsAuthenticated
                    ? AuthenticationResultEnum.Success
                    : AuthenticationResultEnum.NotFound;
                result.AuthorizationResult = authCtx.IsAuthenticated
                    ? AuthorizationResultEnum.Permitted
                    : AuthorizationResultEnum.DeniedImplicit;
                return result;
            };
            if (_Settings.Cors.Enabled)
            {
                server.Routes.Preflight = async (HttpContextBase ctx) =>
                {
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Headers.Add("Access-Control-Allow-Origin", _Settings.Cors.AllowedOrigins);
                    ctx.Response.Headers.Add("Access-Control-Allow-Methods", _Settings.Cors.AllowedMethods);
                    ctx.Response.Headers.Add("Access-Control-Allow-Headers", _Settings.Cors.AllowedHeaders);
                    ctx.Response.Headers.Add("Access-Control-Max-Age", _Settings.Cors.MaxAgeSeconds.ToString());
                    if (!string.IsNullOrEmpty(_Settings.Cors.ExposedHeaders))
                        ctx.Response.Headers.Add("Access-Control-Expose-Headers", _Settings.Cors.ExposedHeaders);
                    if (_Settings.Cors.AllowCredentials)
                        ctx.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
                    await ctx.Response.Send().ConfigureAwait(false);
                };
            }

            server.Routes.PreRouting = async (HttpContextBase ctx) =>
            {
                ctx.Response.ContentType = Constants.JsonContentType;

                if (_Settings.Cors.Enabled)
                {
                    ctx.Response.Headers.Add("Access-Control-Allow-Origin", _Settings.Cors.AllowedOrigins);
                    if (!string.IsNullOrEmpty(_Settings.Cors.ExposedHeaders))
                        ctx.Response.Headers.Add("Access-Control-Expose-Headers", _Settings.Cors.ExposedHeaders);
                    if (_Settings.Cors.AllowCredentials)
                        ctx.Response.Headers.Add("Access-Control-Allow-Credentials", "true");
                }
            };
            server.Routes.PostRouting = async (HttpContextBase ctx) =>
            {
                if (_Settings.Debug.Requests)
                    _Logging.Info(_Header + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithQuery + " " + ctx.Response.StatusCode);
            };
            // Middleware: request history tracking + exception mapping.
            // Request history is tracked here (not in PreRouting/PostRouting) because
            // the middleware pipeline is guaranteed to execute for all HTTP methods.
            server.Middleware.Add(async (HttpContextBase ctx, Func<Task> next, CancellationToken token) =>
            {
                string connId = ctx.Guid.ToString();
                int statusCode = 500;
                _RequestTokens[connId] = token;

                // Create request history entry before the route handler runs
                if (_Settings.RequestHistory.Enabled && _RequestHistoryService != null)
                {
                    try
                    {
                        AuthContext? auth = ctx.Metadata as AuthContext;
                        RequestHistoryEntry entry = await _RequestHistoryService.CreateEntryAsync(
                            ctx.Request.Method.ToString(),
                            ctx.Request.Url.RawWithQuery,
                            ctx.Request.Source.IpAddress,
                            auth).ConfigureAwait(false);
                        Stopwatch sw = Stopwatch.StartNew();
                        _InFlightRequests[connId] = new InFlightRequest { Entry = entry, Stopwatch = sw };
                    }
                    catch (Exception ex)
                    {
                        _Logging.Warn(_Header + "failed to create request history entry: " + ex.Message);
                    }
                }

                try
                {
                    try
                    {
                        await next().ConfigureAwait(false);
                        statusCode = ctx.Response.StatusCode;
                    }
                    catch (WebserverException wex)
                    {
                        statusCode = wex.StatusCode;
                        throw; // already mapped
                    }
                    catch (KeyNotFoundException ex)
                    {
                        statusCode = 404;
                        if (_Settings.Debug.Exceptions) _Logging.Warn(_Header + "exception: " + ex.Message);
                        throw new WebserverException(ApiResultEnum.NotFound, ex.Message);
                    }
                    catch (ArgumentException ex)
                    {
                        statusCode = 400;
                        if (_Settings.Debug.Exceptions) _Logging.Warn(_Header + "exception: " + ex.Message);
                        throw new WebserverException(ApiResultEnum.BadRequest, ex.Message);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        statusCode = 401;
                        if (_Settings.Debug.Exceptions) _Logging.Warn(_Header + "exception: " + ex.Message);
                        throw new WebserverException(ApiResultEnum.NotAuthorized, ex.Message);
                    }
                    catch (ProviderConcurrencyLimitException ex)
                    {
                        statusCode = 429;
                        if (_Settings.Debug.Exceptions) _Logging.Warn(_Header + "exception: " + ex.Message);
                        await SendApiErrorAsync(ctx, 429, "TooManyRequests", ex.Message).ConfigureAwait(false);
                    }
                    catch (ProviderOperationTimeoutException ex)
                    {
                        statusCode = 504;
                        if (_Settings.Debug.Exceptions) _Logging.Warn(_Header + "exception: " + ex.Message);
                        await SendApiErrorAsync(ctx, 504, "GatewayTimeout", ex.Message).ConfigureAwait(false);
                    }
                    catch (EndpointUnhealthyException ex)
                    {
                        statusCode = 502;
                        if (_Settings.Debug.Exceptions) _Logging.Warn(_Header + "exception: " + ex.Message);
                        await SendApiErrorAsync(ctx, 502, "BadGateway", ex.Message).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        statusCode = 500;
                        if (_Settings.Debug.Exceptions) _Logging.Warn(_Header + "exception: " + ex.Message);
                        throw;
                    }
                }
                finally
                {
                    // Update request history entry after the route handler completes
                    if (_InFlightRequests.TryRemove(connId, out InFlightRequest? inflight) && !inflight.DetailRecorded)
                    {
                        try
                        {
                            inflight.Stopwatch.Stop();
                            string? requestBody = ctx.Request.ContentLength > 0 ? ctx.Request.DataAsString : null;
                            string? responseBody = null;
                            try { responseBody = ctx.Response.DataAsString; } catch { }
                            Dictionary<string, string> reqHeaders = ExtractHeaders(ctx.Request.Headers);
                            Dictionary<string, string> respHeaders = ExtractHeaders(ctx.Response.Headers);
                            await _RequestHistoryService!.UpdateWithResponseAsync(
                                inflight.Entry,
                                statusCode,
                                inflight.Stopwatch.Elapsed.TotalMilliseconds,
                                requestBody, responseBody, reqHeaders, respHeaders, null, null).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _Logging.Warn(_Header + "failed to update request history entry: " + ex.Message);
                        }
                    }

                    _RequestTokens.TryRemove(connId, out _);
                }
            });

            #region Health

            // Health (no auth)
            server.Head("/", HealthHead);
            server.Get("/", HealthGet, api => {
                api.Summary = "Health status";
                api.WithTag("Health")
                    .WithResponse(200, OpenApiResponseMetadata.Json("Health status", null));
            });
            server.Get("/v1.0/health", HealthJson, api => {
                api.Summary = "Health status JSON";
                api.WithTag("Health")
                    .WithResponse(200, OpenApiResponseMetadata.Json("Health status", null));
            });
            server.Get("/v1.0/whoami", WhoAmI, api => {
                api.Summary = "Returns the role and tenant of the authenticated caller";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Health")
                    .WithResponse(200, OpenApiResponseMetadata.Json("Caller identity", null))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized());
            }, auth: true);

            #endregion

            #region Processing

            // Process (auth required)
            server.Post<SemanticCellRequest>("/v1.0/process", ProcessSingle, api => {
                api.Summary = "Process a single semantic cell";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Process")
                    .WithDescription("Optionally summarizes, then chunks and embeds a single semantic cell. Embedding endpoint ID is specified in EmbeddingConfiguration.EmbeddingEndpointId.")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Semantic cell to process", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Processed cell with chunks and embeddings", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Post<List<SemanticCellRequest>>("/v1.0/process/batch", ProcessBatch, api => {
                api.Summary = "Process multiple semantic cells";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Process")
                    .WithDescription("Optionally summarizes, then chunks and embeds multiple semantic cells in a single request.")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Semantic cells to process", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Processed cells", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Post<EndpointExplorerEmbeddingRequest>("/v1.0/explorer/embedding", ExploreEmbeddingEndpoint, api => {
                api.Summary = "Exercise an embedding endpoint through Partio";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Explorer")
                    .WithDescription("Sends sample embedding input through the configured Partio embedding path and returns the result together with captured upstream call details.")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Embedding endpoint explorer request", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Embedding explorer result", null))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized());
            }, auth: true);
            server.Post<EndpointExplorerCompletionRequest>("/v1.0/explorer/completion", ExploreCompletionEndpoint, api => {
                api.Summary = "Exercise an inference endpoint through Partio";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Explorer")
                    .WithDescription("Sends a prompt through the configured Partio inference path and returns the generated output together with captured upstream call details.")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Inference endpoint explorer request", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Inference explorer result", null))
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized());
            }, auth: true);
            server.Post<ChunkRequest>("/v1.0/chunk", ChunkOnly, api => {
                api.Summary = "Chunk a semantic cell (no embedding)";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Process")
                    .WithDescription("Chunks a single semantic cell into text chunks without embedding them. Uses a built-in tokenizer (cl100k_base) to honor the token budget, so no embedding endpoint is required.")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Semantic cell to chunk", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Chunked cell (text chunks, no embeddings)", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized());
            }, auth: true);
            server.Post<EmbedRequest>("/v1.0/embed", EmbedTexts, api => {
                api.Summary = "Embed one or more texts";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Process")
                    .WithDescription("Generates embedding vectors for one or more input strings using the specified embedding endpoint. Does not chunk; each input is embedded as-is.")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Embedding request", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Embedding vectors", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Post<SummarizeRequest>("/v1.0/summarize", SummarizeText, api => {
                api.Summary = "Summarize text";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Process")
                    .WithDescription("Summarizes a piece of text through the specified completion endpoint using the same summarization engine as /v1.0/process. Does not chunk or embed.")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Summarize request", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Generated summary", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);

            #endregion

            #region Tenants

            // Tenants (admin)
            server.Put<TenantMetadata>("/v1.0/tenants", CreateTenant, auth: true);
            server.Get("/v1.0/tenants/{id}", ReadTenant, api => {
                api.Summary = "Read a tenant";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Tenants")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID", OpenApiSchemaMetadata.String()))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Tenant details", null))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Put<TenantMetadata>("/v1.0/tenants/{id}", UpdateTenant, auth: true);
            server.Delete("/v1.0/tenants/{id}", DeleteTenant, auth: true);
            server.Head("/v1.0/tenants/{id}", HeadTenant, auth: true);
            server.Post<EnumerationRequest>("/v1.0/tenants/enumerate", EnumerateTenants, api => {
                api.Summary = "List tenants";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Tenants")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Pagination and filter options", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Paginated tenant list", null));
            }, auth: true);

            #endregion

            #region Users

            // Users (admin)
            server.Put<UserMaster>("/v1.0/users", CreateUser, auth: true);
            server.Get("/v1.0/users/{id}", ReadUser, api => {
                api.Summary = "Read a user";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Users")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "User ID", OpenApiSchemaMetadata.String()))
                    .WithResponse(200, OpenApiResponseMetadata.Json("User details (password redacted)", null))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Put<UserMaster>("/v1.0/users/{id}", UpdateUser, auth: true);
            server.Delete("/v1.0/users/{id}", DeleteUser, auth: true);
            server.Head("/v1.0/users/{id}", HeadUser, auth: true);
            server.Post<EnumerationRequest>("/v1.0/users/enumerate", EnumerateUsers, api => {
                api.Summary = "List users";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Users")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Pagination and filter options", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Paginated user list", null));
            }, auth: true);

            #endregion

            #region Credentials

            // Credentials (admin)
            server.Put<Credential>("/v1.0/credentials", CreateCredential, auth: true);
            server.Get("/v1.0/credentials/{id}", ReadCredential, api => {
                api.Summary = "Read a credential";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Credentials")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID", OpenApiSchemaMetadata.String()))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Credential details", null))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Put<Credential>("/v1.0/credentials/{id}", UpdateCredential, auth: true);
            server.Delete("/v1.0/credentials/{id}", DeleteCredential, auth: true);
            server.Head("/v1.0/credentials/{id}", HeadCredential, auth: true);
            server.Post<EnumerationRequest>("/v1.0/credentials/enumerate", EnumerateCredentials, api => {
                api.Summary = "List credentials";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Credentials")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Pagination and filter options", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Paginated credential list", null));
            }, auth: true);

            #endregion

            #region Endpoints

            // Embedding Endpoints (admin)
            // NOTE: Literal path routes (/health, /enumerate) must be registered BEFORE
            // parameterized routes (/{id}) to prevent the router from matching literal
            // segments as parameter values.
            server.Put<EmbeddingEndpoint>("/v1.0/endpoints/embedding", CreateEndpoint, auth: true);
            server.Post<EnumerationRequest>("/v1.0/endpoints/embedding/enumerate", EnumerateEndpoints, api => {
                api.Summary = "List embedding endpoints";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Embedding Endpoints")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Pagination and filter options", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Paginated endpoint list", null));
            }, auth: true);
            server.Get("/v1.0/endpoints/embedding/health", GetAllEndpointHealth, api => {
                api.Summary = "List health status for all embedding endpoints";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Embedding Endpoints")
                    .WithDescription("Returns health status for all monitored embedding endpoints. Scoped by tenant for non-admins.")
                    .WithResponse(200, OpenApiResponseMetadata.Json("List of endpoint health statuses", null));
            }, auth: true);
            server.Post<ModelLoadRequest>("/v1.0/endpoints/embedding/{id}/load", LoadEmbeddingEndpointModel, api => {
                api.Summary = "Load or warm an embedding endpoint model";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Model Loading")
                    .WithDescription("Requests that the configured embedding provider load or warm the endpoint model. Ollama uses native keep-alive behavior; hosted providers use a warm embedding request.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Endpoint ID", OpenApiSchemaMetadata.String()))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Model load request", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Model load result", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound())
                    .WithResponse(409, OpenApiResponseMetadata.Json("Unsupported load strategy", null))
                    .WithResponse(429, OpenApiResponseMetadata.Json("Endpoint concurrency limit reached", null))
                    .WithResponse(502, OpenApiResponseMetadata.Json("Upstream provider failure", null))
                    .WithResponse(504, OpenApiResponseMetadata.Json("Upstream provider timeout", null));
            }, auth: true);
            server.Get("/v1.0/endpoints/embedding/{id}", ReadEndpoint, api => {
                api.Summary = "Read an embedding endpoint";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Embedding Endpoints")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Endpoint ID", OpenApiSchemaMetadata.String()))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Endpoint details", null))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Get("/v1.0/endpoints/embedding/{id}/health", GetEndpointHealth, api => {
                api.Summary = "Get health status for a single embedding endpoint";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Embedding Endpoints")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Endpoint ID", OpenApiSchemaMetadata.String()))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Endpoint health status", null))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Put<EmbeddingEndpoint>("/v1.0/endpoints/embedding/{id}", UpdateEndpoint, auth: true);
            server.Delete("/v1.0/endpoints/embedding/{id}", DeleteEndpoint, auth: true);
            server.Head("/v1.0/endpoints/embedding/{id}", HeadEndpoint, auth: true);

            #endregion

            #region Completion Endpoints

            // Completion Endpoints (admin)
            server.Put<CompletionEndpoint>("/v1.0/endpoints/completion", CreateCompletionEndpoint, auth: true);
            server.Post<EnumerationRequest>("/v1.0/endpoints/completion/enumerate", EnumerateCompletionEndpoints, api => {
                api.Summary = "List completion endpoints";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Completion Endpoints")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Pagination and filter options", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Paginated completion endpoint list", null));
            }, auth: true);
            server.Get("/v1.0/endpoints/completion/health", GetAllCompletionEndpointHealth, api => {
                api.Summary = "List health status for all completion endpoints";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Completion Endpoints")
                    .WithDescription("Returns health status for all monitored completion endpoints. Scoped by tenant for non-admins.")
                    .WithResponse(200, OpenApiResponseMetadata.Json("List of completion endpoint health statuses", null));
            }, auth: true);
            server.Post<ModelLoadRequest>("/v1.0/endpoints/completion/{id}/load", LoadCompletionEndpointModel, api => {
                api.Summary = "Load or warm a completion endpoint model";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Model Loading")
                    .WithDescription("Requests that the configured inference provider load or warm the endpoint model. Ollama uses native keep-alive behavior; OpenAI, vLLM, and Gemini use a minimal warm completion request.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Completion endpoint ID", OpenApiSchemaMetadata.String()))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Model load request", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Model load result", null))
                    .WithResponse(400, OpenApiResponseMetadata.BadRequest())
                    .WithResponse(401, OpenApiResponseMetadata.Unauthorized())
                    .WithResponse(404, OpenApiResponseMetadata.NotFound())
                    .WithResponse(409, OpenApiResponseMetadata.Json("Unsupported load strategy", null))
                    .WithResponse(429, OpenApiResponseMetadata.Json("Endpoint concurrency limit reached", null))
                    .WithResponse(502, OpenApiResponseMetadata.Json("Upstream provider failure", null))
                    .WithResponse(504, OpenApiResponseMetadata.Json("Upstream provider timeout", null));
            }, auth: true);
            server.Get("/v1.0/endpoints/completion/{id}", ReadCompletionEndpoint, api => {
                api.Summary = "Read a completion endpoint";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Completion Endpoints")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Completion endpoint ID", OpenApiSchemaMetadata.String()))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Completion endpoint details", null))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Get("/v1.0/endpoints/completion/{id}/health", GetCompletionEndpointHealth, api => {
                api.Summary = "Get health status for a single completion endpoint";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Completion Endpoints")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Completion endpoint ID", OpenApiSchemaMetadata.String()))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Completion endpoint health status", null))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Put<CompletionEndpoint>("/v1.0/endpoints/completion/{id}", UpdateCompletionEndpoint, auth: true);
            server.Delete("/v1.0/endpoints/completion/{id}", DeleteCompletionEndpoint, auth: true);
            server.Head("/v1.0/endpoints/completion/{id}", HeadCompletionEndpoint, auth: true);

            #endregion

            #region Request-History

            // Request History (admin)
            server.Get("/v1.0/requests/{id}", ReadRequestHistory, api => {
                api.Summary = "Read a request history entry";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Requests")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Request ID", OpenApiSchemaMetadata.String()))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Request history entry", null))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Get("/v1.0/requests/{id}/detail", ReadRequestHistoryDetail, api => {
                api.Summary = "Read request/response body detail";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Requests")
                    .WithDescription("Reads the request and response body detail from the filesystem.")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Request ID", OpenApiSchemaMetadata.String()))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Request and response body detail", null))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound());
            }, auth: true);
            server.Post<EnumerationRequest>("/v1.0/requests/enumerate", EnumerateRequestHistory, api => {
                api.Summary = "List request history";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Requests")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Pagination and filter options", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Paginated request history", null));
            }, auth: true);
            server.Delete("/v1.0/requests/{id}", DeleteRequestHistory, auth: true);
            server.Post<RequestStatisticsRequest>("/v1.0/requests/statistics", GetRequestStatistics, api => {
                api.Summary = "Get request history statistics";
                api.Security = new List<string> { "Bearer" };
                api.WithTag("Requests")
                    .WithDescription("Returns aggregated request counts grouped by time bucket, broken out by success/failure. Supports filtering by request type (Embedding/Inference), timeframe (Hour/Day/Week/Month), and endpoint URL.")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json(null, "Statistics query options", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json("Aggregated request statistics", null));
            }, auth: true);

            #endregion

            #endregion

            // 8. Start server
            CancellationTokenSource serverCts = new CancellationTokenSource();
            server.Start(serverCts.Token);
            _Logging.Info(_Header + "listening on " + (_Settings.Rest.Ssl ? "https" : "http") + "://" + _Settings.Rest.Hostname + ":" + _Settings.Rest.Port);

            EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            AssemblyLoadContext.Default.Unloading += (ctx) => waitHandle.Set();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;

                if (!_ShuttingDown)
                {
                    Console.WriteLine();
                    Console.WriteLine("Shutting down");
                    _ShuttingDown = true;
                    waitHandle.Set();
                }
            };

            bool waitHandleSignal = false;
            do
            {
                waitHandleSignal = waitHandle.WaitOne(1000);
            }
            while (!waitHandleSignal);

            // 9. Graceful shutdown
            _Logging.Info(_Header + "shutting down");
            if (_HealthCheckService != null)
                await _HealthCheckService.StopAsync().ConfigureAwait(false);
            if (_CompletionHealthCheckService != null)
                await _CompletionHealthCheckService.StopAsync().ConfigureAwait(false);
            if (_SharedHealthCheckCoordinator != null)
                await _SharedHealthCheckCoordinator.StopAsync().ConfigureAwait(false);
            if (_CleanupService != null)
                await _CleanupService.StopAsync().ConfigureAwait(false);
            serverCts.Cancel();
            server.Dispose();
            _Logging.Info(_Header + "shutdown complete");
        }

        #region Startup

        private static ServerSettings LoadSettings()
        {
            if (File.Exists(Constants.SettingsFilename))
            {
                string json = File.ReadAllText(Constants.SettingsFilename);
                ServerSettings? settings = _JsonSerializer.DeserializeJson<ServerSettings>(json);
                if (settings != null) return settings;
            }

            ServerSettings defaults = new ServerSettings();
            defaults.DefaultEmbeddingEndpoints = new List<DefaultEmbeddingEndpoint>
            {
                new DefaultEmbeddingEndpoint { Name = "nomic-embed-text", Model = "nomic-embed-text", Endpoint = "http://localhost:11434", ApiFormat = ApiFormatEnum.Ollama },
            };
            defaults.DefaultInferenceEndpoints = new List<DefaultInferenceEndpoint>
            {
                new DefaultInferenceEndpoint { Name = "gemma3:4b", Model = "gemma3:4b", Endpoint = "http://localhost:11434", ApiFormat = ApiFormatEnum.Ollama },
            };

            string defaultJson = _JsonSerializer.SerializeJson(defaults, true);
            File.WriteAllText(Constants.SettingsFilename, defaultJson);
            Console.WriteLine("Created default settings file: " + Constants.SettingsFilename);

            return defaults;
        }

        private static LoggingModule InitializeLogging(ServerSettings settings)
        {
            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = settings.Logging.ConsoleLogging;
            logging.Settings.MinimumSeverity = (Severity)settings.Logging.MinimumSeverity;

            if (settings.Logging.FileLogging)
            {
                if (!Directory.Exists(settings.Logging.LogDirectory))
                    Directory.CreateDirectory(settings.Logging.LogDirectory);

                logging.Settings.FileLogging = settings.Logging.IncludeDateInFilename
                    ? FileLoggingMode.FileWithDate
                    : FileLoggingMode.SingleLogFile;
                logging.Settings.LogFilename = Path.Combine(settings.Logging.LogDirectory, settings.Logging.LogFilename);
            }

            return logging;
        }

        private static DatabaseDriverBase CreateDatabaseDriver(ServerSettings settings, LoggingModule logging)
        {
            switch (settings.Database.Type)
            {
                case DatabaseTypeEnum.Sqlite:
                    return new SqliteDatabaseDriver(settings, logging);
                case DatabaseTypeEnum.Postgresql:
                    return new PostgresqlDatabaseDriver(settings, logging);
                case DatabaseTypeEnum.Mysql:
                    return new MysqlDatabaseDriver(settings, logging);
                case DatabaseTypeEnum.SqlServer:
                    return new SqlServerDatabaseDriver(settings, logging);
                default:
                    throw new ArgumentException("Unsupported database type: " + settings.Database.Type);
            }
        }

        private static async Task InitializeFirstRunAsync()
        {
            long tenantCount = await _Database.Tenant.CountAsync().ConfigureAwait(false);
            if (tenantCount > 0) return;

            _Logging.Info(_Header + "first run detected, creating default records");

            // Create default tenant
            TenantMetadata tenant = new TenantMetadata();
            tenant.Id = "default";
            tenant.Name = "Default Tenant";
            await _Database.Tenant.CreateAsync(tenant).ConfigureAwait(false);

            // Create default user
            UserMaster user = new UserMaster();
            user.Id = "default";
            user.TenantId = "default";
            user.Email = "admin@partio";
            user.SetPassword("password");
            user.IsAdmin = true;
            await _Database.User.CreateAsync(user).ConfigureAwait(false);

            // Create default credential
            Credential credential = new Credential();
            credential.Id = "default";
            credential.TenantId = "default";
            credential.UserId = "default";
            credential.Name = "Default API Key";
            credential.BearerToken = "default";
            await _Database.Credential.CreateAsync(credential).ConfigureAwait(false);

            // Create default embedding endpoints
            List<string> embeddingEndpointSummaries = new List<string>();
            foreach (DefaultEmbeddingEndpoint defaultEp in _Settings.DefaultEmbeddingEndpoints)
            {
                EmbeddingEndpoint ep = new EmbeddingEndpoint();
                ep.Id = "default";
                ep.TenantId = "default";
                ep.Name = defaultEp.Name;
                ep.Model = defaultEp.Model;
                ep.Endpoint = defaultEp.Endpoint;
                ep.ApiFormat = defaultEp.ApiFormat;
                ep.ApiKey = defaultEp.ApiKey;
                ep.MaximumTimeoutMs = defaultEp.MaximumTimeoutMs;
                ep.MaxConcurrentRequests = defaultEp.MaxConcurrentRequests;
                ep.Tokenization = defaultEp.Tokenization;
                ep.Labels = defaultEp.Labels;
                ep.Tags = defaultEp.Tags;
                ep.HealthCheckEnabled = true;
                EmbeddingEndpoint.ApplyHealthCheckDefaults(ep);
                await _Database.EmbeddingEndpoint.CreateAsync(ep).ConfigureAwait(false);
                embeddingEndpointSummaries.Add(ep.Model + " @ " + ep.Endpoint + " (" + ep.ApiFormat + "), ID " + ep.Id);
            }

            // Create default inference (completion) endpoints
            List<string> inferenceEndpointSummaries = new List<string>();
            foreach (DefaultInferenceEndpoint defaultIep in _Settings.DefaultInferenceEndpoints)
            {
                CompletionEndpoint cep = new CompletionEndpoint();
                cep.Id = "default";
                cep.TenantId = "default";
                cep.Name = defaultIep.Name;
                cep.Model = defaultIep.Model;
                cep.Endpoint = defaultIep.Endpoint;
                cep.ApiFormat = defaultIep.ApiFormat;
                cep.ApiKey = defaultIep.ApiKey;
                cep.MaximumTimeoutMs = defaultIep.MaximumTimeoutMs;
                cep.MaxConcurrentRequests = defaultIep.MaxConcurrentRequests;
                cep.Labels = defaultIep.Labels;
                cep.Tags = defaultIep.Tags;
                cep.HealthCheckEnabled = true;
                CompletionEndpoint.ApplyHealthCheckDefaults(cep);
                await _Database.CompletionEndpoint.CreateAsync(cep).ConfigureAwait(false);
                inferenceEndpointSummaries.Add((cep.Name ?? cep.Model) + " @ " + cep.Endpoint + " (" + cep.ApiFormat + "), ID " + cep.Id);
            }

            Console.WriteLine();
            Console.WriteLine("===== FIRST RUN =====");
            Console.WriteLine("");
            Console.WriteLine("Default objects were created to help you get started quickly.");
            Console.WriteLine("");
            Console.WriteLine("Tenant         : Default Tenant, ID default");
            Console.WriteLine("User           : admin@partio / password, ID default");
            Console.WriteLine("Credential     : Bearer token: default");
            Console.WriteLine("Admin API keys : " + string.Join(", ", _Settings.AdminApiKeys));
            Console.WriteLine("");
            if (embeddingEndpointSummaries.Count > 0)
            {
                Console.WriteLine("Embedding endpoints:");
                foreach (string summary in embeddingEndpointSummaries)
                    Console.WriteLine("  " + summary);
            }
            if (inferenceEndpointSummaries.Count > 0)
            {
                Console.WriteLine("Inference endpoints:");
                foreach (string summary in inferenceEndpointSummaries)
                    Console.WriteLine("  " + summary);
            }
            Console.WriteLine("");
            Console.WriteLine("WARNING: Change these credentials before production use!");
            Console.WriteLine("");
            Console.WriteLine("=====================");
            Console.WriteLine();
        }

        private static async Task ReconcileDefaultEmbeddingEndpointAsync()
        {
            if (_Settings.DefaultEmbeddingEndpoints == null || _Settings.DefaultEmbeddingEndpoints.Count < 1)
                return;

            DefaultEmbeddingEndpoint configuredDefault = _Settings.DefaultEmbeddingEndpoints[0];
            EmbeddingEndpoint? existing = await _Database.EmbeddingEndpoint.ReadByIdAsync("default").ConfigureAwait(false);
            if (existing == null)
                existing = await _Database.EmbeddingEndpoint.ReadByModelAsync("default", configuredDefault.Model).ConfigureAwait(false);

            if (existing == null)
            {
                EmbeddingEndpoint created = BuildConfiguredDefaultEmbeddingEndpoint(configuredDefault);
                await _Database.EmbeddingEndpoint.CreateAsync(created).ConfigureAwait(false);
                _Logging.Info(_Header + "created default embedding endpoint from settings for tenant default");
                return;
            }

            if (!string.Equals(existing.TenantId, "default", StringComparison.OrdinalIgnoreCase))
                return;

            EndpointTokenizationSettings? configuredTokenization = CloneTokenizationSettings(configuredDefault.Tokenization);
            bool changed =
                !string.Equals(existing.Name, configuredDefault.Name, StringComparison.Ordinal)
                || !string.Equals(existing.Model, configuredDefault.Model, StringComparison.Ordinal)
                || !string.Equals(existing.Endpoint, configuredDefault.Endpoint, StringComparison.Ordinal)
                || existing.ApiFormat != configuredDefault.ApiFormat
                || !string.Equals(existing.ApiKey, configuredDefault.ApiKey, StringComparison.Ordinal)
                || existing.MaximumTimeoutMs != configuredDefault.MaximumTimeoutMs
                || existing.MaxConcurrentRequests != configuredDefault.MaxConcurrentRequests
                || !TokenizationSettingsEqual(existing.Tokenization, configuredTokenization);

            if (!changed) return;

            existing.Name = configuredDefault.Name;
            existing.Model = configuredDefault.Model;
            existing.Endpoint = configuredDefault.Endpoint;
            existing.ApiFormat = configuredDefault.ApiFormat;
            existing.ApiKey = configuredDefault.ApiKey;
            existing.MaximumTimeoutMs = configuredDefault.MaximumTimeoutMs;
            existing.MaxConcurrentRequests = configuredDefault.MaxConcurrentRequests;
            existing.Tokenization = configuredTokenization;
            EmbeddingEndpoint.ApplyHealthCheckDefaults(existing);

            await _Database.EmbeddingEndpoint.UpdateAsync(existing).ConfigureAwait(false);
            _Logging.Info(_Header + "reconciled default embedding endpoint from settings for tenant default");
        }

        private static EmbeddingEndpoint BuildConfiguredDefaultEmbeddingEndpoint(DefaultEmbeddingEndpoint configuredDefault)
        {
            EmbeddingEndpoint endpoint = new EmbeddingEndpoint();
            endpoint.Id = "default";
            endpoint.TenantId = "default";
            endpoint.Name = configuredDefault.Name;
            endpoint.Model = configuredDefault.Model;
            endpoint.Endpoint = configuredDefault.Endpoint;
            endpoint.ApiFormat = configuredDefault.ApiFormat;
            endpoint.ApiKey = configuredDefault.ApiKey;
            endpoint.MaximumTimeoutMs = configuredDefault.MaximumTimeoutMs;
            endpoint.MaxConcurrentRequests = configuredDefault.MaxConcurrentRequests;
            endpoint.Tokenization = CloneTokenizationSettings(configuredDefault.Tokenization);
            endpoint.HealthCheckEnabled = true;
            EmbeddingEndpoint.ApplyHealthCheckDefaults(endpoint);
            return endpoint;
        }

        private static EndpointTokenizationSettings? CloneTokenizationSettings(EndpointTokenizationSettings? settings)
        {
            if (settings == null) return null;

            return new EndpointTokenizationSettings
            {
                TokenizerKind = settings.TokenizerKind,
                TokenizerModel = settings.TokenizerModel,
                MaxInputTokens = settings.MaxInputTokens,
                ReservedInputTokens = settings.ReservedInputTokens,
                EffectiveInputBudget = settings.EffectiveInputBudget,
                BatchLimitMode = settings.BatchLimitMode,
                AutoDetect = settings.AutoDetect
            };
        }

        private static bool TokenizationSettingsEqual(EndpointTokenizationSettings? left, EndpointTokenizationSettings? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;

            return left.TokenizerKind == right.TokenizerKind
                && string.Equals(left.TokenizerModel, right.TokenizerModel, StringComparison.Ordinal)
                && left.MaxInputTokens == right.MaxInputTokens
                && left.ReservedInputTokens == right.ReservedInputTokens
                && left.EffectiveInputBudget == right.EffectiveInputBudget
                && left.BatchLimitMode == right.BatchLimitMode
                && left.AutoDetect == right.AutoDetect;
        }

        #endregion

        #region Health

        private static async Task<object> HealthHead(ApiRequest req)
        {
            req.Http.Response.StatusCode = 200;
            return null!;
        }

        private static async Task<object> HealthGet(ApiRequest req)
        {
            req.Http.Response.StatusCode = 200;
            return new Dictionary<string, object>
            {
                { "Status", "Healthy" },
                { "Version", Constants.Version },
                { "Uptime", DateTime.UtcNow - _StartTimeUtc }
            };
        }

        private static async Task<object> HealthJson(ApiRequest req)
        {
            req.Http.Response.StatusCode = 200;
            return new Dictionary<string, object>
            {
                { "Status", "Healthy" },
                { "Version", Constants.Version },
                { "Uptime", DateTime.UtcNow - _StartTimeUtc }
            };
        }

        private static async Task<object> WhoAmI(ApiRequest req)
        {
            AuthContext auth = (AuthContext)req.Metadata;

            if (auth.IsGlobalAdmin)
            {
                return new Dictionary<string, string>
                {
                    { "Role", "Admin" },
                    { "TenantName", "Admin" }
                };
            }

            TenantMetadata? tenant = await _Database.Tenant.ReadByIdAsync(auth.TenantId).ConfigureAwait(false);
            UserMaster? user = await _Database.User.ReadByIdAsync(auth.UserId).ConfigureAwait(false);

            return new Dictionary<string, string>
            {
                { "Role", user != null && user.IsAdmin ? "Admin" : "User" },
                { "TenantName", tenant?.Name ?? "Unknown" }
            };
        }

        #endregion

        #region Process

        private static async Task<object> ChunkOnly(ApiRequest req)
        {
            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);
            CancellationToken token = GetRequestCancellationToken(req);

            ChunkRequest? chunkReq = null;

            try
            {
                token.ThrowIfCancellationRequested();
                chunkReq = req.GetData<ChunkRequest>();
                if (chunkReq == null) throw new ArgumentException("Request body is required.");

                SemanticCellRequest cell = new SemanticCellRequest
                {
                    GUID = chunkReq.GUID,
                    Type = chunkReq.Type,
                    Text = chunkReq.Text,
                    UnorderedList = chunkReq.UnorderedList,
                    OrderedList = chunkReq.OrderedList,
                    Table = chunkReq.Table,
                    Binary = chunkReq.Binary,
                    ChunkingConfiguration = chunkReq.ChunkingConfiguration ?? new ChunkingConfiguration(),
                    Labels = chunkReq.Labels,
                    Tags = chunkReq.Tags
                };

                ValidateStrategyForAtomType(cell);

                if (cell.ChunkingConfiguration.Strategy == ChunkStrategyEnum.RegexBased)
                {
                    if (string.IsNullOrWhiteSpace(cell.ChunkingConfiguration.RegexPattern))
                        throw new ArgumentException("RegexPattern is required when using the RegexBased strategy.");
                    try
                    {
                        _ = new Regex(cell.ChunkingConfiguration.RegexPattern, RegexOptions.None, TimeSpan.FromSeconds(5));
                    }
                    catch (ArgumentException ex)
                    {
                        throw new ArgumentException("RegexPattern is not a valid regular expression: " + ex.Message);
                    }
                }

                // Chunk-only uses a built-in tokenizer so no embedding endpoint is needed; the token budget
                // comes straight from the requested FixedTokenCount (there is no endpoint budget to cap against).
                ITokenizerAdapter tokenizer = new SharpTokenTokenizerAdapter("cl100k_base");
                int tokenBudget = Math.Max(1, cell.ChunkingConfiguration.FixedTokenCount);

                if (!string.IsNullOrEmpty(cell.ChunkingConfiguration.ContextPrefix))
                {
                    int contextPrefixTokens = tokenizer.CountTokens(cell.ChunkingConfiguration.ContextPrefix);
                    if (contextPrefixTokens >= tokenBudget)
                        throw new ArgumentException("ContextPrefix consumes the entire chunking token budget.");
                    tokenBudget = Math.Max(1, tokenBudget - contextPrefixTokens);
                }

                List<ChunkResult> chunks = _ChunkingEngine.Chunk(cell, tokenizer, tokenBudget);
                chunks = NormalizeChunksForEmbeddingBudget(chunks, tokenizer, tokenBudget);

                List<string> labels = cell.Labels ?? new List<string>();
                Dictionary<string, string> tags = cell.Tags ?? new Dictionary<string, string>();
                foreach (ChunkResult chunk in chunks)
                {
                    chunk.CellGUID = cell.GUID;
                    chunk.Labels = new List<string>(labels);
                    chunk.Tags = new Dictionary<string, string>(tags);
                }

                ChunkResponse response = new ChunkResponse
                {
                    GUID = cell.GUID,
                    Type = cell.Type,
                    Text = cell.Text ?? string.Empty,
                    Chunks = chunks,
                    Count = chunks.Count
                };

                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    string requestJson = _Serializer.SerializeJson(chunkReq, false);
                    string responseJson = _Serializer.SerializeJson(response, false);
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await RecordDetailedHistoryAsync(inflight.Entry, 200, inflight.Stopwatch.Elapsed.TotalMilliseconds,
                        requestJson, responseJson, reqHeaders, respHeaders, null, null, null).ConfigureAwait(false);
                    inflight.DetailRecorded = true;
                }

                return response;
            }
            catch (Exception ex)
            {
                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    int statusCode = MapExceptionToStatusCode(ex);
                    string? requestBody = chunkReq != null ? _Serializer.SerializeJson(chunkReq, false) : null;
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await RecordDetailedHistoryAsync(inflight.Entry, statusCode, inflight.Stopwatch.Elapsed.TotalMilliseconds,
                        requestBody, ex.Message, reqHeaders, respHeaders, null, null, null).ConfigureAwait(false);
                    inflight.DetailRecorded = true;
                }
                throw;
            }
        }

        private static async Task<object> EmbedTexts(ApiRequest req)
        {
            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);
            CancellationToken token = GetRequestCancellationToken(req);

            EmbedResponse response = new EmbedResponse();
            EmbedRequest? embedReq = null;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                token.ThrowIfCancellationRequested();
                AuthContext auth = (AuthContext)req.Metadata;
                embedReq = req.GetData<EmbedRequest>();
                if (embedReq == null) throw new ArgumentException("Request body is required.");
                if (string.IsNullOrWhiteSpace(embedReq.EndpointId)) throw new ArgumentException("EndpointId is required.");

                List<string> inputs = (embedReq.Input ?? new List<string>()).Where(x => x != null).ToList();
                if (inputs.Count == 0) throw new ArgumentException("At least one input is required.");

                EmbeddingEndpoint endpoint = await ResolveEmbeddingEndpointFromBody(embedReq.EndpointId, auth, token).ConfigureAwait(false);
                response.EndpointId = endpoint.Id;
                response.Model = endpoint.Model;
                response.L2Normalization = embedReq.L2Normalization;
                if (inflight != null) response.RequestHistoryId = inflight.Entry.Id;

                req.Http.Response.Headers.Add(Constants.EndpointIdHeader, endpoint.Id);
                req.Http.Response.Headers.Add(Constants.ModelHeader, endpoint.Model);

                using EmbeddingClientBase client = CreateEmbeddingClient(endpoint);
                response.TokenizationProfile = await _TokenizationResolver.ResolveAsync(endpoint, endpoint.Model, client, token: token).ConfigureAwait(false);
                ApplyRuntimeEmbeddingSafeguards(endpoint, response.TokenizationProfile);
                ApplyTokenizationProfileHeaders(req.Http.Response.Headers, response.TokenizationProfile);

                List<List<float>> embeddings = await client.EmbedBatchAsync(inputs, endpoint.Model, token).ConfigureAwait(false);
                if (embedReq.L2Normalization)
                {
                    for (int i = 0; i < embeddings.Count; i++) embeddings[i] = client.NormalizeL2(embeddings[i]);
                }

                sw.Stop();
                response.Success = true;
                response.StatusCode = 200;
                response.Embeddings = embeddings;
                response.Count = embeddings.Count;
                response.Dimensions = embeddings.Count > 0 ? embeddings[0].Count : 0;
                response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                response.EmbeddingCalls = client.CallDetails.ToList();

                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    string requestJson = _Serializer.SerializeJson(embedReq, false);
                    string responseJson = _Serializer.SerializeJson(response, false);
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await RecordDetailedHistoryAsync(inflight.Entry, 200, inflight.Stopwatch.Elapsed.TotalMilliseconds,
                        requestJson, responseJson, reqHeaders, respHeaders, response.EmbeddingCalls, null,
                        BuildTokenizationDetail(response.TokenizationProfile)).ConfigureAwait(false);
                    inflight.DetailRecorded = true;
                }

                return response;
            }
            catch (Exception ex)
            {
                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    int statusCode = MapExceptionToStatusCode(ex);
                    string? requestBody = embedReq != null ? _Serializer.SerializeJson(embedReq, false) : null;
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await RecordDetailedHistoryAsync(inflight.Entry, statusCode, inflight.Stopwatch.Elapsed.TotalMilliseconds,
                        requestBody, ex.Message, reqHeaders, respHeaders, null, null, null).ConfigureAwait(false);
                    inflight.DetailRecorded = true;
                }
                throw;
            }
        }

        private static async Task<object> SummarizeText(ApiRequest req)
        {
            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);
            CancellationToken token = GetRequestCancellationToken(req);

            SummarizeResponse response = new SummarizeResponse();
            SummarizeRequest? sumReq = null;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                token.ThrowIfCancellationRequested();
                AuthContext auth = (AuthContext)req.Metadata;
                sumReq = req.GetData<SummarizeRequest>();
                if (sumReq == null) throw new ArgumentException("Request body is required.");
                if (string.IsNullOrWhiteSpace(sumReq.Text)) throw new ArgumentException("Text is required.");
                if (sumReq.SummarizationConfiguration == null) throw new ArgumentException("SummarizationConfiguration is required.");
                if (string.IsNullOrWhiteSpace(sumReq.SummarizationConfiguration.CompletionEndpointId))
                    throw new ArgumentException("SummarizationConfiguration.CompletionEndpointId is required.");

                CompletionEndpoint endpoint = await ResolveCompletionEndpointFromBody(sumReq.SummarizationConfiguration.CompletionEndpointId, auth, token).ConfigureAwait(false);
                response.CompletionEndpointId = endpoint.Id;
                response.Model = endpoint.Model;
                if (inflight != null) response.RequestHistoryId = inflight.Entry.Id;

                req.Http.Response.Headers.Add(Constants.EndpointIdHeader, endpoint.Id);
                req.Http.Response.Headers.Add(Constants.ModelHeader, endpoint.Model);

                using CompletionClientBase client = CreateCompletionClient(endpoint);

                SemanticCellRequest cell = new SemanticCellRequest { Type = AtomTypeEnum.Text, Text = sumReq.Text };
                SummarizationEngine summarizer = new SummarizationEngine(_Logging);
                List<SemanticCellRequest> resultCells = await summarizer.SummarizeAsync(
                    new List<SemanticCellRequest> { cell }, sumReq.SummarizationConfiguration, client, endpoint.Model, token).ConfigureAwait(false);

                List<string> summaries = new List<string>();
                CollectSummaries(resultCells, summaries);

                sw.Stop();
                response.Success = true;
                response.StatusCode = 200;
                response.Summaries = summaries;
                response.Summary = summaries.Count > 0 ? summaries[0] : string.Empty;
                response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                response.CompletionCalls = client.CallDetails.ToList();

                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    string requestJson = _Serializer.SerializeJson(sumReq, false);
                    string responseJson = _Serializer.SerializeJson(response, false);
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await RecordDetailedHistoryAsync(inflight.Entry, 200, inflight.Stopwatch.Elapsed.TotalMilliseconds,
                        requestJson, responseJson, reqHeaders, respHeaders, null, response.CompletionCalls).ConfigureAwait(false);
                    inflight.DetailRecorded = true;
                }

                return response;
            }
            catch (Exception ex)
            {
                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    int statusCode = MapExceptionToStatusCode(ex);
                    string? requestBody = sumReq != null ? _Serializer.SerializeJson(sumReq, false) : null;
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await RecordDetailedHistoryAsync(inflight.Entry, statusCode, inflight.Stopwatch.Elapsed.TotalMilliseconds,
                        requestBody, ex.Message, reqHeaders, respHeaders, null, null).ConfigureAwait(false);
                    inflight.DetailRecorded = true;
                }
                throw;
            }
        }

        private static void CollectSummaries(List<SemanticCellRequest> cells, List<string> summaries)
        {
            if (cells == null) return;
            foreach (SemanticCellRequest cell in cells)
            {
                if (cell.Type == AtomTypeEnum.Summary && !string.IsNullOrEmpty(cell.Text)) summaries.Add(cell.Text!);
                if (cell.Children != null && cell.Children.Count > 0) CollectSummaries(cell.Children, summaries);
            }
        }

        private static async Task<object> ProcessSingle(ApiRequest req)
        {
            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);
            CancellationToken token = GetRequestCancellationToken(req);

            SemanticCellRequest? cellReq = null;

            try
            {
                token.ThrowIfCancellationRequested();
                AuthContext auth = (AuthContext)req.Metadata;
                cellReq = req.GetData<SemanticCellRequest>();
                if (cellReq == null) throw new ArgumentException("Request body is required.");

                EmbeddingEndpoint endpoint = await ResolveEmbeddingEndpointFromBody(cellReq.EmbeddingConfiguration.EmbeddingEndpointId, auth, token).ConfigureAwait(false);

                req.Http.Response.Headers.Add(Constants.EndpointIdHeader, endpoint.Id);
                req.Http.Response.Headers.Add(Constants.ModelHeader, endpoint.Model);

                ProcessCellResult cellResult = await ProcessCellAsync(cellReq, endpoint, token).ConfigureAwait(false);
                ApplyTokenizationProfileHeaders(req.Http.Response.Headers, cellResult.TokenizationProfile);

                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    string requestJson = _Serializer.SerializeJson(cellReq, false);
                    string responseJson = _Serializer.SerializeJson(cellResult.Response, false);
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await RecordDetailedHistoryAsync(
                        inflight.Entry,
                        200,
                        inflight.Stopwatch.Elapsed.TotalMilliseconds,
                        requestJson,
                        responseJson,
                        reqHeaders,
                        respHeaders,
                        cellResult.EmbeddingCalls,
                        cellResult.CompletionCalls,
                        BuildTokenizationDetail(cellResult.TokenizationProfile, cellResult.ChunkDiagnostics)).ConfigureAwait(false);
                    inflight.DetailRecorded = true;
                }

                return cellResult.Response;
            }
            catch (Exception ex)
            {
                ProcessCellResult? partialResult = ex is ProcessCellException ? ((ProcessCellException)ex).Result : null;
                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    int statusCode = MapExceptionToStatusCode(ex is ProcessCellException && ex.InnerException != null ? ex.InnerException : ex);
                    string? requestBody = cellReq != null ? _Serializer.SerializeJson(cellReq, false) : null;
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await RecordDetailedHistoryAsync(
                        inflight.Entry,
                        statusCode,
                        inflight.Stopwatch.Elapsed.TotalMilliseconds,
                        requestBody,
                        ex.InnerException?.Message ?? ex.Message,
                        reqHeaders,
                        respHeaders,
                        partialResult?.EmbeddingCalls,
                        partialResult?.CompletionCalls,
                        BuildTokenizationDetail(partialResult?.TokenizationProfile, partialResult?.ChunkDiagnostics)).ConfigureAwait(false);
                    inflight.DetailRecorded = true;
                }
                throw ex is ProcessCellException && ex.InnerException != null ? ex.InnerException : ex;
            }
        }

        private static async Task<object> ProcessBatch(ApiRequest req)
        {
            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);
            CancellationToken token = GetRequestCancellationToken(req);

            List<SemanticCellRequest>? cellReqs = null;
            List<SemanticCellResponse> responses = new List<SemanticCellResponse>();
            List<EmbeddingCallDetail> allEmbeddingCalls = new List<EmbeddingCallDetail>();
            List<CompletionCallDetail> allCompletionCalls = new List<CompletionCallDetail>();
            List<ChunkProcessingDiagnostic> allChunkDiagnostics = new List<ChunkProcessingDiagnostic>();
            ResolvedTokenizationProfile? tokenizationProfile = null;

            try
            {
                token.ThrowIfCancellationRequested();
                AuthContext auth = (AuthContext)req.Metadata;
                cellReqs = req.GetData<List<SemanticCellRequest>>();
                if (cellReqs == null || cellReqs.Count == 0) throw new ArgumentException("Request body must contain at least one cell.");

                Dictionary<string, EmbeddingEndpoint> endpointCache = new Dictionary<string, EmbeddingEndpoint>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> endpointIdsUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> modelsUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (SemanticCellRequest cellReq in cellReqs)
                {
                    string embeddingEndpointId = cellReq.EmbeddingConfiguration.EmbeddingEndpointId;
                    if (!endpointCache.TryGetValue(embeddingEndpointId, out EmbeddingEndpoint? endpoint))
                    {
                        endpoint = await ResolveEmbeddingEndpointFromBody(embeddingEndpointId, auth, token).ConfigureAwait(false);
                        endpointCache[embeddingEndpointId] = endpoint;
                    }

                    token.ThrowIfCancellationRequested();
                    endpointIdsUsed.Add(endpoint.Id);
                    modelsUsed.Add(endpoint.Model);
                    ProcessCellResult cellResult = await ProcessCellAsync(cellReq, endpoint, token).ConfigureAwait(false);
                    responses.Add(cellResult.Response);
                    allEmbeddingCalls.AddRange(cellResult.EmbeddingCalls);
                    allCompletionCalls.AddRange(cellResult.CompletionCalls);
                    allChunkDiagnostics.AddRange(cellResult.ChunkDiagnostics);
                    tokenizationProfile ??= cellResult.TokenizationProfile;
                }

                if (endpointIdsUsed.Count == 1)
                    req.Http.Response.Headers[Constants.EndpointIdHeader] = endpointIdsUsed.First();
                if (modelsUsed.Count == 1)
                    req.Http.Response.Headers[Constants.ModelHeader] = modelsUsed.First();

                ApplyTokenizationProfileHeaders(req.Http.Response.Headers, tokenizationProfile);

                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    string requestJson = _Serializer.SerializeJson(cellReqs, false);
                    string responseJson = _Serializer.SerializeJson(responses, false);
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await RecordDetailedHistoryAsync(
                        inflight.Entry,
                        200,
                        inflight.Stopwatch.Elapsed.TotalMilliseconds,
                        requestJson,
                        responseJson,
                        reqHeaders,
                        respHeaders,
                        allEmbeddingCalls,
                        allCompletionCalls,
                        BuildTokenizationDetail(tokenizationProfile, allChunkDiagnostics)).ConfigureAwait(false);
                    inflight.DetailRecorded = true;
                }

                return responses;
            }
            catch (Exception ex)
            {
                if (ex is ProcessCellException processEx)
                {
                    allEmbeddingCalls.AddRange(processEx.Result.EmbeddingCalls);
                    allCompletionCalls.AddRange(processEx.Result.CompletionCalls);
                    allChunkDiagnostics.AddRange(processEx.Result.ChunkDiagnostics);
                    tokenizationProfile ??= processEx.Result.TokenizationProfile;
                }

                ProcessCellResult? partialResult = ex is ProcessCellException ? ((ProcessCellException)ex).Result : null;
                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    int statusCode = MapExceptionToStatusCode(ex is ProcessCellException && ex.InnerException != null ? ex.InnerException : ex);
                    string? requestBody = cellReqs != null ? _Serializer.SerializeJson(cellReqs, false) : null;
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await RecordDetailedHistoryAsync(
                        inflight.Entry,
                        statusCode,
                        inflight.Stopwatch.Elapsed.TotalMilliseconds,
                        requestBody,
                        ex.InnerException?.Message ?? ex.Message,
                        reqHeaders,
                        respHeaders,
                        allEmbeddingCalls.Count > 0 ? allEmbeddingCalls : partialResult?.EmbeddingCalls,
                        allCompletionCalls.Count > 0 ? allCompletionCalls : partialResult?.CompletionCalls,
                        BuildTokenizationDetail(
                            tokenizationProfile ?? partialResult?.TokenizationProfile,
                            allChunkDiagnostics.Count > 0 ? allChunkDiagnostics : partialResult?.ChunkDiagnostics)).ConfigureAwait(false);
                    inflight.DetailRecorded = true;
                }
                throw ex is ProcessCellException && ex.InnerException != null ? ex.InnerException : ex;
            }
        }

        private static async Task<EmbeddingEndpoint> ResolveEmbeddingEndpointFromBody(string id, AuthContext auth, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("EmbeddingConfiguration.EmbeddingEndpointId is required.");

            EmbeddingEndpoint? endpoint = await _Database.EmbeddingEndpoint.ReadByIdAsync(id, token).ConfigureAwait(false);

            // Return 404 if not found, or if non-admin caller's tenant doesn't match
            if (endpoint == null || (!auth.IsGlobalAdmin && endpoint.TenantId != auth.TenantId))
                throw new KeyNotFoundException("Embedding endpoint not found: " + id);

            if (!endpoint.Active)
                throw new ArgumentException("Embedding endpoint '" + id + "' is inactive.");

            if (_HealthCheckService != null && !_HealthCheckService.IsHealthy(endpoint.Id))
                throw new EndpointUnhealthyException(endpoint.Id,
                    "Endpoint " + endpoint.Id + " (" + endpoint.Model + ") is currently unhealthy");

            return endpoint;
        }

        private static async Task RecordDetailedHistoryAsync(
            RequestHistoryEntry entry,
            int statusCode,
            double responseTimeMs,
            string? requestBody,
            string? responseBody,
            Dictionary<string, string>? requestHeaders = null,
            Dictionary<string, string>? responseHeaders = null,
            List<EmbeddingCallDetail>? embeddingCalls = null,
            List<CompletionCallDetail>? completionCalls = null,
            Dictionary<string, object?>? additionalDetail = null)
        {
            if (_RequestHistoryService == null)
                return;

            await _RequestHistoryService.UpdateWithResponseAsync(
                entry,
                statusCode,
                responseTimeMs,
                requestBody,
                responseBody,
                requestHeaders,
                responseHeaders,
                embeddingCalls,
                completionCalls,
                additionalDetail).ConfigureAwait(false);
        }

        private static async Task<CompletionEndpoint> ResolveCompletionEndpointFromBody(string id, AuthContext auth, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Completion endpoint ID is required.");

            CompletionEndpoint? endpoint = await _Database.CompletionEndpoint.ReadByIdAsync(id, token).ConfigureAwait(false);

            if (endpoint == null || (!auth.IsGlobalAdmin && endpoint.TenantId != auth.TenantId))
                throw new KeyNotFoundException("Completion endpoint not found: " + id);

            if (!endpoint.Active)
                throw new ArgumentException("Completion endpoint '" + id + "' is inactive.");

            if (_CompletionHealthCheckService != null && !_CompletionHealthCheckService.IsHealthy(endpoint.Id))
                throw new EndpointUnhealthyException(endpoint.Id,
                    "Completion endpoint " + endpoint.Id + " (" + endpoint.Model + ") is currently unhealthy");

            return endpoint;
        }

        private static async Task<EmbeddingEndpoint> ResolveEmbeddingEndpointForLoadAsync(string id, AuthContext auth, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Embedding endpoint ID is required.");

            EmbeddingEndpoint? endpoint = await _Database.EmbeddingEndpoint.ReadByIdAsync(id, token).ConfigureAwait(false);
            if (endpoint == null || (!auth.IsGlobalAdmin && endpoint.TenantId != auth.TenantId))
                throw new KeyNotFoundException("Embedding endpoint not found: " + id);

            if (!endpoint.Active)
                throw new ArgumentException("Embedding endpoint '" + id + "' is inactive.");

            return endpoint;
        }

        private static async Task<CompletionEndpoint> ResolveCompletionEndpointForLoadAsync(string id, AuthContext auth, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Completion endpoint ID is required.");

            CompletionEndpoint? endpoint = await _Database.CompletionEndpoint.ReadByIdAsync(id, token).ConfigureAwait(false);
            if (endpoint == null || (!auth.IsGlobalAdmin && endpoint.TenantId != auth.TenantId))
                throw new KeyNotFoundException("Completion endpoint not found: " + id);

            if (!endpoint.Active)
                throw new ArgumentException("Completion endpoint '" + id + "' is inactive.");

            return endpoint;
        }

        private static async Task<object> LoadEmbeddingEndpointModel(ApiRequest req)
        {
            RequireAdmin(req);

            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);
            CancellationToken token = GetRequestCancellationToken(req);
            ModelLoadRequest? loadReq = req.GetData<ModelLoadRequest>();
            if (loadReq == null) loadReq = new ModelLoadRequest();
            ValidateModelLoadRequest(loadReq);

            AuthContext auth = (AuthContext)req.Metadata;
            string id = req.Parameters["id"];
            EmbeddingEndpoint endpoint = await ResolveEmbeddingEndpointForLoadAsync(id, auth, token).ConfigureAwait(false);

            req.Http.Response.Headers[Constants.EndpointIdHeader] = endpoint.Id;
            req.Http.Response.Headers[Constants.ModelHeader] = endpoint.Model;
            req.Http.Response.Headers[Constants.PartioModelHeader] = endpoint.Model;

            ModelLoadResponse response = await _ModelLoadService
                .LoadEmbeddingEndpointAsync(endpoint, loadReq, inflight?.Entry.Id, token)
                .ConfigureAwait(false);
            req.Http.Response.StatusCode = response.StatusCode;

            await RecordModelLoadHistoryAsync(req, inflight, loadReq, response).ConfigureAwait(false);
            return response;
        }

        private static async Task<object> LoadCompletionEndpointModel(ApiRequest req)
        {
            RequireAdmin(req);

            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);
            CancellationToken token = GetRequestCancellationToken(req);
            ModelLoadRequest? loadReq = req.GetData<ModelLoadRequest>();
            if (loadReq == null) loadReq = new ModelLoadRequest();
            ValidateModelLoadRequest(loadReq);

            AuthContext auth = (AuthContext)req.Metadata;
            string id = req.Parameters["id"];
            CompletionEndpoint endpoint = await ResolveCompletionEndpointForLoadAsync(id, auth, token).ConfigureAwait(false);

            req.Http.Response.Headers[Constants.EndpointIdHeader] = endpoint.Id;
            req.Http.Response.Headers[Constants.ModelHeader] = endpoint.Model;
            req.Http.Response.Headers[Constants.PartioModelHeader] = endpoint.Model;

            ModelLoadResponse response = await _ModelLoadService
                .LoadCompletionEndpointAsync(endpoint, loadReq, inflight?.Entry.Id, token)
                .ConfigureAwait(false);
            req.Http.Response.StatusCode = response.StatusCode;

            await RecordModelLoadHistoryAsync(req, inflight, loadReq, response).ConfigureAwait(false);
            return response;
        }

        private static async Task RecordModelLoadHistoryAsync(
            ApiRequest req,
            InFlightRequest? inflight,
            ModelLoadRequest request,
            ModelLoadResponse response)
        {
            if (inflight == null || !request.RecordRequestHistory)
                return;

            inflight.Stopwatch.Stop();
            string requestJson = _Serializer.SerializeJson(request, false);
            string responseJson = _Serializer.SerializeJson(response, false);
            Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
            Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
            await RecordDetailedHistoryAsync(
                inflight.Entry,
                response.StatusCode,
                inflight.Stopwatch.Elapsed.TotalMilliseconds,
                requestJson,
                responseJson,
                reqHeaders,
                respHeaders,
                response.EmbeddingCalls,
                response.CompletionCalls,
                BuildModelLoadDetail(response)).ConfigureAwait(false);
            inflight.DetailRecorded = true;
        }

        private static Dictionary<string, object?> BuildModelLoadDetail(ModelLoadResponse response)
        {
            Dictionary<string, object?> metadata = new Dictionary<string, object?>();
            metadata["EndpointType"] = response.EndpointType.ToString();
            metadata["EndpointId"] = response.EndpointId;
            metadata["TenantId"] = response.TenantId;
            metadata["ApiFormat"] = response.ApiFormat.ToString();
            metadata["Model"] = response.Model;
            metadata["Strategy"] = response.Strategy.ToString();
            metadata["Outcome"] = response.Outcome.ToString();
            metadata["Success"] = response.Success;
            metadata["ResponseTimeMs"] = response.ResponseTimeMs;

            Dictionary<string, object?> detail = new Dictionary<string, object?>();
            detail["ModelLoad"] = metadata;
            return detail;
        }

        private static void ValidateModelLoadRequest(ModelLoadRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!string.IsNullOrWhiteSpace(request.KeepAlive) && IsUnloadKeepAliveValue(request.KeepAlive))
                throw new ArgumentException("KeepAlive must not request unload. Use a positive duration such as 30m.");
        }

        private static bool IsUnloadKeepAliveValue(string value)
        {
            string normalized = value.Trim().ToLowerInvariant();
            return Regex.IsMatch(normalized, "^0+(\\.0+)?(ms|s|m|h)?$");
        }

        private static async Task<object> ExploreEmbeddingEndpoint(ApiRequest req)
        {
            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);
            CancellationToken token = GetRequestCancellationToken(req);

            EndpointExplorerEmbeddingResponse response = new EndpointExplorerEmbeddingResponse();
            EndpointExplorerEmbeddingRequest? explorerReq = null;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                token.ThrowIfCancellationRequested();
                AuthContext auth = (AuthContext)req.Metadata;
                explorerReq = req.GetData<EndpointExplorerEmbeddingRequest>();
                if (explorerReq == null) throw new ArgumentException("Request body is required.");
                if (string.IsNullOrWhiteSpace(explorerReq.EndpointId)) throw new ArgumentException("EndpointId is required.");
                if (string.IsNullOrWhiteSpace(explorerReq.Input)) throw new ArgumentException("Input is required.");

                EmbeddingEndpoint endpoint = await ResolveEmbeddingEndpointFromBody(explorerReq.EndpointId, auth, token).ConfigureAwait(false);
                response.EndpointId = endpoint.Id;
                response.Model = endpoint.Model;
                response.Input = explorerReq.Input;

                if (inflight != null)
                    response.RequestHistoryId = inflight.Entry.Id;

                req.Http.Response.Headers.Add(Constants.EndpointIdHeader, endpoint.Id);
                req.Http.Response.Headers.Add(Constants.ModelHeader, endpoint.Model);

                using EmbeddingClientBase client = CreateEmbeddingClient(endpoint);
                response.TokenizationProfile = await _TokenizationResolver.ResolveAsync(endpoint, endpoint.Model, client, token: token).ConfigureAwait(false);
                ApplyRuntimeEmbeddingSafeguards(endpoint, response.TokenizationProfile);
                ApplyTokenizationProfileHeaders(req.Http.Response.Headers, response.TokenizationProfile);
                int startCallIndex = client.CallDetails.Count;

                try
                {
                    List<float> embedding = await client.EmbedAsync(explorerReq.Input, endpoint.Model, token).ConfigureAwait(false);
                    if (response.TokenizationProfile != null)
                    {
                        BatchLimitModeEnum appliedBatchLimitMode = response.TokenizationProfile.BatchLimitMode == BatchLimitModeEnum.Unknown
                            ? BatchLimitModeEnum.WholeRequest
                            : response.TokenizationProfile.BatchLimitMode;
                        AnnotateEmbeddingRequestCalls(
                            client,
                            startCallIndex,
                            new List<string> { explorerReq.Input },
                            TokenizerAdapterFactory.Create(response.TokenizationProfile),
                            response.TokenizationProfile,
                            appliedBatchLimitMode,
                            null);
                    }
                    if (explorerReq.L2Normalization)
                        embedding = client.NormalizeL2(embedding);

                    sw.Stop();

                    response.Success = true;
                    response.StatusCode = 200;
                    response.Embedding = embedding;
                    response.Dimensions = embedding.Count;
                    response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                    response.EmbeddingCalls = client.CallDetails.ToList();

                    if (inflight != null)
                    {
                        inflight.Stopwatch.Stop();
                        string requestJson = _Serializer.SerializeJson(explorerReq, false);
                        string responseJson = _Serializer.SerializeJson(response, false);
                        Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                        Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                        await RecordDetailedHistoryAsync(
                            inflight.Entry,
                            200,
                            inflight.Stopwatch.Elapsed.TotalMilliseconds,
                            requestJson,
                            responseJson,
                            reqHeaders,
                            respHeaders,
                            response.EmbeddingCalls,
                            null,
                            BuildTokenizationDetail(response.TokenizationProfile)).ConfigureAwait(false);
                        inflight.DetailRecorded = true;
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    if (ex is ProviderConcurrencyLimitException)
                    {
                        sw.Stop();

                        response.Success = false;
                        response.StatusCode = 429;
                        response.Error = ex.Message;
                        response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                        if (response.TokenizationProfile != null)
                        {
                            BatchLimitModeEnum appliedBatchLimitMode = response.TokenizationProfile.BatchLimitMode == BatchLimitModeEnum.Unknown
                                ? BatchLimitModeEnum.WholeRequest
                                : response.TokenizationProfile.BatchLimitMode;
                            AnnotateEmbeddingRequestCalls(
                                client,
                                startCallIndex,
                                new List<string> { explorerReq.Input ?? string.Empty },
                                TokenizerAdapterFactory.Create(response.TokenizationProfile),
                                response.TokenizationProfile,
                                appliedBatchLimitMode,
                                "Upstream embedding request failed.");
                        }
                        response.EmbeddingCalls = client.CallDetails.ToList();

                        if (inflight != null)
                        {
                            inflight.Stopwatch.Stop();
                            string requestJson = _Serializer.SerializeJson(explorerReq, false);
                            string responseJson = _Serializer.SerializeJson(response, false);
                            Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                            Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                            await RecordDetailedHistoryAsync(
                                inflight.Entry,
                                response.StatusCode,
                                inflight.Stopwatch.Elapsed.TotalMilliseconds,
                                requestJson,
                                responseJson,
                                reqHeaders,
                                respHeaders,
                                response.EmbeddingCalls,
                                null,
                                BuildTokenizationDetail(response.TokenizationProfile)).ConfigureAwait(false);
                            inflight.DetailRecorded = true;
                        }

                        throw;
                    }

                    sw.Stop();

                    response.Success = false;
                    response.StatusCode = MapExceptionToStatusCode(ex);
                    response.Error = ex.Message;
                    response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                    if (response.TokenizationProfile != null)
                    {
                        BatchLimitModeEnum appliedBatchLimitMode = response.TokenizationProfile.BatchLimitMode == BatchLimitModeEnum.Unknown
                            ? BatchLimitModeEnum.WholeRequest
                            : response.TokenizationProfile.BatchLimitMode;
                        AnnotateEmbeddingRequestCalls(
                            client,
                            startCallIndex,
                            new List<string> { explorerReq.Input ?? string.Empty },
                            TokenizerAdapterFactory.Create(response.TokenizationProfile),
                            response.TokenizationProfile,
                            appliedBatchLimitMode,
                            "Upstream embedding request failed.");
                    }
                    response.EmbeddingCalls = client.CallDetails.ToList();

                    if (inflight != null)
                    {
                        inflight.Stopwatch.Stop();
                        string requestJson = _Serializer.SerializeJson(explorerReq, false);
                        string responseJson = _Serializer.SerializeJson(response, false);
                        Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                        Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                        await RecordDetailedHistoryAsync(
                            inflight.Entry,
                            response.StatusCode,
                            inflight.Stopwatch.Elapsed.TotalMilliseconds,
                            requestJson,
                            responseJson,
                            reqHeaders,
                            respHeaders,
                            response.EmbeddingCalls,
                            null,
                            BuildTokenizationDetail(response.TokenizationProfile)).ConfigureAwait(false);
                        inflight.DetailRecorded = true;
                    }

                    return response;
                }
            }
            catch (ProviderConcurrencyLimitException)
            {
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                response.Success = false;
                response.StatusCode = MapExceptionToStatusCode(ex);
                response.Error = ex.Message;
                response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                if (explorerReq != null)
                {
                    response.EndpointId = explorerReq.EndpointId;
                    response.Input = explorerReq.Input;
                }
                return response;
            }
        }

        private static async Task<object> ExploreCompletionEndpoint(ApiRequest req)
        {
            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);
            CancellationToken token = GetRequestCancellationToken(req);

            EndpointExplorerCompletionResponse response = new EndpointExplorerCompletionResponse();
            EndpointExplorerCompletionRequest? explorerReq = null;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                token.ThrowIfCancellationRequested();
                AuthContext auth = (AuthContext)req.Metadata;
                explorerReq = req.GetData<EndpointExplorerCompletionRequest>();
                if (explorerReq == null) throw new ArgumentException("Request body is required.");
                if (string.IsNullOrWhiteSpace(explorerReq.EndpointId)) throw new ArgumentException("EndpointId is required.");
                if (string.IsNullOrWhiteSpace(explorerReq.Prompt)) throw new ArgumentException("Prompt is required.");

                CompletionEndpoint endpoint = await ResolveCompletionEndpointFromBody(explorerReq.EndpointId, auth, token).ConfigureAwait(false);
                response.EndpointId = endpoint.Id;
                response.Model = endpoint.Model;
                response.Prompt = explorerReq.Prompt;
                response.SystemPrompt = explorerReq.SystemPrompt;

                if (inflight != null)
                    response.RequestHistoryId = inflight.Entry.Id;

                req.Http.Response.Headers.Add(Constants.EndpointIdHeader, endpoint.Id);
                req.Http.Response.Headers.Add(Constants.ModelHeader, endpoint.Model);

                using CompletionClientBase client = CreateCompletionClient(endpoint);

                try
                {
                    int maxTokens = explorerReq.MaxTokens > 0 ? explorerReq.MaxTokens : 512;
                    int timeoutMs = explorerReq.TimeoutMs > 0 ? explorerReq.TimeoutMs : 60000;
                    string? output = await client.GenerateCompletionAsync(
                        explorerReq.Prompt,
                        endpoint.Model,
                        maxTokens,
                        timeoutMs,
                        token,
                        explorerReq.SystemPrompt).ConfigureAwait(false);

                    sw.Stop();

                    response.Success = true;
                    response.StatusCode = 200;
                    response.Output = output;
                    response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                    response.CompletionCalls = client.CallDetails.ToList();

                    if (inflight != null)
                    {
                        inflight.Stopwatch.Stop();
                        string requestJson = _Serializer.SerializeJson(explorerReq, false);
                        string responseJson = _Serializer.SerializeJson(response, false);
                        Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                        Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                        await RecordDetailedHistoryAsync(
                            inflight.Entry, 200, inflight.Stopwatch.Elapsed.TotalMilliseconds, requestJson, responseJson, reqHeaders, respHeaders, null, response.CompletionCalls).ConfigureAwait(false);
                        inflight.DetailRecorded = true;
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    if (ex is ProviderConcurrencyLimitException)
                    {
                        sw.Stop();

                        response.Success = false;
                        response.StatusCode = 429;
                        response.Error = ex.Message;
                        response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                        response.CompletionCalls = client.CallDetails.ToList();

                        if (inflight != null)
                        {
                            inflight.Stopwatch.Stop();
                            string requestJson = _Serializer.SerializeJson(explorerReq, false);
                            string responseJson = _Serializer.SerializeJson(response, false);
                            Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                            Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                            await RecordDetailedHistoryAsync(
                                inflight.Entry,
                                response.StatusCode,
                                inflight.Stopwatch.Elapsed.TotalMilliseconds,
                                requestJson,
                                responseJson,
                                reqHeaders,
                                respHeaders,
                                null,
                                response.CompletionCalls).ConfigureAwait(false);
                            inflight.DetailRecorded = true;
                        }

                        throw;
                    }

                    sw.Stop();

                    response.Success = false;
                    response.StatusCode = MapExceptionToStatusCode(ex);
                    response.Error = ex.Message;
                    response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                    response.CompletionCalls = client.CallDetails.ToList();

                    if (inflight != null)
                    {
                        inflight.Stopwatch.Stop();
                        string requestJson = _Serializer.SerializeJson(explorerReq, false);
                        string responseJson = _Serializer.SerializeJson(response, false);
                        Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                        Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                        await RecordDetailedHistoryAsync(
                            inflight.Entry, response.StatusCode, inflight.Stopwatch.Elapsed.TotalMilliseconds, requestJson, responseJson, reqHeaders, respHeaders, null, response.CompletionCalls).ConfigureAwait(false);
                        inflight.DetailRecorded = true;
                    }

                    return response;
                }
            }
            catch (ProviderConcurrencyLimitException)
            {
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                response.Success = false;
                response.StatusCode = MapExceptionToStatusCode(ex);
                response.Error = ex.Message;
                response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                if (explorerReq != null)
                {
                    response.EndpointId = explorerReq.EndpointId;
                    response.Prompt = explorerReq.Prompt;
                    response.SystemPrompt = explorerReq.SystemPrompt;
                }
                return response;
            }
        }

        /// <summary>
        /// Scaling factor to convert from model-native token counts to cl100k_base token counts.
        /// cl100k_base (100k vocab BPE) is more efficient than most embedding model tokenizers,
        /// Validate that the selected chunking strategy is compatible with the request atom type.
        /// </summary>
        private static void ValidateStrategyForAtomType(SemanticCellRequest request)
        {
            ChunkStrategyEnum strategy = request.ChunkingConfiguration.Strategy;
            AtomTypeEnum atomType = request.Type;

            // Generic strategies are universally applicable
            if (strategy == ChunkStrategyEnum.FixedTokenCount
                || strategy == ChunkStrategyEnum.SentenceBased
                || strategy == ChunkStrategyEnum.ParagraphBased
                || strategy == ChunkStrategyEnum.RegexBased)
                return;

            // List-only strategies
            if (strategy == ChunkStrategyEnum.WholeList || strategy == ChunkStrategyEnum.ListEntry)
            {
                if (atomType != AtomTypeEnum.List)
                    throw new ArgumentException(
                        "Strategy '" + strategy + "' is only compatible with atom type 'List', but got '" + atomType + "'.");
                return;
            }

            // Table-only strategies
            if (strategy == ChunkStrategyEnum.Row
                || strategy == ChunkStrategyEnum.RowWithHeaders
                || strategy == ChunkStrategyEnum.RowGroupWithHeaders
                || strategy == ChunkStrategyEnum.KeyValuePairs
                || strategy == ChunkStrategyEnum.WholeTable)
            {
                if (atomType != AtomTypeEnum.Table)
                    throw new ArgumentException(
                        "Strategy '" + strategy + "' is only compatible with atom type 'Table', but got '" + atomType + "'.");
                return;
            }
        }

        private static async Task<ProcessCellResult> ProcessCellAsync(SemanticCellRequest request, EmbeddingEndpoint endpoint, CancellationToken token = default)
        {
            ProcessCellResult cellResult = new ProcessCellResult();
            List<CompletionCallDetail> completionCalls = new List<CompletionCallDetail>();
            EmbeddingClientBase? client = null;

            try
            {
                token.ThrowIfCancellationRequested();
                List<SemanticCellRequest> rootCells = SummarizationEngine.Deflatten(new List<SemanticCellRequest> { request });

                if (request.SummarizationConfiguration != null)
                {
                    SummarizationConfiguration sumConfig = request.SummarizationConfiguration;

                    CompletionEndpoint? compEndpoint = await _Database.CompletionEndpoint.ReadByIdAsync(sumConfig.CompletionEndpointId, token).ConfigureAwait(false);
                    if (compEndpoint == null)
                        throw new KeyNotFoundException("Completion endpoint not found: " + sumConfig.CompletionEndpointId);
                    if (!compEndpoint.Active)
                        throw new ArgumentException("Completion endpoint '" + sumConfig.CompletionEndpointId + "' is inactive.");
                    if (_CompletionHealthCheckService != null && !_CompletionHealthCheckService.IsHealthy(compEndpoint.Id))
                        throw new EndpointUnhealthyException(compEndpoint.Id,
                            "Completion endpoint " + compEndpoint.Id + " (" + compEndpoint.Model + ") is currently unhealthy");

                    CompletionClientBase compClient = CreateCompletionClient(compEndpoint);
                    using (compClient)
                    {
                        SummarizationEngine summarizer = new SummarizationEngine(_Logging);
                        rootCells = await summarizer.SummarizeAsync(rootCells, sumConfig, compClient, compEndpoint.Model, token).ConfigureAwait(false);
                        completionCalls.AddRange(compClient.CallDetails);
                    }
                }

                string model = endpoint.Model;
                using (client = CreateEmbeddingClient(endpoint))
                {
                    ResolvedTokenizationProfile profile = await _TokenizationResolver.ResolveAsync(endpoint, model, client, token: token).ConfigureAwait(false);
                    ApplyRuntimeEmbeddingSafeguards(endpoint, profile);
                    cellResult.TokenizationProfile = profile;
                    ITokenizerAdapter tokenizer = TokenizerAdapterFactory.Create(profile);

                    List<SemanticCellResponse> rootResponses = new List<SemanticCellResponse>();
                    foreach (SemanticCellRequest rootCell in rootCells)
                    {
                        token.ThrowIfCancellationRequested();
                        SemanticCellResponse resp = await ProcessCellHierarchyAsync(rootCell, client, model, profile, tokenizer, cellResult.ChunkDiagnostics, token).ConfigureAwait(false);
                        rootResponses.Add(resp);
                    }
                    cellResult.Response = rootResponses.Count > 0 ? rootResponses[0] : new SemanticCellResponse();
                    cellResult.EmbeddingCalls = client.CallDetails.ToList();
                    cellResult.CompletionCalls = completionCalls;
                    return cellResult;
                }
            }
            catch (Exception ex)
            {
                cellResult.EmbeddingCalls = client?.CallDetails.ToList() ?? new List<EmbeddingCallDetail>();
                cellResult.CompletionCalls = completionCalls;
                throw new ProcessCellException(ex.Message, ex, cellResult);
            }
        }

        private static async Task<SemanticCellResponse> ProcessCellHierarchyAsync(
            SemanticCellRequest request,
            EmbeddingClientBase client,
            string model,
            ResolvedTokenizationProfile profile,
            ITokenizerAdapter tokenizer,
            List<ChunkProcessingDiagnostic> chunkDiagnostics,
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ValidateStrategyForAtomType(request);

            if (request.ChunkingConfiguration.Strategy == ChunkStrategyEnum.RegexBased)
            {
                if (string.IsNullOrWhiteSpace(request.ChunkingConfiguration.RegexPattern))
                    throw new ArgumentException("RegexPattern is required when using the RegexBased strategy.");
                try
                {
                    _ = new Regex(request.ChunkingConfiguration.RegexPattern, RegexOptions.None, TimeSpan.FromSeconds(5));
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException("RegexPattern is not a valid regular expression: " + ex.Message);
                }
            }

            int contextPrefixTokens = 0;
            if (!string.IsNullOrEmpty(request.ChunkingConfiguration.ContextPrefix))
            {
                contextPrefixTokens = tokenizer.CountTokens(request.ChunkingConfiguration.ContextPrefix);
                if (contextPrefixTokens >= profile.EffectiveInputBudget)
                {
                    throw new ArgumentException("ContextPrefix consumes the entire effective embedding input budget for model " + model + ".");
                }
            }

            int endpointBudget = Math.Max(1, profile.EffectiveInputBudget - contextPrefixTokens);
            int requestedBudget = request.ChunkingConfiguration.FixedTokenCount;
            int tokenBudget = Math.Min(requestedBudget, endpointBudget);

            if (requestedBudget > tokenBudget)
            {
                _Logging.Info("[ProcessCell] capping requested token budget from "
                    + requestedBudget
                    + " to " + tokenBudget
                    + " using profile source "
                    + profile.ProfileSource);
            }

            List<ChunkResult> chunks = _ChunkingEngine.Chunk(request, tokenizer, tokenBudget);
            chunks = NormalizeChunksForEmbeddingBudget(chunks, tokenizer, tokenBudget);

            // Set CellGUID on each chunk
            foreach (ChunkResult chunk in chunks)
            {
                chunk.CellGUID = request.GUID;
            }

            // Embed — apply context prefix inline
            string? contextPrefix = request.ChunkingConfiguration.ContextPrefix;
            List<string> textsToEmbed = chunks.Select(c =>
                string.IsNullOrEmpty(contextPrefix) ? c.Text : contextPrefix + c.Text
            ).ToList();

            if (textsToEmbed.Count > 0)
            {
                for (int i = 0; i < chunks.Count && i < textsToEmbed.Count; i++)
                {
                    string chunkText = chunks[i].Text ?? string.Empty;
                    string embeddingText = textsToEmbed[i] ?? string.Empty;
                    int chunkTokenCount = tokenizer.CountTokens(chunkText);
                    int embeddingTokenCount = tokenizer.CountTokens(embeddingText);

                    chunkDiagnostics.Add(new ChunkProcessingDiagnostic
                    {
                        CellGuid = request.GUID,
                        ChunkIndex = i,
                        ChunkCharacterCount = chunkText.Length,
                        ChunkTokenCount = chunkTokenCount,
                        EmbeddingCharacterCount = embeddingText.Length,
                        EmbeddingTokenCount = embeddingTokenCount,
                        ExceedsEffectiveInputBudget = embeddingTokenCount > profile.EffectiveInputBudget,
                        Preview = BuildPreview(embeddingText)
                    });
                }

                List<List<float>> embeddings = await EmbedTextsAsync(textsToEmbed, client, model, profile, tokenizer, token).ConfigureAwait(false);

                for (int i = 0; i < chunks.Count && i < embeddings.Count; i++)
                {
                    List<float> emb = embeddings[i];
                    if (request.EmbeddingConfiguration.L2Normalization)
                        emb = client.NormalizeL2(emb);
                    chunks[i].Embeddings = emb;
                }
            }

            // Populate Labels and Tags on each chunk
            List<string> labels = request.Labels ?? new List<string>();
            Dictionary<string, string> tags = request.Tags ?? new Dictionary<string, string>();

            foreach (ChunkResult chunk in chunks)
            {
                chunk.Labels = labels;
                chunk.Tags = tags;
            }

            SemanticCellResponse response = new SemanticCellResponse();
            response.GUID = request.GUID;
            response.ParentGUID = request.ParentGUID;
            response.Type = request.Type;
            response.Text = ResolveInputText(request);
            response.Chunks = chunks;

            // Recurse into children
            if (request.Children != null && request.Children.Count > 0)
            {
                response.Children = new List<SemanticCellResponse>();
                foreach (SemanticCellRequest child in request.Children)
                {
                    token.ThrowIfCancellationRequested();
                    SemanticCellResponse childResp = await ProcessCellHierarchyAsync(child, client, model, profile, tokenizer, chunkDiagnostics, token).ConfigureAwait(false);
                    response.Children.Add(childResp);
                }
            }

            return response;
        }

        private static async Task<List<List<float>>> EmbedTextsAsync(
            List<string> textsToEmbed,
            EmbeddingClientBase client,
            string model,
            ResolvedTokenizationProfile profile,
            ITokenizerAdapter tokenizer,
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (textsToEmbed == null || textsToEmbed.Count == 0) return new List<List<float>>();

            BatchLimitModeEnum appliedBatchLimitMode = profile.BatchLimitMode == BatchLimitModeEnum.Unknown
                ? BatchLimitModeEnum.WholeRequest
                : profile.BatchLimitMode;

            if (appliedBatchLimitMode == BatchLimitModeEnum.PerInput)
            {
                try
                {
                    return await EmbedBatchOnceAsync(textsToEmbed, client, model, profile, tokenizer, appliedBatchLimitMode, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (!IsContextLengthExceeded(ex))
                        throw;
                }
            }

            return await EmbedWithWholeRequestBudgetAsync(textsToEmbed, client, model, profile, tokenizer, appliedBatchLimitMode, token).ConfigureAwait(false);
        }

        private static async Task<List<List<float>>> EmbedWithWholeRequestBudgetAsync(
            List<string> textsToEmbed,
            EmbeddingClientBase client,
            string model,
            ResolvedTokenizationProfile profile,
            ITokenizerAdapter tokenizer,
            BatchLimitModeEnum appliedBatchLimitMode,
            CancellationToken token = default)
        {
            List<List<float>> allEmbeddings = new List<List<float>>();
            int index = 0;
            while (index < textsToEmbed.Count)
            {
                token.ThrowIfCancellationRequested();
                List<string> batch = new List<string>();
                int batchTokens = 0;

                while (index < textsToEmbed.Count)
                {
                    string candidate = textsToEmbed[index];
                    int candidateTokens = tokenizer.CountTokens(candidate);

                    if (batch.Count > 0 && batchTokens + candidateTokens > profile.EffectiveInputBudget)
                        break;

                    batch.Add(candidate);
                    batchTokens += candidateTokens;
                    index++;

                    if (batchTokens >= profile.EffectiveInputBudget)
                        break;
                }

                if (batch.Count == 0)
                {
                    batch.Add(textsToEmbed[index]);
                    index++;
                }

                List<List<float>> embeddings = await EmbedBatchWithContextFallbackAsync(
                    batch,
                    client,
                    model,
                    profile,
                    tokenizer,
                    appliedBatchLimitMode,
                    token).ConfigureAwait(false);
                allEmbeddings.AddRange(embeddings);
            }

            return allEmbeddings;
        }

        private static async Task<List<List<float>>> EmbedBatchWithContextFallbackAsync(
            List<string> inputs,
            EmbeddingClientBase client,
            string model,
            ResolvedTokenizationProfile profile,
            ITokenizerAdapter tokenizer,
            BatchLimitModeEnum appliedBatchLimitMode,
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                return await EmbedBatchOnceAsync(inputs, client, model, profile, tokenizer, appliedBatchLimitMode, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (!IsContextLengthExceeded(ex))
                    throw;

                if (inputs.Count > 1)
                {
                    (List<string> left, List<string> right) = SplitInputsForRetry(inputs, tokenizer);
                    List<List<float>> leftEmbeddings = await EmbedBatchWithContextFallbackAsync(
                        left,
                        client,
                        model,
                        profile,
                        tokenizer,
                        appliedBatchLimitMode,
                        token).ConfigureAwait(false);
                    List<List<float>> rightEmbeddings = await EmbedBatchWithContextFallbackAsync(
                        right,
                        client,
                        model,
                        profile,
                        tokenizer,
                        appliedBatchLimitMode,
                        token).ConfigureAwait(false);
                    leftEmbeddings.AddRange(rightEmbeddings);
                    return leftEmbeddings;
                }

                return new List<List<float>>
                {
                    await EmbedSingleInputWithContextFallbackAsync(
                        inputs[0],
                        client,
                        model,
                        profile,
                        tokenizer,
                        appliedBatchLimitMode,
                        token).ConfigureAwait(false)
                };
            }
        }

        private static async Task<List<float>> EmbedSingleInputWithContextFallbackAsync(
            string input,
            EmbeddingClientBase client,
            string model,
            ResolvedTokenizationProfile profile,
            ITokenizerAdapter tokenizer,
            BatchLimitModeEnum appliedBatchLimitMode,
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            int tokenCount = tokenizer.CountTokens(input);
            if (tokenCount <= 1)
                throw new InvalidOperationException("A single-token embedding input was rejected for exceeding context length.");

            int splitBudget = Math.Min(profile.EffectiveInputBudget - 1, Math.Max(1, tokenCount / 2));
            if (splitBudget >= tokenCount)
                splitBudget = tokenCount - 1;
            if (splitBudget < 1)
                throw new InvalidOperationException("Unable to shrink a rejected embedding input below the current token budget.");

            ChunkingConfiguration splitConfig = new ChunkingConfiguration
            {
                Strategy = ChunkStrategyEnum.FixedTokenCount,
                FixedTokenCount = splitBudget,
                OverlapCount = 0
            };

            List<string> splitInputs = FixedTokenChunker.Chunk(input, splitConfig, tokenizer, splitBudget);
            if (splitInputs.Count <= 1)
                throw new InvalidOperationException("Failed to split a rejected embedding input into smaller token-safe spans.");

            List<List<float>> splitEmbeddings = new List<List<float>>();
            foreach (string splitInput in splitInputs)
            {
                token.ThrowIfCancellationRequested();
                List<List<float>> result = await EmbedBatchWithContextFallbackAsync(
                    new List<string> { splitInput },
                    client,
                    model,
                    profile,
                    tokenizer,
                    appliedBatchLimitMode,
                    token).ConfigureAwait(false);
                splitEmbeddings.Add(result[0]);
            }

            return AverageEmbeddings(splitEmbeddings);
        }

        private static async Task<List<List<float>>> EmbedBatchOnceAsync(
            List<string> inputs,
            EmbeddingClientBase client,
            string model,
            ResolvedTokenizationProfile profile,
            ITokenizerAdapter tokenizer,
            BatchLimitModeEnum appliedBatchLimitMode,
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            int startCallIndex = client.CallDetails.Count;
            try
            {
                List<List<float>> embeddings = await client.EmbedBatchAsync(inputs, model, token).ConfigureAwait(false);
                AnnotateEmbeddingRequestCalls(client, startCallIndex, inputs, tokenizer, profile, appliedBatchLimitMode, null);
                return embeddings;
            }
            catch
            {
                AnnotateEmbeddingRequestCalls(client, startCallIndex, inputs, tokenizer, profile, appliedBatchLimitMode, "Upstream embedding request failed.");
                throw;
            }
        }

        private static (List<string> Left, List<string> Right) SplitInputsForRetry(
            List<string> inputs,
            ITokenizerAdapter tokenizer)
        {
            if (inputs == null || inputs.Count < 2)
                throw new ArgumentException("At least two inputs are required to split a batch.");

            int totalTokens = inputs.Sum(tokenizer.CountTokens);
            int targetLeftTokens = Math.Max(1, totalTokens / 2);
            int runningTokens = 0;
            int splitIndex = 0;

            while (splitIndex < inputs.Count - 1)
            {
                runningTokens += tokenizer.CountTokens(inputs[splitIndex]);
                splitIndex++;
                if (runningTokens >= targetLeftTokens)
                    break;
            }

            if (splitIndex <= 0 || splitIndex >= inputs.Count)
                splitIndex = Math.Max(1, inputs.Count / 2);

            return (
                inputs.Take(splitIndex).ToList(),
                inputs.Skip(splitIndex).ToList());
        }

        private static List<float> AverageEmbeddings(List<List<float>> embeddings)
        {
            if (embeddings == null || embeddings.Count == 0)
                return new List<float>();

            List<List<float>> nonNullEmbeddings = embeddings
                .Where(embedding => embedding != null)
                .ToList();
            if (nonNullEmbeddings.Count == 0)
                return new List<float>();

            int dimension = nonNullEmbeddings[0].Count;
            if (dimension == 0)
                return new List<float>();

            if (nonNullEmbeddings.Any(embedding => embedding.Count != dimension))
                throw new InvalidOperationException("Embedding fallback produced vectors with mismatched dimensions.");

            float[] sums = new float[dimension];
            foreach (List<float> embedding in nonNullEmbeddings)
            {
                for (int i = 0; i < dimension; i++)
                    sums[i] += embedding[i];
            }

            float divisor = nonNullEmbeddings.Count;
            return sums.Select(value => value / divisor).ToList();
        }

        private static bool IsContextLengthExceeded(Exception ex)
        {
            Exception? current = ex;
            while (current != null)
            {
                string message = current.Message ?? string.Empty;
                if (message.IndexOf("context length", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("input length exceeds", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private static void ApplyRuntimeEmbeddingSafeguards(EmbeddingEndpoint endpoint, ResolvedTokenizationProfile profile)
        {
            if (endpoint.ApiFormat != ApiFormatEnum.Ollama)
                return;
            if (profile.TokenizerKind != TokenizerKindEnum.BertWordPiece)
                return;

            bool explicitPerInputOverride = endpoint.Tokenization?.AutoDetect == false
                && endpoint.Tokenization?.BatchLimitMode == BatchLimitModeEnum.PerInput;
            if (explicitPerInputOverride || profile.BatchLimitMode == BatchLimitModeEnum.WholeRequest)
                return;

            profile.BatchLimitMode = BatchLimitModeEnum.WholeRequest;
            profile.ProviderMetadata["RuntimeBatchLimitModeOverride"] = BatchLimitModeEnum.WholeRequest.ToString();
            profile.ProviderMetadata["RuntimeBatchLimitModeOverrideReason"] = "ConservativeOllamaBertSafety";
        }

        private static List<ChunkResult> NormalizeChunksForEmbeddingBudget(
            List<ChunkResult> chunks,
            ITokenizerAdapter tokenizer,
            int tokenBudget)
        {
            if (chunks == null || chunks.Count == 0)
                return chunks ?? new List<ChunkResult>();

            ChunkingConfiguration splitConfig = new ChunkingConfiguration
            {
                Strategy = ChunkStrategyEnum.FixedTokenCount,
                FixedTokenCount = tokenBudget,
                OverlapCount = 0
            };

            List<ChunkResult> normalized = new List<ChunkResult>();
            foreach (ChunkResult chunk in chunks)
            {
                string chunkText = chunk.Text ?? string.Empty;
                if (tokenizer.CountTokens(chunkText) <= tokenBudget)
                {
                    normalized.Add(chunk);
                    continue;
                }

                List<string> splitTexts = FixedTokenChunker.Chunk(chunkText, splitConfig, tokenizer, tokenBudget);
                if (splitTexts.Count <= 1)
                {
                    normalized.Add(chunk);
                    continue;
                }

                normalized.AddRange(splitTexts.Select(splitText => new ChunkResult
                {
                    CellGUID = chunk.CellGUID,
                    Text = splitText
                }));
            }

            return normalized;
        }

        private static string ResolveInputText(SemanticCellRequest request)
        {
            switch (request.Type)
            {
                case AtomTypeEnum.Text:
                case AtomTypeEnum.Code:
                case AtomTypeEnum.Hyperlink:
                case AtomTypeEnum.Meta:
                    return request.Text ?? string.Empty;

                case AtomTypeEnum.List:
                    List<string>? items = request.OrderedList ?? request.UnorderedList;
                    if (items == null || items.Count == 0) return string.Empty;
                    bool ordered = request.OrderedList != null;
                    List<string> lines = new List<string>();
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (ordered)
                            lines.Add($"{i + 1}. {items[i]}");
                        else
                            lines.Add($"- {items[i]}");
                    }
                    return string.Join("\n", lines);

                case AtomTypeEnum.Table:
                    if (request.Table == null || request.Table.Count == 0) return string.Empty;
                    List<string> rows = new List<string>();
                    foreach (List<string> row in request.Table)
                    {
                        rows.Add(string.Join(" | ", row));
                    }
                    return string.Join("\n", rows);

                case AtomTypeEnum.Binary:
                case AtomTypeEnum.Image:
                case AtomTypeEnum.Unknown:
                default:
                    return request.Text ?? string.Empty;
            }
        }

        private static int MapExceptionToStatusCode(Exception ex)
        {
            if (ex is KeyNotFoundException) return 404;
            if (ex is ArgumentException || ex is ArgumentNullException) return 400;
            if (ex is UnauthorizedAccessException) return 401;
            if (ex is ProviderConcurrencyLimitException) return 429;
            if (ex is EndpointUnhealthyException) return 502;
            if (ex is ProviderOperationTimeoutException) return 504;
            return 500;
        }

        private static async Task SendApiErrorAsync(HttpContextBase ctx, int statusCode, string error, string message)
        {
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = Constants.JsonContentType;

            ApiErrorResponse response = new ApiErrorResponse
            {
                Error = error,
                Message = message,
                StatusCode = statusCode,
                TimestampUtc = DateTime.UtcNow
            };

            await ctx.Response.Send(_Serializer.SerializeJson(response, false)).ConfigureAwait(false);
        }

        private static Dictionary<string, string> ExtractHeaders(System.Collections.Specialized.NameValueCollection? headers)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers != null)
            {
                foreach (string? key in headers.AllKeys)
                {
                    if (!string.IsNullOrEmpty(key))
                        dict[key] = headers[key] ?? "";
                }
            }
            return dict;
        }

        private static void ApplyTokenizationProfileHeaders(System.Collections.Specialized.NameValueCollection headers, ResolvedTokenizationProfile? profile)
        {
            if (headers == null || profile == null) return;

            headers[Constants.TokenizerKindHeader] = profile.TokenizerKind.ToString();
            headers[Constants.TokenizerModelHeader] = profile.TokenizerModel;
            headers[Constants.TokenizerSourceHeader] = profile.ProfileSource.ToString();
            headers[Constants.EffectiveInputBudgetHeader] = profile.EffectiveInputBudget.ToString();
            headers[Constants.MaxInputTokensHeader] = profile.MaxInputTokens.ToString();
            headers[Constants.BatchLimitModeHeader] = profile.BatchLimitMode.ToString();
        }

        private static Dictionary<string, object?>? BuildTokenizationDetail(
            ResolvedTokenizationProfile? profile,
            List<ChunkProcessingDiagnostic>? chunkDiagnostics = null)
        {
            if (profile == null && (chunkDiagnostics == null || chunkDiagnostics.Count < 1)) return null;

            Dictionary<string, object?> detail = new Dictionary<string, object?>();
            if (profile != null)
            {
                detail["TokenizationProfile"] = profile;
            }
            if (chunkDiagnostics != null && chunkDiagnostics.Count > 0)
            {
                detail["ChunkDiagnostics"] = chunkDiagnostics;
            }

            return detail;
        }

        private static void AnnotateEmbeddingRequestCalls(
            EmbeddingClientBase client,
            int startCallIndex,
            List<string> inputs,
            ITokenizerAdapter tokenizer,
            ResolvedTokenizationProfile profile,
            BatchLimitModeEnum appliedBatchLimitMode,
            string? failureHint)
        {
            IReadOnlyList<EmbeddingCallDetail> callDetails = client.CallDetails;
            if (callDetails.Count <= startCallIndex) return;

            List<EmbeddingCallInputDetail> inputDetails = inputs.Select((input, index) =>
            {
                int tokenCount = tokenizer.CountTokens(input);
                return new EmbeddingCallInputDetail
                {
                    Index = index,
                    CharacterCount = input.Length,
                    TokenCount = tokenCount,
                    ExceedsEffectiveInputBudget = tokenCount > profile.EffectiveInputBudget,
                    Preview = BuildPreview(input)
                };
            }).ToList();

            List<int> failedInputIndices = inputDetails
                .Where(detail => detail.ExceedsEffectiveInputBudget)
                .Select(detail => detail.Index)
                .ToList();

            string? resolvedFailureHint = failureHint;
            if (failedInputIndices.Count > 0)
            {
                resolvedFailureHint = "Inputs " + string.Join(", ", failedInputIndices)
                    + " exceeded the effective per-input budget of "
                    + profile.EffectiveInputBudget
                    + " tokens.";
            }
            else if (appliedBatchLimitMode == BatchLimitModeEnum.WholeRequest
                && inputDetails.Sum(detail => detail.TokenCount) > profile.EffectiveInputBudget)
            {
                resolvedFailureHint = "The batched request exceeded the whole-request token budget of "
                    + profile.EffectiveInputBudget
                    + " tokens.";
            }

            for (int i = startCallIndex; i < callDetails.Count; i++)
            {
                EmbeddingCallDetail detail = callDetails[i];
                if (string.IsNullOrEmpty(detail.Purpose))
                    detail.Purpose = "EmbeddingRequest";
                detail.Inputs = inputDetails;
                detail.BatchTokenCount = inputDetails.Sum(d => d.TokenCount);
                detail.EffectiveInputBudget = profile.EffectiveInputBudget;
                detail.MaxInputTokens = profile.MaxInputTokens;
                detail.BatchLimitMode = appliedBatchLimitMode;
                detail.FailedInputIndices = failedInputIndices.Count > 0 ? failedInputIndices : null;
                detail.FailureReasonHint = resolvedFailureHint;
            }
        }

        private static string BuildPreview(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            const int maxLength = 160;
            string flattened = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return flattened.Length <= maxLength ? flattened : flattened.Substring(0, maxLength);
        }

        private static EmbeddingClientBase CreateEmbeddingClient(EmbeddingEndpoint endpoint)
        {
            switch (endpoint.ApiFormat)
            {
                case ApiFormatEnum.Ollama:
                    return new OllamaEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, _Logging, endpoint.MaximumTimeoutMs, endpoint.Id, endpoint.MaxConcurrentRequests);
                case ApiFormatEnum.OpenAI:
                case ApiFormatEnum.vLLM:
                    return new OpenAiEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, _Logging, endpoint.MaximumTimeoutMs, endpoint.Id, endpoint.MaxConcurrentRequests);
                case ApiFormatEnum.Gemini:
                    return new GeminiEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, _Logging, endpoint.MaximumTimeoutMs, endpoint.Id, endpoint.MaxConcurrentRequests);
                default:
                    throw new ArgumentException("Unsupported API format: " + endpoint.ApiFormat);
            }
        }

        private static CompletionClientBase CreateCompletionClient(CompletionEndpoint endpoint)
        {
            switch (endpoint.ApiFormat)
            {
                case ApiFormatEnum.Ollama:
                    return new OllamaCompletionClient(endpoint.Endpoint, endpoint.ApiKey, _Logging, endpoint.MaximumTimeoutMs, endpoint.Id, endpoint.MaxConcurrentRequests);
                case ApiFormatEnum.OpenAI:
                case ApiFormatEnum.vLLM:
                    return new OpenAiCompletionClient(endpoint.Endpoint, endpoint.ApiKey, _Logging, endpoint.MaximumTimeoutMs, endpoint.Id, endpoint.MaxConcurrentRequests);
                case ApiFormatEnum.Gemini:
                    return new GeminiCompletionClient(endpoint.Endpoint, endpoint.ApiKey, _Logging, endpoint.MaximumTimeoutMs, endpoint.Id, endpoint.MaxConcurrentRequests);
                default:
                    throw new ArgumentException("Unsupported API format: " + endpoint.ApiFormat);
            }
        }

        #endregion

        #region Tenants

        private static async Task<object> CreateTenant(ApiRequest req)
        {
            RequireAdmin(req);
            TenantMetadata? tenant = req.GetData<TenantMetadata>();
            if (tenant == null) throw new ArgumentException("Request body is required.");

            TenantMetadata created = await _Database.Tenant.CreateAsync(tenant).ConfigureAwait(false);

            // Create default user, credential, and endpoints for new tenant
            UserMaster user = new UserMaster();
            user.TenantId = created.Id;
            user.Email = "admin@" + created.Name.ToLowerInvariant().Replace(" ", "");
            user.SetPassword("password");
            user.IsAdmin = true;
            UserMaster createdUser = await _Database.User.CreateAsync(user).ConfigureAwait(false);

            Credential cred = new Credential();
            cred.TenantId = created.Id;
            cred.UserId = createdUser.Id;
            cred.Name = "Default API Key";
            await _Database.Credential.CreateAsync(cred).ConfigureAwait(false);

            foreach (DefaultEmbeddingEndpoint defaultEp in _Settings.DefaultEmbeddingEndpoints)
            {
                EmbeddingEndpoint ep = new EmbeddingEndpoint();
                ep.TenantId = created.Id;
                ep.Name = defaultEp.Name;
                ep.Model = defaultEp.Model;
                ep.Endpoint = defaultEp.Endpoint;
                ep.ApiFormat = defaultEp.ApiFormat;
                ep.ApiKey = defaultEp.ApiKey;
                ep.MaximumTimeoutMs = defaultEp.MaximumTimeoutMs;
                ep.MaxConcurrentRequests = defaultEp.MaxConcurrentRequests;
                ep.Tokenization = defaultEp.Tokenization;
                ep.Labels = defaultEp.Labels;
                ep.Tags = defaultEp.Tags;
                ep.HealthCheckEnabled = true;
                EmbeddingEndpoint.ApplyHealthCheckDefaults(ep);
                EmbeddingEndpoint createdEp = await _Database.EmbeddingEndpoint.CreateAsync(ep).ConfigureAwait(false);
                _HealthCheckService?.OnEndpointCreated(createdEp);
            }

            foreach (DefaultInferenceEndpoint defaultIep in _Settings.DefaultInferenceEndpoints)
            {
                CompletionEndpoint cep = new CompletionEndpoint();
                cep.TenantId = created.Id;
                cep.Name = defaultIep.Name;
                cep.Model = defaultIep.Model;
                cep.Endpoint = defaultIep.Endpoint;
                cep.ApiFormat = defaultIep.ApiFormat;
                cep.ApiKey = defaultIep.ApiKey;
                cep.MaximumTimeoutMs = defaultIep.MaximumTimeoutMs;
                cep.MaxConcurrentRequests = defaultIep.MaxConcurrentRequests;
                cep.Labels = defaultIep.Labels;
                cep.Tags = defaultIep.Tags;
                cep.HealthCheckEnabled = true;
                CompletionEndpoint.ApplyHealthCheckDefaults(cep);
                CompletionEndpoint createdCep = await _Database.CompletionEndpoint.CreateAsync(cep).ConfigureAwait(false);
                _CompletionHealthCheckService?.OnEndpointCreated(createdCep);
            }

            req.Http.Response.StatusCode = 201;
            return created;
        }

        private static async Task<object> ReadTenant(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            TenantMetadata? tenant = await _Database.Tenant.ReadByIdAsync(id).ConfigureAwait(false);
            if (tenant == null) throw new KeyNotFoundException("Tenant not found: " + id);
            return tenant;
        }

        private static async Task<object> UpdateTenant(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            TenantMetadata? tenant = req.GetData<TenantMetadata>();
            if (tenant == null) throw new ArgumentException("Request body is required.");
            tenant.Id = id;
            TenantMetadata updated = await _Database.Tenant.UpdateAsync(tenant).ConfigureAwait(false);
            return updated;
        }

        private static async Task<object> DeleteTenant(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.Tenant.DeleteByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        private static async Task<object> HeadTenant(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            bool exists = await _Database.Tenant.ExistsByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null!;
        }

        private static async Task<object> EnumerateTenants(ApiRequest req)
        {
            RequireAdmin(req);
            EnumerationRequest? enumReq = req.GetData<EnumerationRequest>();
            if (enumReq == null) enumReq = new EnumerationRequest();
            EnumerationResult<TenantMetadata> result = await _Database.Tenant.EnumerateAsync(enumReq).ConfigureAwait(false);
            return result;
        }

        #endregion

        #region Users

        private static async Task<object> CreateUser(ApiRequest req)
        {
            RequireAdmin(req);
            UserMaster? user = req.GetData<UserMaster>();
            if (user == null) throw new ArgumentException("Request body is required.");
            UserMaster created = await _Database.User.CreateAsync(user).ConfigureAwait(false);
            return UserMaster.Redact(created);
        }

        private static async Task<object> ReadUser(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            UserMaster? user = await _Database.User.ReadByIdAsync(id).ConfigureAwait(false);
            if (user == null) throw new KeyNotFoundException("User not found: " + id);
            return UserMaster.Redact(user);
        }

        private static async Task<object> UpdateUser(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            UserMaster? user = req.GetData<UserMaster>();
            if (user == null) throw new ArgumentException("Request body is required.");
            user.Id = id;
            UserMaster updated = await _Database.User.UpdateAsync(user).ConfigureAwait(false);
            return UserMaster.Redact(updated);
        }

        private static async Task<object> DeleteUser(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.User.DeleteByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        private static async Task<object> HeadUser(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            bool exists = await _Database.User.ExistsByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null!;
        }

        private static async Task<object> EnumerateUsers(ApiRequest req)
        {
            RequireAdmin(req);
            AuthContext auth = (AuthContext)req.Metadata;
            EnumerationRequest? enumReq = req.GetData<EnumerationRequest>();
            if (enumReq == null) enumReq = new EnumerationRequest();
            string tenantId = auth.TenantId ?? "default";
            EnumerationResult<UserMaster> result = await _Database.User.EnumerateAsync(tenantId, enumReq).ConfigureAwait(false);

            // Redact passwords
            List<UserMaster> redacted = result.Data.Select(u => UserMaster.Redact(u)).ToList();
            result.Data = redacted;
            return result;
        }

        #endregion

        #region Credentials

        private static async Task<object> CreateCredential(ApiRequest req)
        {
            RequireAdmin(req);
            Credential? cred = req.GetData<Credential>();
            if (cred == null) throw new ArgumentException("Request body is required.");
            Credential created = await _Database.Credential.CreateAsync(cred).ConfigureAwait(false);
            req.Http.Response.StatusCode = 201;
            return created;
        }

        private static async Task<object> ReadCredential(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            Credential? cred = await _Database.Credential.ReadByIdAsync(id).ConfigureAwait(false);
            if (cred == null) throw new KeyNotFoundException("Credential not found: " + id);
            return cred;
        }

        private static async Task<object> UpdateCredential(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            Credential? cred = req.GetData<Credential>();
            if (cred == null) throw new ArgumentException("Request body is required.");
            cred.Id = id;
            Credential updated = await _Database.Credential.UpdateAsync(cred).ConfigureAwait(false);
            return updated;
        }

        private static async Task<object> DeleteCredential(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.Credential.DeleteByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        private static async Task<object> HeadCredential(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            bool exists = await _Database.Credential.ExistsByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null!;
        }

        private static async Task<object> EnumerateCredentials(ApiRequest req)
        {
            RequireAdmin(req);
            AuthContext auth = (AuthContext)req.Metadata;
            EnumerationRequest? enumReq = req.GetData<EnumerationRequest>();
            if (enumReq == null) enumReq = new EnumerationRequest();
            string tenantId = auth.TenantId ?? "default";
            EnumerationResult<Credential> result = await _Database.Credential.EnumerateAsync(tenantId, enumReq).ConfigureAwait(false);
            return result;
        }

        #endregion

        #region Endpoints

        private static async Task<object> CreateEndpoint(ApiRequest req)
        {
            RequireAdmin(req);
            EmbeddingEndpoint? ep = req.GetData<EmbeddingEndpoint>();
            if (ep == null) throw new ArgumentException("Request body is required.");
            EmbeddingEndpoint.ApplyHealthCheckDefaults(ep);
            EmbeddingEndpoint created = await _Database.EmbeddingEndpoint.CreateAsync(ep).ConfigureAwait(false);
            _TokenizationResolver?.Invalidate(created.Id);
            _HealthCheckService?.OnEndpointCreated(created);
            req.Http.Response.StatusCode = 201;
            return created;
        }

        private static async Task<object> ReadEndpoint(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            EmbeddingEndpoint? ep = await _Database.EmbeddingEndpoint.ReadByIdAsync(id).ConfigureAwait(false);
            if (ep == null) throw new KeyNotFoundException("Embedding endpoint not found: " + id);
            return ep;
        }

        private static async Task<object> UpdateEndpoint(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            EmbeddingEndpoint? ep = req.GetData<EmbeddingEndpoint>();
            if (ep == null) throw new ArgumentException("Request body is required.");
            ep.Id = id;
            EmbeddingEndpoint.ApplyHealthCheckDefaults(ep);
            EmbeddingEndpoint updated = await _Database.EmbeddingEndpoint.UpdateAsync(ep).ConfigureAwait(false);
            _TokenizationResolver?.Invalidate(updated.Id);
            _HealthCheckService?.OnEndpointUpdated(updated);
            return updated;
        }

        private static async Task<object> DeleteEndpoint(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.EmbeddingEndpoint.DeleteByIdAsync(id).ConfigureAwait(false);
            _TokenizationResolver?.Invalidate(id);
            _HealthCheckService?.OnEndpointDeleted(id);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        private static async Task<object> HeadEndpoint(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            bool exists = await _Database.EmbeddingEndpoint.ExistsByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null!;
        }

        private static async Task<object> EnumerateEndpoints(ApiRequest req)
        {
            RequireAdmin(req);
            AuthContext auth = (AuthContext)req.Metadata;
            EnumerationRequest? enumReq = req.GetData<EnumerationRequest>();
            if (enumReq == null) enumReq = new EnumerationRequest();
            string tenantId = auth.TenantId ?? "default";
            EnumerationResult<EmbeddingEndpoint> result = await _Database.EmbeddingEndpoint.EnumerateAsync(tenantId, enumReq).ConfigureAwait(false);
            return result;
        }

        #endregion

        #region Endpoint Health

        private static async Task<object> GetAllEndpointHealth(ApiRequest req)
        {
            RequireAdmin(req);
            AuthContext auth = (AuthContext)req.Metadata;

            if (_HealthCheckService == null)
                return new List<EndpointHealthStatus>();

            string? tenantFilter = auth.IsGlobalAdmin ? null : auth.TenantId;
            List<EndpointHealthState> states = _HealthCheckService.GetAllHealthStates(tenantFilter);

            List<EndpointHealthStatus> statuses = new List<EndpointHealthStatus>();
            foreach (EndpointHealthState state in states)
            {
                statuses.Add(EndpointHealthStatus.FromState(state));
            }
            return statuses;
        }

        private static async Task<object> GetEndpointHealth(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];

            if (_HealthCheckService == null)
                throw new KeyNotFoundException("Health check service not available");

            EndpointHealthState? state = _HealthCheckService.GetHealthState(id);
            if (state == null)
                throw new KeyNotFoundException("No health state for endpoint " + id + " (health check may not be enabled)");

            return EndpointHealthStatus.FromState(state);
        }

        #endregion

        #region Completion Endpoints

        private static async Task<object> CreateCompletionEndpoint(ApiRequest req)
        {
            RequireAdmin(req);
            CompletionEndpoint? ep = req.GetData<CompletionEndpoint>();
            if (ep == null) throw new ArgumentException("Request body is required.");
            CompletionEndpoint.ApplyHealthCheckDefaults(ep);
            CompletionEndpoint created = await _Database.CompletionEndpoint.CreateAsync(ep).ConfigureAwait(false);
            _CompletionHealthCheckService?.OnEndpointCreated(created);
            req.Http.Response.StatusCode = 201;
            return created;
        }

        private static async Task<object> ReadCompletionEndpoint(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            CompletionEndpoint? ep = await _Database.CompletionEndpoint.ReadByIdAsync(id).ConfigureAwait(false);
            if (ep == null) throw new KeyNotFoundException("Completion endpoint not found: " + id);
            return ep;
        }

        private static async Task<object> UpdateCompletionEndpoint(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            CompletionEndpoint? ep = req.GetData<CompletionEndpoint>();
            if (ep == null) throw new ArgumentException("Request body is required.");
            ep.Id = id;
            CompletionEndpoint.ApplyHealthCheckDefaults(ep);
            CompletionEndpoint updated = await _Database.CompletionEndpoint.UpdateAsync(ep).ConfigureAwait(false);
            _CompletionHealthCheckService?.OnEndpointUpdated(updated);
            return updated;
        }

        private static async Task<object> DeleteCompletionEndpoint(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.CompletionEndpoint.DeleteByIdAsync(id).ConfigureAwait(false);
            _CompletionHealthCheckService?.OnEndpointDeleted(id);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        private static async Task<object> HeadCompletionEndpoint(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            bool exists = await _Database.CompletionEndpoint.ExistsByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null!;
        }

        private static async Task<object> EnumerateCompletionEndpoints(ApiRequest req)
        {
            RequireAdmin(req);
            AuthContext auth = (AuthContext)req.Metadata;
            EnumerationRequest? enumReq = req.GetData<EnumerationRequest>();
            if (enumReq == null) enumReq = new EnumerationRequest();
            string tenantId = auth.TenantId ?? "default";
            EnumerationResult<CompletionEndpoint> result = await _Database.CompletionEndpoint.EnumerateAsync(tenantId, enumReq).ConfigureAwait(false);
            return result;
        }

        #endregion

        #region Completion Endpoint Health

        private static async Task<object> GetAllCompletionEndpointHealth(ApiRequest req)
        {
            RequireAdmin(req);
            AuthContext auth = (AuthContext)req.Metadata;

            if (_CompletionHealthCheckService == null)
                return new List<EndpointHealthStatus>();

            string? tenantFilter = auth.IsGlobalAdmin ? null : auth.TenantId;
            List<EndpointHealthState> states = _CompletionHealthCheckService.GetAllHealthStates(tenantFilter);

            List<EndpointHealthStatus> statuses = new List<EndpointHealthStatus>();
            foreach (EndpointHealthState state in states)
            {
                statuses.Add(EndpointHealthStatus.FromState(state));
            }
            return statuses;
        }

        private static async Task<object> GetCompletionEndpointHealth(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];

            if (_CompletionHealthCheckService == null)
                throw new KeyNotFoundException("Health check service not available");

            EndpointHealthState? state = _CompletionHealthCheckService.GetHealthState(id);
            if (state == null)
                throw new KeyNotFoundException("No health state for completion endpoint " + id + " (health check may not be enabled)");

            return EndpointHealthStatus.FromState(state);
        }

        #endregion

        #region Request History

        private static async Task<object> ReadRequestHistory(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            RequestHistoryEntry? entry = await _Database.RequestHistory.ReadByIdAsync(id).ConfigureAwait(false);
            if (entry == null) throw new KeyNotFoundException("Request history entry not found: " + id);
            return entry;
        }

        private static async Task<object> ReadRequestHistoryDetail(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            RequestHistoryEntry? entry = await _Database.RequestHistory.ReadByIdAsync(id).ConfigureAwait(false);
            if (entry == null) throw new KeyNotFoundException("Request history entry not found: " + id);

            if (_RequestHistoryService == null || string.IsNullOrEmpty(entry.ObjectKey))
            {
                return new Dictionary<string, string> { { "Message", "No detail available" } };
            }

            string? detail = await _RequestHistoryService.ReadDetailAsync(entry.ObjectKey).ConfigureAwait(false);
            if (detail == null) throw new KeyNotFoundException("Request history detail file not found.");

            req.Http.Response.ContentType = Constants.JsonContentType;
            return _JsonSerializer.DeserializeJson<object>(detail);
        }

        private static async Task<object> EnumerateRequestHistory(ApiRequest req)
        {
            RequireAdmin(req);
            AuthContext auth = (AuthContext)req.Metadata;
            EnumerationRequest? enumReq = req.GetData<EnumerationRequest>();
            if (enumReq == null) enumReq = new EnumerationRequest();

            EnumerationResult<RequestHistoryEntry> result;
            if (auth.IsGlobalAdmin)
            {
                result = await _Database.RequestHistory.EnumerateAllAsync(enumReq).ConfigureAwait(false);
            }
            else
            {
                string tenantId = auth.TenantId ?? "default";
                result = await _Database.RequestHistory.EnumerateAsync(tenantId, enumReq).ConfigureAwait(false);
            }

            return result;
        }

        private static async Task<object> DeleteRequestHistory(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.RequestHistory.DeleteByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        private static async Task<object> GetRequestStatistics(ApiRequest req)
        {
            RequireAdmin(req);
            AuthContext auth = (AuthContext)req.Metadata;
            RequestStatisticsRequest? statsReq = req.GetData<RequestStatisticsRequest>();
            if (statsReq == null) statsReq = new RequestStatisticsRequest();

            RequestStatisticsResponse result;
            if (auth.IsGlobalAdmin)
            {
                result = await _Database.RequestHistory.GetStatisticsAllAsync(statsReq).ConfigureAwait(false);
            }
            else
            {
                string tenantId = auth.TenantId ?? "default";
                result = await _Database.RequestHistory.GetStatisticsAsync(tenantId, statsReq).ConfigureAwait(false);
            }

            return result;
        }

        #endregion

        #region Helpers

        private static CancellationToken GetRequestCancellationToken(ApiRequest req)
        {
            if (req != null && _RequestTokens.TryGetValue(req.Http.Guid.ToString(), out CancellationToken token))
            {
                return token;
            }

            return CancellationToken.None;
        }

        private static void RequireAdmin(ApiRequest req)
        {
            AuthContext auth = (AuthContext)req.Metadata;
            if (!auth.IsGlobalAdmin)
            {
                throw new UnauthorizedAccessException("Admin access required.");
            }
        }

        #endregion

        private class InFlightRequest
        {
            public RequestHistoryEntry Entry { get; set; } = null!;
            public Stopwatch Stopwatch { get; set; } = null!;
            public bool DetailRecorded { get; set; } = false;
        }
    }
}
