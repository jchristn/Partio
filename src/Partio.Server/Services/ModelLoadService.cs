namespace Partio.Server.Services
{
    using System.Diagnostics;
    using Partio.Core.Enums;
    using Partio.Core.Exceptions;
    using Partio.Core.Models;
    using Partio.Core.ThirdParty;
    using SyslogLogging;

    /// <summary>
    /// Orchestrates provider-specific model load and warm requests.
    /// </summary>
    public class ModelLoadService
    {
        private readonly LoggingModule _Logging;
        private readonly Func<EmbeddingEndpoint, EmbeddingClientBase> _CreateEmbeddingClient;
        private readonly Func<CompletionEndpoint, CompletionClientBase> _CreateCompletionClient;
        private readonly string _Header = "[ModelLoad] ";

        /// <summary>
        /// Initialize a new model load service.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        /// <param name="createEmbeddingClient">Embedding client factory.</param>
        /// <param name="createCompletionClient">Completion client factory.</param>
        public ModelLoadService(
            LoggingModule logging,
            Func<EmbeddingEndpoint, EmbeddingClientBase> createEmbeddingClient,
            Func<CompletionEndpoint, CompletionClientBase> createCompletionClient)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _CreateEmbeddingClient = createEmbeddingClient ?? throw new ArgumentNullException(nameof(createEmbeddingClient));
            _CreateCompletionClient = createCompletionClient ?? throw new ArgumentNullException(nameof(createCompletionClient));
        }

        /// <summary>
        /// Load or warm the model configured for an embedding endpoint.
        /// </summary>
        /// <param name="endpoint">Embedding endpoint.</param>
        /// <param name="request">Load request.</param>
        /// <param name="requestHistoryId">Optional request history ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model load response.</returns>
        public async Task<ModelLoadResponse> LoadEmbeddingEndpointAsync(
            EmbeddingEndpoint endpoint,
            ModelLoadRequest request,
            string? requestHistoryId,
            CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Stopwatch sw = Stopwatch.StartNew();
            ModelLoadResponse response = CreateModelLoadResponse(
                EndpointTypeEnum.Embedding,
                endpoint.Id,
                endpoint.TenantId,
                endpoint.ApiFormat,
                endpoint.Model,
                DateTime.UtcNow,
                requestHistoryId);
            response.Strategy = ResolveInitialModelLoadStrategy(endpoint.ApiFormat, request);

            using EmbeddingClientBase client = _CreateEmbeddingClient(endpoint);
            try
            {
                ModelLoadProviderResult providerResult = await client.LoadModelAsync(endpoint.Model, request, token).ConfigureAwait(false);
                ApplyProviderResult(response, providerResult);
                response.EmbeddingCalls = client.CallDetails.ToList();
            }
            catch (Exception ex)
            {
                ApplyProviderException(response, ex);
                response.EmbeddingCalls = client.CallDetails.ToList();
            }

            CompleteModelLoadResponse(response, sw);
            LogModelLoadResult(response);
            return response;
        }

        /// <summary>
        /// Load or warm the model configured for a completion endpoint.
        /// </summary>
        /// <param name="endpoint">Completion endpoint.</param>
        /// <param name="request">Load request.</param>
        /// <param name="requestHistoryId">Optional request history ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Model load response.</returns>
        public async Task<ModelLoadResponse> LoadCompletionEndpointAsync(
            CompletionEndpoint endpoint,
            ModelLoadRequest request,
            string? requestHistoryId,
            CancellationToken token = default)
        {
            if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
            if (request == null) throw new ArgumentNullException(nameof(request));

            Stopwatch sw = Stopwatch.StartNew();
            ModelLoadResponse response = CreateModelLoadResponse(
                EndpointTypeEnum.Completion,
                endpoint.Id,
                endpoint.TenantId,
                endpoint.ApiFormat,
                endpoint.Model,
                DateTime.UtcNow,
                requestHistoryId);
            response.Strategy = ResolveInitialModelLoadStrategy(endpoint.ApiFormat, request);

            using CompletionClientBase client = _CreateCompletionClient(endpoint);
            try
            {
                ModelLoadProviderResult providerResult = await client.LoadModelAsync(endpoint.Model, request, token).ConfigureAwait(false);
                ApplyProviderResult(response, providerResult);
                response.CompletionCalls = client.CallDetails.ToList();
            }
            catch (Exception ex)
            {
                ApplyProviderException(response, ex);
                response.CompletionCalls = client.CallDetails.ToList();
            }

            CompleteModelLoadResponse(response, sw);
            LogModelLoadResult(response);
            return response;
        }

        private static ModelLoadResponse CreateModelLoadResponse(
            EndpointTypeEnum endpointType,
            string endpointId,
            string tenantId,
            ApiFormatEnum apiFormat,
            string model,
            DateTime startedUtc,
            string? requestHistoryId)
        {
            ModelLoadResponse response = new ModelLoadResponse();
            response.EndpointType = endpointType;
            response.EndpointId = endpointId;
            response.TenantId = tenantId;
            response.ApiFormat = apiFormat;
            response.Model = model;
            response.StartedUtc = startedUtc;
            response.RequestHistoryId = requestHistoryId;
            return response;
        }

        private static ModelLoadStrategyEnum ResolveInitialModelLoadStrategy(ApiFormatEnum apiFormat, ModelLoadRequest request)
        {
            if (request.Strategy != ModelLoadStrategyEnum.Auto)
                return request.Strategy;

            return apiFormat == ApiFormatEnum.Ollama
                ? ModelLoadStrategyEnum.NativeProviderLoad
                : ModelLoadStrategyEnum.WarmRequest;
        }

        private static void ApplyProviderResult(ModelLoadResponse response, ModelLoadProviderResult providerResult)
        {
            response.Success = providerResult.Success;
            response.StatusCode = providerResult.StatusCode;
            response.Outcome = providerResult.Outcome;
            response.Strategy = providerResult.Strategy;
            response.Message = providerResult.Message;

            if (response.ApiFormat == ApiFormatEnum.vLLM
                && response.Success
                && response.Outcome == ModelLoadOutcomeEnum.Warmed)
            {
                response.Message = response.Message + " vLLM must already be serving the configured model.";
            }
        }

        private static void ApplyProviderException(ModelLoadResponse response, Exception ex)
        {
            response.Success = false;
            response.Outcome = ModelLoadOutcomeEnum.Failed;
            response.Strategy = response.Strategy == ModelLoadStrategyEnum.Auto ? ModelLoadStrategyEnum.WarmRequest : response.Strategy;
            response.Message = ex.Message;

            if (ex is ProviderConcurrencyLimitException)
                response.StatusCode = 429;
            else if (ex is ProviderOperationTimeoutException)
                response.StatusCode = 504;
            else
                response.StatusCode = 502;
        }

        private static void CompleteModelLoadResponse(ModelLoadResponse response, Stopwatch sw)
        {
            sw.Stop();
            response.CompletedUtc = DateTime.UtcNow;
            response.ResponseTimeMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2);
            if (string.IsNullOrWhiteSpace(response.Message))
                response.Message = response.Success ? "Model load request succeeded." : "Model load request failed.";
        }

        private void LogModelLoadResult(ModelLoadResponse response)
        {
            string message = _Header
                + "model load "
                + (response.Success ? "succeeded" : "failed")
                + ": endpoint "
                + response.EndpointId
                + ", tenant "
                + response.TenantId
                + ", provider "
                + response.ApiFormat
                + ", model "
                + response.Model
                + ", strategy "
                + response.Strategy
                + ", outcome "
                + response.Outcome
                + ", status "
                + response.StatusCode
                + ", duration "
                + response.ResponseTimeMs
                + "ms";

            if (response.Success)
                _Logging.Info(message);
            else
                _Logging.Warn(message);
        }
    }
}
