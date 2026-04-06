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
        private static EmbeddingHealthCheckService? _HealthCheckService;
        private static CompletionHealthCheckService? _CompletionHealthCheckService;
        private static ChunkingEngine _ChunkingEngine = null!;
        private static PartioSerializer _Serializer = new PartioSerializer();
        private static SerializationHelper.Serializer _JsonSerializer = new SerializationHelper.Serializer();
        private static DateTime _StartTimeUtc = DateTime.UtcNow;
        private static string _Header = "[PartioServer] ";
        private static ConcurrentDictionary<string, AuthContext> _AuthContexts = new ConcurrentDictionary<string, AuthContext>();
        private static ConcurrentDictionary<string, InFlightRequest> _InFlightRequests = new ConcurrentDictionary<string, InFlightRequest>();
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

            // 5. Initialize services
            _AuthService = new AuthenticationService(_Settings, _Database, _Logging);
            _ChunkingEngine = new ChunkingEngine(_Logging);

            // 6. Request history
            if (_Settings.RequestHistory.Enabled)
            {
                _RequestHistoryService = new RequestHistoryService(_Settings, _Database, _Logging);
                _CleanupService = new RequestHistoryCleanupService(_Settings, _Database, _Logging);
                _CleanupService.Start();
                _Logging.Info(_Header + "request history enabled");
            }

            // 6b. Health check services
            _HealthCheckService = new EmbeddingHealthCheckService(_Database, _Logging);
            await _HealthCheckService.StartAsync().ConfigureAwait(false);
            _CompletionHealthCheckService = new CompletionHealthCheckService(_Database, _Logging);
            await _CompletionHealthCheckService.StartAsync().ConfigureAwait(false);

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
                    catch (EndpointUnhealthyException ex)
                    {
                        statusCode = 500;
                        if (_Settings.Debug.Exceptions) _Logging.Warn(_Header + "exception: " + ex.Message);
                        throw new WebserverException(ApiResultEnum.InternalError, ex.Message);
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

        private static async Task<object> ProcessSingle(ApiRequest req)
        {
            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);

            SemanticCellRequest? cellReq = null;

            try
            {
                AuthContext auth = (AuthContext)req.Metadata;
                cellReq = req.GetData<SemanticCellRequest>();
                if (cellReq == null) throw new ArgumentException("Request body is required.");

                EmbeddingEndpoint endpoint = await ResolveEmbeddingEndpointFromBody(cellReq.EmbeddingConfiguration.EmbeddingEndpointId, auth).ConfigureAwait(false);

                req.Http.Response.Headers.Add(Constants.EndpointIdHeader, endpoint.Id);
                req.Http.Response.Headers.Add(Constants.ModelHeader, endpoint.Model);

                ProcessCellResult cellResult = await ProcessCellAsync(cellReq, endpoint).ConfigureAwait(false);

                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    inflight.DetailRecorded = true;
                    string requestJson = _Serializer.SerializeJson(cellReq, false);
                    string responseJson = _Serializer.SerializeJson(cellResult.Response, false);
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await _RequestHistoryService!.UpdateWithResponseAsync(
                        inflight.Entry, 200, inflight.Stopwatch.Elapsed.TotalMilliseconds, requestJson, responseJson, reqHeaders, respHeaders, cellResult.EmbeddingCalls, cellResult.CompletionCalls).ConfigureAwait(false);
                }

                return cellResult.Response;
            }
            catch (Exception ex)
            {
                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    inflight.DetailRecorded = true;
                    int statusCode = MapExceptionToStatusCode(ex);
                    string? requestBody = cellReq != null ? _Serializer.SerializeJson(cellReq, false) : null;
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await _RequestHistoryService!.UpdateWithResponseAsync(
                        inflight.Entry, statusCode, inflight.Stopwatch.Elapsed.TotalMilliseconds, requestBody, ex.Message, reqHeaders, respHeaders, null, null).ConfigureAwait(false);
                }
                throw;
            }
        }

        private static async Task<object> ProcessBatch(ApiRequest req)
        {
            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);

            List<SemanticCellRequest>? cellReqs = null;

            try
            {
                AuthContext auth = (AuthContext)req.Metadata;
                cellReqs = req.GetData<List<SemanticCellRequest>>();
                if (cellReqs == null || cellReqs.Count == 0) throw new ArgumentException("Request body must contain at least one cell.");

                // Resolve embedding endpoint from the first cell's config (all cells share the same endpoint in batch)
                string embeddingEndpointId = cellReqs[0].EmbeddingConfiguration.EmbeddingEndpointId;
                EmbeddingEndpoint endpoint = await ResolveEmbeddingEndpointFromBody(embeddingEndpointId, auth).ConfigureAwait(false);

                req.Http.Response.Headers.Add(Constants.EndpointIdHeader, endpoint.Id);
                req.Http.Response.Headers.Add(Constants.ModelHeader, endpoint.Model);

                List<SemanticCellResponse> responses = new List<SemanticCellResponse>();
                List<EmbeddingCallDetail> allEmbeddingCalls = new List<EmbeddingCallDetail>();
                List<CompletionCallDetail> allCompletionCalls = new List<CompletionCallDetail>();
                foreach (SemanticCellRequest cellReq in cellReqs)
                {
                    ProcessCellResult cellResult = await ProcessCellAsync(cellReq, endpoint).ConfigureAwait(false);
                    responses.Add(cellResult.Response);
                    allEmbeddingCalls.AddRange(cellResult.EmbeddingCalls);
                    allCompletionCalls.AddRange(cellResult.CompletionCalls);
                }

                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    inflight.DetailRecorded = true;
                    string requestJson = _Serializer.SerializeJson(cellReqs, false);
                    string responseJson = _Serializer.SerializeJson(responses, false);
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await _RequestHistoryService!.UpdateWithResponseAsync(
                        inflight.Entry, 200, inflight.Stopwatch.Elapsed.TotalMilliseconds, requestJson, responseJson, reqHeaders, respHeaders, allEmbeddingCalls, allCompletionCalls).ConfigureAwait(false);
                }

                return responses;
            }
            catch (Exception ex)
            {
                if (inflight != null)
                {
                    inflight.Stopwatch.Stop();
                    inflight.DetailRecorded = true;
                    int statusCode = MapExceptionToStatusCode(ex);
                    string? requestBody = cellReqs != null ? _Serializer.SerializeJson(cellReqs, false) : null;
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await _RequestHistoryService!.UpdateWithResponseAsync(
                        inflight.Entry, statusCode, inflight.Stopwatch.Elapsed.TotalMilliseconds, requestBody, ex.Message, reqHeaders, respHeaders, null, null).ConfigureAwait(false);
                }
                throw;
            }
        }

        private static async Task<EmbeddingEndpoint> ResolveEmbeddingEndpointFromBody(string id, AuthContext auth)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("EmbeddingConfiguration.EmbeddingEndpointId is required.");

            EmbeddingEndpoint? endpoint = await _Database.EmbeddingEndpoint.ReadByIdAsync(id).ConfigureAwait(false);

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

        private static async Task<CompletionEndpoint> ResolveCompletionEndpointFromBody(string id, AuthContext auth)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Completion endpoint ID is required.");

            CompletionEndpoint? endpoint = await _Database.CompletionEndpoint.ReadByIdAsync(id).ConfigureAwait(false);

            if (endpoint == null || (!auth.IsGlobalAdmin && endpoint.TenantId != auth.TenantId))
                throw new KeyNotFoundException("Completion endpoint not found: " + id);

            if (!endpoint.Active)
                throw new ArgumentException("Completion endpoint '" + id + "' is inactive.");

            if (_CompletionHealthCheckService != null && !_CompletionHealthCheckService.IsHealthy(endpoint.Id))
                throw new EndpointUnhealthyException(endpoint.Id,
                    "Completion endpoint " + endpoint.Id + " (" + endpoint.Model + ") is currently unhealthy");

            return endpoint;
        }

        private static async Task<object> ExploreEmbeddingEndpoint(ApiRequest req)
        {
            string connId = req.Http.Guid.ToString();
            _InFlightRequests.TryGetValue(connId, out InFlightRequest? inflight);

            EndpointExplorerEmbeddingResponse response = new EndpointExplorerEmbeddingResponse();
            EndpointExplorerEmbeddingRequest? explorerReq = null;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                AuthContext auth = (AuthContext)req.Metadata;
                explorerReq = req.GetData<EndpointExplorerEmbeddingRequest>();
                if (explorerReq == null) throw new ArgumentException("Request body is required.");
                if (string.IsNullOrWhiteSpace(explorerReq.EndpointId)) throw new ArgumentException("EndpointId is required.");
                if (string.IsNullOrWhiteSpace(explorerReq.Input)) throw new ArgumentException("Input is required.");

                EmbeddingEndpoint endpoint = await ResolveEmbeddingEndpointFromBody(explorerReq.EndpointId, auth).ConfigureAwait(false);
                response.EndpointId = endpoint.Id;
                response.Model = endpoint.Model;
                response.Input = explorerReq.Input;

                if (inflight != null)
                    response.RequestHistoryId = inflight.Entry.Id;

                req.Http.Response.Headers.Add(Constants.EndpointIdHeader, endpoint.Id);
                req.Http.Response.Headers.Add(Constants.ModelHeader, endpoint.Model);

                using EmbeddingClientBase client = CreateEmbeddingClient(endpoint);

                try
                {
                    List<float> embedding = await client.EmbedAsync(explorerReq.Input, endpoint.Model).ConfigureAwait(false);
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
                        inflight.DetailRecorded = true;
                        string requestJson = _Serializer.SerializeJson(explorerReq, false);
                        string responseJson = _Serializer.SerializeJson(response, false);
                        Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                        Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                        await _RequestHistoryService!.UpdateWithResponseAsync(
                            inflight.Entry, 200, inflight.Stopwatch.Elapsed.TotalMilliseconds, requestJson, responseJson, reqHeaders, respHeaders, response.EmbeddingCalls, null).ConfigureAwait(false);
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    sw.Stop();

                    response.Success = false;
                    response.StatusCode = MapExceptionToStatusCode(ex);
                    response.Error = ex.Message;
                    response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                    response.EmbeddingCalls = client.CallDetails.ToList();

                    if (inflight != null)
                    {
                        inflight.Stopwatch.Stop();
                        inflight.DetailRecorded = true;
                        string requestJson = _Serializer.SerializeJson(explorerReq, false);
                        string responseJson = _Serializer.SerializeJson(response, false);
                        Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                        Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                        await _RequestHistoryService!.UpdateWithResponseAsync(
                            inflight.Entry, response.StatusCode, inflight.Stopwatch.Elapsed.TotalMilliseconds, requestJson, responseJson, reqHeaders, respHeaders, response.EmbeddingCalls, null).ConfigureAwait(false);
                    }

                    return response;
                }
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

            EndpointExplorerCompletionResponse response = new EndpointExplorerCompletionResponse();
            EndpointExplorerCompletionRequest? explorerReq = null;
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                AuthContext auth = (AuthContext)req.Metadata;
                explorerReq = req.GetData<EndpointExplorerCompletionRequest>();
                if (explorerReq == null) throw new ArgumentException("Request body is required.");
                if (string.IsNullOrWhiteSpace(explorerReq.EndpointId)) throw new ArgumentException("EndpointId is required.");
                if (string.IsNullOrWhiteSpace(explorerReq.Prompt)) throw new ArgumentException("Prompt is required.");

                CompletionEndpoint endpoint = await ResolveCompletionEndpointFromBody(explorerReq.EndpointId, auth).ConfigureAwait(false);
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
                        default,
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
                        inflight.DetailRecorded = true;
                        string requestJson = _Serializer.SerializeJson(explorerReq, false);
                        string responseJson = _Serializer.SerializeJson(response, false);
                        Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                        Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                        await _RequestHistoryService!.UpdateWithResponseAsync(
                            inflight.Entry, 200, inflight.Stopwatch.Elapsed.TotalMilliseconds, requestJson, responseJson, reqHeaders, respHeaders, null, response.CompletionCalls).ConfigureAwait(false);
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    sw.Stop();

                    response.Success = false;
                    response.StatusCode = MapExceptionToStatusCode(ex);
                    response.Error = ex.Message;
                    response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
                    response.CompletionCalls = client.CallDetails.ToList();

                    if (inflight != null)
                    {
                        inflight.Stopwatch.Stop();
                        inflight.DetailRecorded = true;
                        string requestJson = _Serializer.SerializeJson(explorerReq, false);
                        string responseJson = _Serializer.SerializeJson(response, false);
                        Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                        Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                        await _RequestHistoryService!.UpdateWithResponseAsync(
                            inflight.Entry, response.StatusCode, inflight.Stopwatch.Elapsed.TotalMilliseconds, requestJson, responseJson, reqHeaders, respHeaders, null, response.CompletionCalls).ConfigureAwait(false);
                    }

                    return response;
                }
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
        /// so N model tokens ≈ N * 0.75 cl100k_base tokens.
        /// </summary>
        private const double _TokenScalingFactor = 0.75;

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

        private static async Task<ProcessCellResult> ProcessCellAsync(SemanticCellRequest request, EmbeddingEndpoint endpoint)
        {
            // 1. Normalize hierarchy
            List<SemanticCellRequest> rootCells = SummarizationEngine.Deflatten(new List<SemanticCellRequest> { request });

            // 2. Summarize (if configured)
            List<CompletionCallDetail> completionCalls = new List<CompletionCallDetail>();
            if (request.SummarizationConfiguration != null)
            {
                SummarizationConfiguration sumConfig = request.SummarizationConfiguration;

                CompletionEndpoint? compEndpoint = await _Database.CompletionEndpoint.ReadByIdAsync(sumConfig.CompletionEndpointId).ConfigureAwait(false);
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
                    rootCells = await summarizer.SummarizeAsync(rootCells, sumConfig, compClient, compEndpoint.Model).ConfigureAwait(false);
                    completionCalls.AddRange(compClient.CallDetails);
                }
            }

            // 3. Chunk and embed all cells (including summaries) recursively
            string model = endpoint.Model;
            EmbeddingClientBase client = CreateEmbeddingClient(endpoint);
            using (client)
            {
                // Query the model's context length and cap the chunk size accordingly
                int? modelContextLength = await client.GetModelContextLengthAsync(model).ConfigureAwait(false);
                int maxCl100kTokens = int.MaxValue;
                if (modelContextLength.HasValue)
                {
                    maxCl100kTokens = (int)(modelContextLength.Value * _TokenScalingFactor);
                    if (maxCl100kTokens < 1) maxCl100kTokens = 1;
                }

                // Process all cells in the hierarchy
                List<SemanticCellResponse> rootResponses = new List<SemanticCellResponse>();
                foreach (SemanticCellRequest rootCell in rootCells)
                {
                    SemanticCellResponse resp = await ProcessCellHierarchyAsync(rootCell, client, model, maxCl100kTokens).ConfigureAwait(false);
                    rootResponses.Add(resp);
                }

                // For single cell processing, return the first root response
                SemanticCellResponse response = rootResponses.Count > 0 ? rootResponses[0] : new SemanticCellResponse();

                ProcessCellResult cellResult = new ProcessCellResult();
                cellResult.Response = response;
                cellResult.EmbeddingCalls = client.CallDetails.ToList();
                cellResult.CompletionCalls = completionCalls;
                return cellResult;
            }
        }

        private static async Task<SemanticCellResponse> ProcessCellHierarchyAsync(
            SemanticCellRequest request, EmbeddingClientBase client, string model, int maxCl100kTokens)
        {
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

            if (request.ChunkingConfiguration.FixedTokenCount > maxCl100kTokens)
            {
                _Logging.Info("[ProcessCell] capping FixedTokenCount from "
                    + request.ChunkingConfiguration.FixedTokenCount
                    + " to " + maxCl100kTokens);
                request.ChunkingConfiguration.FixedTokenCount = maxCl100kTokens;
            }

            // Chunk
            List<ChunkResult> chunks = _ChunkingEngine.Chunk(request);

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
                List<List<float>> embeddings = await client.EmbedBatchAsync(textsToEmbed, model).ConfigureAwait(false);

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
                    SemanticCellResponse childResp = await ProcessCellHierarchyAsync(child, client, model, maxCl100kTokens).ConfigureAwait(false);
                    response.Children.Add(childResp);
                }
            }

            return response;
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
            if (ex is EndpointUnhealthyException) return 502;
            return 500;
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

        private static EmbeddingClientBase CreateEmbeddingClient(EmbeddingEndpoint endpoint)
        {
            switch (endpoint.ApiFormat)
            {
                case ApiFormatEnum.Ollama:
                    return new OllamaEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, _Logging);
                case ApiFormatEnum.OpenAI:
                case ApiFormatEnum.vLLM:
                    return new OpenAiEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, _Logging);
                case ApiFormatEnum.Gemini:
                    return new GeminiEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, _Logging);
                default:
                    throw new ArgumentException("Unsupported API format: " + endpoint.ApiFormat);
            }
        }

        private static CompletionClientBase CreateCompletionClient(CompletionEndpoint endpoint)
        {
            switch (endpoint.ApiFormat)
            {
                case ApiFormatEnum.Ollama:
                    return new OllamaCompletionClient(endpoint.Endpoint, endpoint.ApiKey, _Logging);
                case ApiFormatEnum.OpenAI:
                case ApiFormatEnum.vLLM:
                    return new OpenAiCompletionClient(endpoint.Endpoint, endpoint.ApiKey, _Logging);
                case ApiFormatEnum.Gemini:
                    return new GeminiCompletionClient(endpoint.Endpoint, endpoint.ApiKey, _Logging);
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
            _HealthCheckService?.OnEndpointUpdated(updated);
            return updated;
        }

        private static async Task<object> DeleteEndpoint(ApiRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.EmbeddingEndpoint.DeleteByIdAsync(id).ConfigureAwait(false);
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
