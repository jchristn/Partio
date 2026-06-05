namespace Partio.Server.Services
{
    using System.Collections.Concurrent;
    using Partio.Core.Database;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using Partio.Core.Tokenization;
    using SyslogLogging;

    /// <summary>
    /// Background service that performs periodic health checks on embedding endpoints.
    /// Health state is tracked entirely in RAM and not persisted.
    /// </summary>
    public class EmbeddingHealthCheckService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly TokenizationProfileResolver? _TokenizationResolver;
        private readonly SharedHealthCheckCoordinator _Coordinator;
        private readonly string _Header = "[HealthCheck] ";
        private readonly ConcurrentDictionary<string, EndpointHealthState> _States = new ConcurrentDictionary<string, EndpointHealthState>();
        private static readonly TimeSpan _HistoryRetention = TimeSpan.FromHours(24);

        /// <summary>
        /// Initialize a new EmbeddingHealthCheckService.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="tokenizationResolver">Optional tokenization resolver for capability cache invalidation on health transitions.</param>
        /// <param name="coordinator">Shared health check coordinator.</param>
        public EmbeddingHealthCheckService(
            DatabaseDriverBase database,
            LoggingModule logging,
            TokenizationProfileResolver? tokenizationResolver = null,
            SharedHealthCheckCoordinator? coordinator = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _TokenizationResolver = tokenizationResolver;
            _Coordinator = coordinator ?? new SharedHealthCheckCoordinator(logging);
        }

        /// <summary>
        /// Start health checks for all enabled and active endpoints.
        /// </summary>
        public async Task StartAsync()
        {
            _Logging.Info(_Header + "starting health check service");

            // Load all endpoints from all tenants
            // We enumerate with a large page size to get all endpoints
            EnumerationRequest enumReq = new EnumerationRequest();
            enumReq.MaxResults = 1000;

            // Get all tenants first
            EnumerationResult<TenantMetadata> tenants = await _Database.Tenant.EnumerateAsync(enumReq).ConfigureAwait(false);

            int started = 0;
            foreach (TenantMetadata tenant in tenants.Data)
            {
                EnumerationResult<EmbeddingEndpoint> endpoints = await _Database.EmbeddingEndpoint.EnumerateAsync(tenant.Id, enumReq).ConfigureAwait(false);
                foreach (EmbeddingEndpoint ep in endpoints.Data)
                {
                    if (ep.HealthCheckEnabled && ep.Active)
                    {
                        StartLoop(ep);
                        started++;
                    }
                }
            }

            _Logging.Info(_Header + "health check service started, monitoring " + started + " endpoints");
        }

        /// <summary>
        /// Stop all health check loops.
        /// </summary>
        public async Task StopAsync()
        {
            _Logging.Info(_Header + "stopping health check service");

            foreach (string endpointId in _States.Keys)
                _Coordinator.Unregister(BuildSubscriptionId(endpointId));
            _States.Clear();

            _Logging.Info(_Header + "health check service stopped");
            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Called when a new endpoint is created. Starts health check loop if enabled and active.
        /// </summary>
        public void OnEndpointCreated(EmbeddingEndpoint endpoint)
        {
            if (endpoint == null) return;
            if (endpoint.HealthCheckEnabled && endpoint.Active)
            {
                StartLoop(endpoint);
            }
        }

        /// <summary>
        /// Called when an endpoint is updated. Stops existing loop and restarts with new config if still enabled and active.
        /// </summary>
        public void OnEndpointUpdated(EmbeddingEndpoint endpoint)
        {
            if (endpoint == null) return;

            StopLoop(endpoint.Id);

            if (endpoint.HealthCheckEnabled && endpoint.Active)
            {
                StartLoop(endpoint);
            }
        }

        /// <summary>
        /// Called when an endpoint is deleted. Stops loop and removes state.
        /// </summary>
        public void OnEndpointDeleted(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            StopLoop(id);
            _States.TryRemove(id, out _);
        }

        /// <summary>
        /// Get the health state for a specific endpoint.
        /// Returns null if no state exists (health check not enabled).
        /// </summary>
        public EndpointHealthState? GetHealthState(string endpointId)
        {
            if (_States.TryGetValue(endpointId, out EndpointHealthState? state))
                return state;
            return null;
        }

        /// <summary>
        /// Get health states for all monitored endpoints, optionally filtered by tenant.
        /// </summary>
        public List<EndpointHealthState> GetAllHealthStates(string? tenantId = null)
        {
            List<EndpointHealthState> results = new List<EndpointHealthState>();
            foreach (EndpointHealthState state in _States.Values)
            {
                if (string.IsNullOrEmpty(tenantId) || state.TenantId == tenantId)
                    results.Add(state);
            }
            return results;
        }

        /// <summary>
        /// Returns true if the endpoint is healthy or if no health state exists (health check not enabled).
        /// </summary>
        public bool IsHealthy(string endpointId)
        {
            if (_States.TryGetValue(endpointId, out EndpointHealthState? state))
            {
                lock (state.Lock)
                {
                    return state.IsHealthy;
                }
            }
            return true; // No state = assumed healthy (health check not enabled)
        }

        private void StartLoop(EmbeddingEndpoint endpoint)
        {
            string monitorKey = BuildMonitorKey(endpoint);

            EndpointHealthState state = new EndpointHealthState();
            state.EndpointId = endpoint.Id;
            state.EndpointName = endpoint.Model;
            state.TenantId = endpoint.TenantId;
            state.IsHealthy = false; // Starts unhealthy; must prove health
            state.FirstCheckUtc = DateTime.UtcNow;
            state.LastStateChangeUtc = DateTime.UtcNow;

            _States[endpoint.Id] = state;

            _Coordinator.Register(new SharedHealthCheckSubscription
            {
                SubscriptionId = BuildSubscriptionId(endpoint.Id),
                MonitorKey = monitorKey,
                Url = ResolveHealthCheckUrl(endpoint),
                Method = endpoint.HealthCheckMethod,
                ExpectedStatusCode = endpoint.HealthCheckExpectedStatusCode,
                IntervalMs = Math.Max(1, endpoint.HealthCheckIntervalMs),
                TimeoutMs = Math.Max(1, endpoint.HealthCheckTimeoutMs),
                UseAuth = endpoint.HealthCheckUseAuth,
                ApiFormat = endpoint.ApiFormat,
                ApiKey = endpoint.ApiKey,
                Description = "embedding endpoint " + endpoint.Id + " (" + endpoint.Model + ")",
                UpdateState = (success, errorMessage) =>
                {
                    if (_States.TryGetValue(endpoint.Id, out EndpointHealthState? currentState))
                        UpdateState(currentState, success, errorMessage, endpoint);
                }
            });
        }

        private void StopLoop(string endpointId)
        {
            _States.TryRemove(endpointId, out _);
            _Coordinator.Unregister(BuildSubscriptionId(endpointId));
        }

        private static string BuildMonitorKey(EmbeddingEndpoint endpoint)
        {
            return SharedHealthCheckCoordinator.BuildMonitorKey(
                ResolveHealthCheckUrl(endpoint),
                endpoint.HealthCheckMethod,
                endpoint.HealthCheckExpectedStatusCode,
                endpoint.HealthCheckUseAuth,
                endpoint.ApiFormat);
        }

        private static string ResolveHealthCheckUrl(EmbeddingEndpoint endpoint)
        {
            return !string.IsNullOrEmpty(endpoint.HealthCheckUrl)
                ? endpoint.HealthCheckUrl
                : endpoint.Endpoint;
        }

        private static string BuildSubscriptionId(string endpointId)
        {
            return "embedding:" + endpointId;
        }

        private void UpdateState(EndpointHealthState state, bool success, string? errorMessage, EmbeddingEndpoint endpoint)
        {
            DateTime now = DateTime.UtcNow;

            // Add to check history
            HealthCheckRecord record = new HealthCheckRecord();
            record.TimestampUtc = now;
            record.Success = success;

            lock (state.HistoryLock)
            {
                state.CheckHistory.Add(record);

                // Prune records older than 24 hours
                DateTime cutoff = now - _HistoryRetention;
                state.CheckHistory.RemoveAll(r => r.TimestampUtc < cutoff);
            }

            lock (state.Lock)
            {
                state.LastCheckUtc = now;

                if (success)
                {
                    state.ConsecutiveSuccesses++;
                    state.ConsecutiveFailures = 0;
                    state.LastError = null;

                    if (!state.IsHealthy && state.ConsecutiveSuccesses >= endpoint.HealthyThreshold)
                    {
                        // Transition: unhealthy -> healthy
                        if (state.LastStateChangeUtc.HasValue)
                        {
                            long downtimeMs = (long)(now - state.LastStateChangeUtc.Value).TotalMilliseconds;
                            if (downtimeMs > 0) state.TotalDowntimeMs += downtimeMs;
                        }

                        state.IsHealthy = true;
                        state.LastHealthyUtc = now;
                        state.LastStateChangeUtc = now;
                        _TokenizationResolver?.Invalidate(state.EndpointId);

                        _Logging.Info(_Header + "endpoint " + state.EndpointId + " (" + state.EndpointName + ") transitioned to HEALTHY");
                    }
                }
                else
                {
                    state.ConsecutiveFailures++;
                    state.ConsecutiveSuccesses = 0;
                    state.LastError = errorMessage;

                    if (state.IsHealthy && state.ConsecutiveFailures >= endpoint.UnhealthyThreshold)
                    {
                        // Transition: healthy -> unhealthy
                        if (state.LastStateChangeUtc.HasValue)
                        {
                            long uptimeMs = (long)(now - state.LastStateChangeUtc.Value).TotalMilliseconds;
                            if (uptimeMs > 0) state.TotalUptimeMs += uptimeMs;
                        }

                        state.IsHealthy = false;
                        state.LastUnhealthyUtc = now;
                        state.LastStateChangeUtc = now;
                        _TokenizationResolver?.Invalidate(state.EndpointId);

                        _Logging.Warn(_Header + "endpoint " + state.EndpointId + " (" + state.EndpointName + ") transitioned to UNHEALTHY: " + (errorMessage ?? "check failed"));
                    }
                }
            }
        }
    }
}
