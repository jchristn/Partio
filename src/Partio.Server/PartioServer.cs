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
    using SwiftStack;
    using SwiftStack.Rest;
    using SwiftStack.Rest.OpenApi;
    using WatsonWebserver.Core;
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

            // 7. Initialize SwiftStack
            SwiftStackApp app = new SwiftStackApp("Partio Server");
            app.Serializer = _Serializer;

            RestApp rest = app.Rest;
            rest.WebserverSettings.Hostname = _Settings.Rest.Hostname;
            rest.WebserverSettings.Port = _Settings.Rest.Port;
            rest.WebserverSettings.Ssl.Enable = _Settings.Rest.Ssl;

            // OpenAPI / Swagger
            rest.UseOpenApi(settings =>
            {
                settings.Info = new OpenApiInfo
                {
                    Title = "Partio API",
                    Version = Constants.Version,
                    Description = "Multi-tenant semantic cell processing with chunking and embedding."
                };
                settings.Tags = new List<OpenApiTag>
                {
                    new OpenApiTag("Health", "Health check endpoints"),
                    new OpenApiTag("Process", "Chunk and embed semantic cells"),
                    new OpenApiTag("Tenants", "Tenant management (admin)"),
                    new OpenApiTag("Users", "User management (admin)"),
                    new OpenApiTag("Credentials", "Credential management (admin)"),
                    new OpenApiTag("Embedding Endpoints", "Embedding endpoint management (admin)"),
                    new OpenApiTag("Completion Endpoints", "Completion/inference endpoint management (admin)"),
                    new OpenApiTag("Requests", "Request history (admin)")
                };
                settings.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
                {
                    ["Bearer"] = OpenApiSecurityScheme.Bearer("token", "Bearer token authentication. Use an admin API key or credential bearer token.")
                };
            });

            #region Routes

            rest.AuthenticationRoute = async (HttpContextBase ctx) =>
            {
                string? authHeader = ctx.Request.Headers?[Constants.AuthorizationHeader];
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
                    : AuthenticationResultEnum.Invalid;
                return result;
            };
            rest.PreRoutingRoute = async (HttpContextBase ctx) =>
            {
                ctx.Response.ContentType = Constants.JsonContentType;
            };
            rest.PostRoutingRoute = async (HttpContextBase ctx) =>
            {
                if (_Settings.Debug.Requests)
                    _Logging.Info(_Header + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithQuery + " " + ctx.Response.StatusCode);
            };
            rest.ExceptionRoute = async (HttpContextBase ctx, Exception ex) =>
            {
                if (_Settings.Debug.Exceptions)
                    _Logging.Warn(_Header + "exception: " + ex.Message);

                int statusCode = 500;
                string error = "InternalServerError";

                if (ex is KeyNotFoundException)
                {
                    statusCode = 404;
                    error = "NotFound";
                }
                else if (ex is ArgumentException || ex is ArgumentNullException)
                {
                    statusCode = 400;
                    error = "BadRequest";
                }
                else if (ex is UnauthorizedAccessException)
                {
                    statusCode = 401;
                    error = "Unauthorized";
                }
                else if (ex is EndpointUnhealthyException)
                {
                    statusCode = 502;
                    error = "BadGateway";
                }

                ctx.Response.StatusCode = statusCode;
                ctx.Response.ContentType = Constants.JsonContentType;

                ApiErrorResponse errorResponse = new ApiErrorResponse
                {
                    Error = error,
                    Message = ex.Message,
                    StatusCode = statusCode
                };

                string json = _Serializer.SerializeJson(errorResponse, true);
                await ctx.Response.Send(json).ConfigureAwait(false);
            };

            #region Health

            // Health (no auth)
            rest.Head("/", HealthHead, api => api
                .WithTag("Health")
                .WithSummary("Health check")
                .WithResponse(200, OpenApiResponseMetadata.Create("OK")), false);
            rest.Get("/", HealthGet, api => api
                .WithTag("Health")
                .WithSummary("Health status")
                .WithResponse(200, OpenApiResponseMetadata.Json<Dictionary<string, string>>("Health status")), false);
            rest.Get("/v1.0/health", HealthJson, api => api
                .WithTag("Health")
                .WithSummary("Health status JSON")
                .WithResponse(200, OpenApiResponseMetadata.Json<Dictionary<string, string>>("Health status")), false);
            rest.Get("/v1.0/whoami", WhoAmI, api => api
                .WithTag("Health")
                .WithSummary("Returns the role and tenant of the authenticated caller")
                .WithResponse(200, OpenApiResponseMetadata.Json<Dictionary<string, string>>("Caller identity"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized("Missing or invalid token"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);

            #endregion

            #region Processing

            // Process (auth required)
            rest.Post<SemanticCellRequest>("/v1.0/process", ProcessSingle, api => api
                .WithTag("Process")
                .WithSummary("Process a single semantic cell")
                .WithDescription("Optionally summarizes, then chunks and embeds a single semantic cell. Embedding endpoint ID is specified in EmbeddingConfiguration.EmbeddingEndpointId.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<SemanticCellRequest>("Semantic cell to process", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<SemanticCellResponse>("Processed cell with chunks and embeddings"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest("Invalid request or inactive endpoint"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized("Missing or invalid token"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Embedding endpoint not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Post<List<SemanticCellRequest>>("/v1.0/process/batch", ProcessBatch, api => api
                .WithTag("Process")
                .WithSummary("Process multiple semantic cells")
                .WithDescription("Optionally summarizes, then chunks and embeds multiple semantic cells in a single request.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<List<SemanticCellRequest>>("Semantic cells to process", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<List<SemanticCellResponse>>("Processed cells"))
                .WithResponse(400, OpenApiResponseMetadata.BadRequest("Invalid request or inactive endpoint"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized("Missing or invalid token"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Embedding endpoint not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);

            #endregion

            #region Tenants

            // Tenants (admin)
            rest.Put<TenantMetadata>("/v1.0/tenants", CreateTenant, api => api
                .WithTag("Tenants")
                .WithSummary("Create a tenant")
                .WithDescription("Creates a tenant along with a default user, credential, and embedding endpoints.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<TenantMetadata>("Tenant to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<TenantMetadata>("Created tenant"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized("Admin access required"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Get("/v1.0/tenants/{id}", ReadTenant, api => api
                .WithTag("Tenants")
                .WithSummary("Read a tenant")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Json<TenantMetadata>("Tenant details"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Tenant not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Put<TenantMetadata>("/v1.0/tenants/{id}", UpdateTenant, api => api
                .WithTag("Tenants")
                .WithSummary("Update a tenant")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID", OpenApiSchemaMetadata.String()))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<TenantMetadata>("Tenant fields to update", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<TenantMetadata>("Updated tenant"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Tenant not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Delete("/v1.0/tenants/{id}", DeleteTenant, api => api
                .WithTag("Tenants")
                .WithSummary("Delete a tenant")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID", OpenApiSchemaMetadata.String()))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Tenant not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Head("/v1.0/tenants/{id}", HeadTenant, api => api
                .WithTag("Tenants")
                .WithSummary("Check if a tenant exists")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Tenant ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Create("Tenant exists"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Tenant not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Post<EnumerationRequest>("/v1.0/tenants/enumerate", EnumerateTenants, api => api
                .WithTag("Tenants")
                .WithSummary("List tenants")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationRequest>("Pagination and filter options", false))
                .WithResponse(200, OpenApiResponseMetadata.Json<EnumerationResult<TenantMetadata>>("Paginated tenant list"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);

            #endregion

            #region Users

            // Users (admin)
            rest.Put<UserMaster>("/v1.0/users", CreateUser, api => api
                .WithTag("Users")
                .WithSummary("Create a user")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<UserMaster>("User to create", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<UserMaster>("Created user (password redacted)"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized("Admin access required"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Get("/v1.0/users/{id}", ReadUser, api => api
                .WithTag("Users")
                .WithSummary("Read a user")
                .WithParameter(OpenApiParameterMetadata.Path("id", "User ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Json<UserMaster>("User details (password redacted)"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("User not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Put<UserMaster>("/v1.0/users/{id}", UpdateUser, api => api
                .WithTag("Users")
                .WithSummary("Update a user")
                .WithParameter(OpenApiParameterMetadata.Path("id", "User ID", OpenApiSchemaMetadata.String()))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<UserMaster>("User fields to update", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<UserMaster>("Updated user (password redacted)"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("User not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Delete("/v1.0/users/{id}", DeleteUser, api => api
                .WithTag("Users")
                .WithSummary("Delete a user")
                .WithParameter(OpenApiParameterMetadata.Path("id", "User ID", OpenApiSchemaMetadata.String()))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(404, OpenApiResponseMetadata.NotFound("User not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Head("/v1.0/users/{id}", HeadUser, api => api
                .WithTag("Users")
                .WithSummary("Check if a user exists")
                .WithParameter(OpenApiParameterMetadata.Path("id", "User ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Create("User exists"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("User not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Post<EnumerationRequest>("/v1.0/users/enumerate", EnumerateUsers, api => api
                .WithTag("Users")
                .WithSummary("List users")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationRequest>("Pagination and filter options", false))
                .WithResponse(200, OpenApiResponseMetadata.Json<EnumerationResult<UserMaster>>("Paginated user list"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);

            #endregion

            #region Credentials

            // Credentials (admin)
            rest.Put<Credential>("/v1.0/credentials", CreateCredential, api => api
                .WithTag("Credentials")
                .WithSummary("Create a credential")
                .WithDescription("Creates a credential and generates a bearer token.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<Credential>("Credential to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<Credential>("Created credential with bearer token"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized("Admin access required"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Get("/v1.0/credentials/{id}", ReadCredential, api => api
                .WithTag("Credentials")
                .WithSummary("Read a credential")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Json<Credential>("Credential details"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Credential not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Put<Credential>("/v1.0/credentials/{id}", UpdateCredential, api => api
                .WithTag("Credentials")
                .WithSummary("Update a credential")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID", OpenApiSchemaMetadata.String()))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<Credential>("Credential fields to update", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<Credential>("Updated credential"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Credential not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Delete("/v1.0/credentials/{id}", DeleteCredential, api => api
                .WithTag("Credentials")
                .WithSummary("Delete a credential")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID", OpenApiSchemaMetadata.String()))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Credential not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Head("/v1.0/credentials/{id}", HeadCredential, api => api
                .WithTag("Credentials")
                .WithSummary("Check if a credential exists")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Credential ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Create("Credential exists"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Credential not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Post<EnumerationRequest>("/v1.0/credentials/enumerate", EnumerateCredentials, api => api
                .WithTag("Credentials")
                .WithSummary("List credentials")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationRequest>("Pagination and filter options", false))
                .WithResponse(200, OpenApiResponseMetadata.Json<EnumerationResult<Credential>>("Paginated credential list"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);

            #endregion

            #region Endpoints

            // Embedding Endpoints (admin)
            // NOTE: Literal path routes (/health, /enumerate) must be registered BEFORE
            // parameterized routes (/{id}) to prevent the router from matching literal
            // segments as parameter values.
            rest.Put<EmbeddingEndpoint>("/v1.0/endpoints/embedding", CreateEndpoint, api => api
                .WithTag("Embedding Endpoints")
                .WithSummary("Create an embedding endpoint")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<EmbeddingEndpoint>("Endpoint to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<EmbeddingEndpoint>("Created endpoint"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized("Admin access required"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Post<EnumerationRequest>("/v1.0/endpoints/embedding/enumerate", EnumerateEndpoints, api => api
                .WithTag("Embedding Endpoints")
                .WithSummary("List embedding endpoints")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationRequest>("Pagination and filter options", false))
                .WithResponse(200, OpenApiResponseMetadata.Json<EnumerationResult<EmbeddingEndpoint>>("Paginated endpoint list"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Get("/v1.0/endpoints/embedding/health", GetAllEndpointHealth, api => api
                .WithTag("Embedding Endpoints")
                .WithSummary("List health status for all embedding endpoints")
                .WithDescription("Returns health status for all monitored embedding endpoints. Scoped by tenant for non-admins.")
                .WithResponse(200, OpenApiResponseMetadata.Json<List<EndpointHealthStatus>>("List of endpoint health statuses"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Get("/v1.0/endpoints/embedding/{id}", ReadEndpoint, api => api
                .WithTag("Embedding Endpoints")
                .WithSummary("Read an embedding endpoint")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Endpoint ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Json<EmbeddingEndpoint>("Endpoint details"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Endpoint not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Get("/v1.0/endpoints/embedding/{id}/health", GetEndpointHealth, api => api
                .WithTag("Embedding Endpoints")
                .WithSummary("Get health status for a single embedding endpoint")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Endpoint ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Json<EndpointHealthStatus>("Endpoint health status"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Endpoint not found or health check not enabled"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Put<EmbeddingEndpoint>("/v1.0/endpoints/embedding/{id}", UpdateEndpoint, api => api
                .WithTag("Embedding Endpoints")
                .WithSummary("Update an embedding endpoint")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Endpoint ID", OpenApiSchemaMetadata.String()))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<EmbeddingEndpoint>("Endpoint fields to update", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<EmbeddingEndpoint>("Updated endpoint"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Endpoint not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Delete("/v1.0/endpoints/embedding/{id}", DeleteEndpoint, api => api
                .WithTag("Embedding Endpoints")
                .WithSummary("Delete an embedding endpoint")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Endpoint ID", OpenApiSchemaMetadata.String()))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Endpoint not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Head("/v1.0/endpoints/embedding/{id}", HeadEndpoint, api => api
                .WithTag("Embedding Endpoints")
                .WithSummary("Check if an embedding endpoint exists")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Endpoint ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Create("Endpoint exists"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Endpoint not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);

            #endregion

            #region Completion Endpoints

            // Completion Endpoints (admin)
            rest.Put<CompletionEndpoint>("/v1.0/endpoints/completion", CreateCompletionEndpoint, api => api
                .WithTag("Completion Endpoints")
                .WithSummary("Create a completion endpoint")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<CompletionEndpoint>("Completion endpoint to create", true))
                .WithResponse(201, OpenApiResponseMetadata.Json<CompletionEndpoint>("Created completion endpoint"))
                .WithResponse(401, OpenApiResponseMetadata.Unauthorized("Admin access required"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Post<EnumerationRequest>("/v1.0/endpoints/completion/enumerate", EnumerateCompletionEndpoints, api => api
                .WithTag("Completion Endpoints")
                .WithSummary("List completion endpoints")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationRequest>("Pagination and filter options", false))
                .WithResponse(200, OpenApiResponseMetadata.Json<EnumerationResult<CompletionEndpoint>>("Paginated completion endpoint list"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Get("/v1.0/endpoints/completion/health", GetAllCompletionEndpointHealth, api => api
                .WithTag("Completion Endpoints")
                .WithSummary("List health status for all completion endpoints")
                .WithDescription("Returns health status for all monitored completion endpoints. Scoped by tenant for non-admins.")
                .WithResponse(200, OpenApiResponseMetadata.Json<List<EndpointHealthStatus>>("List of completion endpoint health statuses"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Get("/v1.0/endpoints/completion/{id}", ReadCompletionEndpoint, api => api
                .WithTag("Completion Endpoints")
                .WithSummary("Read a completion endpoint")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Completion endpoint ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Json<CompletionEndpoint>("Completion endpoint details"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Completion endpoint not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Get("/v1.0/endpoints/completion/{id}/health", GetCompletionEndpointHealth, api => api
                .WithTag("Completion Endpoints")
                .WithSummary("Get health status for a single completion endpoint")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Completion endpoint ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Json<EndpointHealthStatus>("Completion endpoint health status"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Completion endpoint not found or health check not enabled"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Put<CompletionEndpoint>("/v1.0/endpoints/completion/{id}", UpdateCompletionEndpoint, api => api
                .WithTag("Completion Endpoints")
                .WithSummary("Update a completion endpoint")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Completion endpoint ID", OpenApiSchemaMetadata.String()))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<CompletionEndpoint>("Completion endpoint fields to update", true))
                .WithResponse(200, OpenApiResponseMetadata.Json<CompletionEndpoint>("Updated completion endpoint"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Completion endpoint not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Delete("/v1.0/endpoints/completion/{id}", DeleteCompletionEndpoint, api => api
                .WithTag("Completion Endpoints")
                .WithSummary("Delete a completion endpoint")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Completion endpoint ID", OpenApiSchemaMetadata.String()))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Completion endpoint not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Head("/v1.0/endpoints/completion/{id}", HeadCompletionEndpoint, api => api
                .WithTag("Completion Endpoints")
                .WithSummary("Check if a completion endpoint exists")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Completion endpoint ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Create("Completion endpoint exists"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Completion endpoint not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);

            #endregion

            #region Request-History

            // Request History (admin)
            rest.Get("/v1.0/requests/{id}", ReadRequestHistory, api => api
                .WithTag("Requests")
                .WithSummary("Read a request history entry")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Request ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Json<RequestHistoryEntry>("Request history entry"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Entry not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Get("/v1.0/requests/{id}/detail", ReadRequestHistoryDetail, api => api
                .WithTag("Requests")
                .WithSummary("Read request/response body detail")
                .WithDescription("Reads the request and response body detail from the filesystem.")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Request ID", OpenApiSchemaMetadata.String()))
                .WithResponse(200, OpenApiResponseMetadata.Json<object>("Request and response body detail"))
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Entry or detail not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Post<EnumerationRequest>("/v1.0/requests/enumerate", EnumerateRequestHistory, api => api
                .WithTag("Requests")
                .WithSummary("List request history")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json<EnumerationRequest>("Pagination and filter options", false))
                .WithResponse(200, OpenApiResponseMetadata.Json<EnumerationResult<RequestHistoryEntry>>("Paginated request history"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);
            rest.Delete("/v1.0/requests/{id}", DeleteRequestHistory, api => api
                .WithTag("Requests")
                .WithSummary("Delete a request history entry")
                .WithParameter(OpenApiParameterMetadata.Path("id", "Request ID", OpenApiSchemaMetadata.String()))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(404, OpenApiResponseMetadata.NotFound("Entry not found"))
                .WithSecurity("Bearer", Array.Empty<string>()), true);

            #endregion

            #endregion

            // 8. Start server
            CancellationTokenSource serverCts = new CancellationTokenSource();
            Task serverTask = app.Rest.Run(serverCts.Token);
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
            try { await serverTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
            app.Rest.Dispose();
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
                new DefaultEmbeddingEndpoint { Model = "all-minilm", Endpoint = "http://localhost:11434", ApiFormat = ApiFormatEnum.Ollama },
            };
            defaults.DefaultInferenceEndpoints = new List<DefaultInferenceEndpoint>
            {
                new DefaultInferenceEndpoint { Name = "Default Inference", Model = "gemma3:4b", Endpoint = "http://localhost:11434", ApiFormat = ApiFormatEnum.Ollama },
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

        private static async Task<object> HealthHead(AppRequest req)
        {
            req.Http.Response.StatusCode = 200;
            return null!;
        }

        private static async Task<object> HealthGet(AppRequest req)
        {
            req.Http.Response.StatusCode = 200;
            return new Dictionary<string, object>
            {
                { "Status", "Healthy" },
                { "Version", Constants.Version },
                { "Uptime", DateTime.UtcNow - _StartTimeUtc }
            };
        }

        private static async Task<object> HealthJson(AppRequest req)
        {
            req.Http.Response.StatusCode = 200;
            return new Dictionary<string, object>
            {
                { "Status", "Healthy" },
                { "Version", Constants.Version },
                { "Uptime", DateTime.UtcNow - _StartTimeUtc }
            };
        }

        private static async Task<object> WhoAmI(AppRequest req)
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

        private static async Task<object> ProcessSingle(AppRequest req)
        {
            bool recordHistory = _Settings.RequestHistory.Enabled && _RequestHistoryService != null;
            RequestHistoryEntry? historyEntry = null;
            Stopwatch? sw = null;

            if (recordHistory)
            {
                AuthContext auth = (AuthContext)req.Metadata;
                historyEntry = await _RequestHistoryService!.CreateEntryAsync(
                    req.Http.Request.Method.ToString(),
                    req.Http.Request.Url.RawWithQuery,
                    req.Http.Request.Source.IpAddress,
                    auth).ConfigureAwait(false);
                sw = Stopwatch.StartNew();
            }

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

                if (recordHistory && historyEntry != null && sw != null)
                {
                    sw.Stop();
                    string requestJson = _Serializer.SerializeJson(cellReq, false);
                    string responseJson = _Serializer.SerializeJson(cellResult.Response, false);
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await _RequestHistoryService!.UpdateWithResponseAsync(
                        historyEntry, 200, sw.ElapsedMilliseconds, requestJson, responseJson, reqHeaders, respHeaders, cellResult.EmbeddingCalls, cellResult.CompletionCalls).ConfigureAwait(false);
                }

                return cellResult.Response;
            }
            catch (Exception ex)
            {
                if (recordHistory && historyEntry != null && sw != null)
                {
                    sw.Stop();
                    int statusCode = MapExceptionToStatusCode(ex);
                    string? requestBody = cellReq != null ? _Serializer.SerializeJson(cellReq, false) : null;
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await _RequestHistoryService!.UpdateWithResponseAsync(
                        historyEntry, statusCode, sw.ElapsedMilliseconds, requestBody, ex.Message, reqHeaders, respHeaders, null, null).ConfigureAwait(false);
                }
                throw;
            }
        }

        private static async Task<object> ProcessBatch(AppRequest req)
        {
            bool recordHistory = _Settings.RequestHistory.Enabled && _RequestHistoryService != null;
            RequestHistoryEntry? historyEntry = null;
            Stopwatch? sw = null;

            if (recordHistory)
            {
                AuthContext auth = (AuthContext)req.Metadata;
                historyEntry = await _RequestHistoryService!.CreateEntryAsync(
                    req.Http.Request.Method.ToString(),
                    req.Http.Request.Url.RawWithQuery,
                    req.Http.Request.Source.IpAddress,
                    auth).ConfigureAwait(false);
                sw = Stopwatch.StartNew();
            }

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

                if (recordHistory && historyEntry != null && sw != null)
                {
                    sw.Stop();
                    string requestJson = _Serializer.SerializeJson(cellReqs, false);
                    string responseJson = _Serializer.SerializeJson(responses, false);
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await _RequestHistoryService!.UpdateWithResponseAsync(
                        historyEntry, 200, sw.ElapsedMilliseconds, requestJson, responseJson, reqHeaders, respHeaders, allEmbeddingCalls, allCompletionCalls).ConfigureAwait(false);
                }

                return responses;
            }
            catch (Exception ex)
            {
                if (recordHistory && historyEntry != null && sw != null)
                {
                    sw.Stop();
                    int statusCode = MapExceptionToStatusCode(ex);
                    string? requestBody = cellReqs != null ? _Serializer.SerializeJson(cellReqs, false) : null;
                    Dictionary<string, string> reqHeaders = ExtractHeaders(req.Http.Request.Headers);
                    Dictionary<string, string> respHeaders = ExtractHeaders(req.Http.Response.Headers);
                    await _RequestHistoryService!.UpdateWithResponseAsync(
                        historyEntry, statusCode, sw.ElapsedMilliseconds, requestBody, ex.Message, reqHeaders, respHeaders, null, null).ConfigureAwait(false);
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

        /// <summary>
        /// Scaling factor to convert from model-native token counts to cl100k_base token counts.
        /// cl100k_base (100k vocab BPE) is more efficient than most embedding model tokenizers,
        /// so N model tokens ≈ N * 0.75 cl100k_base tokens.
        /// </summary>
        private const double TokenScalingFactor = 0.75;

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
                    maxCl100kTokens = (int)(modelContextLength.Value * TokenScalingFactor);
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
                    return new OpenAiEmbeddingClient(endpoint.Endpoint, endpoint.ApiKey, _Logging);
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
                    return new OpenAiCompletionClient(endpoint.Endpoint, endpoint.ApiKey, _Logging);
                default:
                    throw new ArgumentException("Unsupported API format: " + endpoint.ApiFormat);
            }
        }

        #endregion

        #region Tenants

        private static async Task<object> CreateTenant(AppRequest req)
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

        private static async Task<object> ReadTenant(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            TenantMetadata? tenant = await _Database.Tenant.ReadByIdAsync(id).ConfigureAwait(false);
            if (tenant == null) throw new KeyNotFoundException("Tenant not found: " + id);
            return tenant;
        }

        private static async Task<object> UpdateTenant(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            TenantMetadata? tenant = req.GetData<TenantMetadata>();
            if (tenant == null) throw new ArgumentException("Request body is required.");
            tenant.Id = id;
            TenantMetadata updated = await _Database.Tenant.UpdateAsync(tenant).ConfigureAwait(false);
            return updated;
        }

        private static async Task<object> DeleteTenant(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.Tenant.DeleteByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        private static async Task<object> HeadTenant(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            bool exists = await _Database.Tenant.ExistsByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null!;
        }

        private static async Task<object> EnumerateTenants(AppRequest req)
        {
            RequireAdmin(req);
            EnumerationRequest? enumReq = req.GetData<EnumerationRequest>();
            if (enumReq == null) enumReq = new EnumerationRequest();
            EnumerationResult<TenantMetadata> result = await _Database.Tenant.EnumerateAsync(enumReq).ConfigureAwait(false);
            return result;
        }

        #endregion

        #region Users

        private static async Task<object> CreateUser(AppRequest req)
        {
            RequireAdmin(req);
            UserMaster? user = req.GetData<UserMaster>();
            if (user == null) throw new ArgumentException("Request body is required.");
            UserMaster created = await _Database.User.CreateAsync(user).ConfigureAwait(false);
            return UserMaster.Redact(created);
        }

        private static async Task<object> ReadUser(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            UserMaster? user = await _Database.User.ReadByIdAsync(id).ConfigureAwait(false);
            if (user == null) throw new KeyNotFoundException("User not found: " + id);
            return UserMaster.Redact(user);
        }

        private static async Task<object> UpdateUser(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            UserMaster? user = req.GetData<UserMaster>();
            if (user == null) throw new ArgumentException("Request body is required.");
            user.Id = id;
            UserMaster updated = await _Database.User.UpdateAsync(user).ConfigureAwait(false);
            return UserMaster.Redact(updated);
        }

        private static async Task<object> DeleteUser(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.User.DeleteByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        private static async Task<object> HeadUser(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            bool exists = await _Database.User.ExistsByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null!;
        }

        private static async Task<object> EnumerateUsers(AppRequest req)
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

        private static async Task<object> CreateCredential(AppRequest req)
        {
            RequireAdmin(req);
            Credential? cred = req.GetData<Credential>();
            if (cred == null) throw new ArgumentException("Request body is required.");
            Credential created = await _Database.Credential.CreateAsync(cred).ConfigureAwait(false);
            req.Http.Response.StatusCode = 201;
            return created;
        }

        private static async Task<object> ReadCredential(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            Credential? cred = await _Database.Credential.ReadByIdAsync(id).ConfigureAwait(false);
            if (cred == null) throw new KeyNotFoundException("Credential not found: " + id);
            return cred;
        }

        private static async Task<object> UpdateCredential(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            Credential? cred = req.GetData<Credential>();
            if (cred == null) throw new ArgumentException("Request body is required.");
            cred.Id = id;
            Credential updated = await _Database.Credential.UpdateAsync(cred).ConfigureAwait(false);
            return updated;
        }

        private static async Task<object> DeleteCredential(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.Credential.DeleteByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        private static async Task<object> HeadCredential(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            bool exists = await _Database.Credential.ExistsByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null!;
        }

        private static async Task<object> EnumerateCredentials(AppRequest req)
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

        private static async Task<object> CreateEndpoint(AppRequest req)
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

        private static async Task<object> ReadEndpoint(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            EmbeddingEndpoint? ep = await _Database.EmbeddingEndpoint.ReadByIdAsync(id).ConfigureAwait(false);
            if (ep == null) throw new KeyNotFoundException("Embedding endpoint not found: " + id);
            return ep;
        }

        private static async Task<object> UpdateEndpoint(AppRequest req)
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

        private static async Task<object> DeleteEndpoint(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.EmbeddingEndpoint.DeleteByIdAsync(id).ConfigureAwait(false);
            _HealthCheckService?.OnEndpointDeleted(id);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        private static async Task<object> HeadEndpoint(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            bool exists = await _Database.EmbeddingEndpoint.ExistsByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null!;
        }

        private static async Task<object> EnumerateEndpoints(AppRequest req)
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

        private static async Task<object> GetAllEndpointHealth(AppRequest req)
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

        private static async Task<object> GetEndpointHealth(AppRequest req)
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

        private static async Task<object> CreateCompletionEndpoint(AppRequest req)
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

        private static async Task<object> ReadCompletionEndpoint(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            CompletionEndpoint? ep = await _Database.CompletionEndpoint.ReadByIdAsync(id).ConfigureAwait(false);
            if (ep == null) throw new KeyNotFoundException("Completion endpoint not found: " + id);
            return ep;
        }

        private static async Task<object> UpdateCompletionEndpoint(AppRequest req)
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

        private static async Task<object> DeleteCompletionEndpoint(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.CompletionEndpoint.DeleteByIdAsync(id).ConfigureAwait(false);
            _CompletionHealthCheckService?.OnEndpointDeleted(id);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        private static async Task<object> HeadCompletionEndpoint(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            bool exists = await _Database.CompletionEndpoint.ExistsByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = exists ? 200 : 404;
            return null!;
        }

        private static async Task<object> EnumerateCompletionEndpoints(AppRequest req)
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

        private static async Task<object> GetAllCompletionEndpointHealth(AppRequest req)
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

        private static async Task<object> GetCompletionEndpointHealth(AppRequest req)
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

        private static async Task<object> ReadRequestHistory(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            RequestHistoryEntry? entry = await _Database.RequestHistory.ReadByIdAsync(id).ConfigureAwait(false);
            if (entry == null) throw new KeyNotFoundException("Request history entry not found: " + id);
            return entry;
        }

        private static async Task<object> ReadRequestHistoryDetail(AppRequest req)
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

        private static async Task<object> EnumerateRequestHistory(AppRequest req)
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

        private static async Task<object> DeleteRequestHistory(AppRequest req)
        {
            RequireAdmin(req);
            string id = req.Parameters["id"];
            await _Database.RequestHistory.DeleteByIdAsync(id).ConfigureAwait(false);
            req.Http.Response.StatusCode = 204;
            return null!;
        }

        #endregion

        #region Helpers

        private static void RequireAdmin(AppRequest req)
        {
            AuthContext auth = (AuthContext)req.Metadata;
            if (!auth.IsGlobalAdmin)
            {
                throw new UnauthorizedAccessException("Admin access required.");
            }
        }

        #endregion
    }
}
