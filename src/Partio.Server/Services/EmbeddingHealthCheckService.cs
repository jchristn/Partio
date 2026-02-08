namespace Partio.Server.Services
{
    using System.Collections.Concurrent;
    using System.Net.Http;
    using Partio.Core.Database;
    using Partio.Core.Enums;
    using Partio.Core.Models;
    using SyslogLogging;

    /// <summary>
    /// Background service that performs periodic health checks on embedding endpoints.
    /// Health state is tracked entirely in RAM and not persisted.
    /// </summary>
    public class EmbeddingHealthCheckService
    {
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[HealthCheck] ";
        private readonly ConcurrentDictionary<string, EndpointHealthState> _States = new ConcurrentDictionary<string, EndpointHealthState>();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _CancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly ConcurrentDictionary<string, Task> _RunningTasks = new ConcurrentDictionary<string, Task>();
        private readonly HttpClient _HttpClient = new HttpClient();
        private static readonly TimeSpan HistoryRetention = TimeSpan.FromHours(24);

        /// <summary>
        /// Initialize a new EmbeddingHealthCheckService.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public EmbeddingHealthCheckService(DatabaseDriverBase database, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
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

            foreach (string key in _CancellationTokens.Keys)
            {
                if (_CancellationTokens.TryGetValue(key, out CancellationTokenSource? cts))
                {
                    cts.Cancel();
                }
            }

            foreach (string key in _RunningTasks.Keys)
            {
                if (_RunningTasks.TryGetValue(key, out Task? task))
                {
                    try
                    {
                        await task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                    catch (Exception ex)
                    {
                        _Logging.Warn(_Header + "error during shutdown for " + key + ": " + ex.Message);
                    }
                }
            }

            foreach (string key in _CancellationTokens.Keys)
            {
                if (_CancellationTokens.TryRemove(key, out CancellationTokenSource? cts))
                {
                    cts.Dispose();
                }
            }

            _RunningTasks.Clear();
            _States.Clear();

            _Logging.Info(_Header + "health check service stopped");
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
            EndpointHealthState state = new EndpointHealthState();
            state.EndpointId = endpoint.Id;
            state.EndpointName = endpoint.Model;
            state.TenantId = endpoint.TenantId;
            state.IsHealthy = false; // Starts unhealthy; must prove health
            state.FirstCheckUtc = DateTime.UtcNow;
            state.LastStateChangeUtc = DateTime.UtcNow;

            _States[endpoint.Id] = state;

            CancellationTokenSource cts = new CancellationTokenSource();
            _CancellationTokens[endpoint.Id] = cts;

            Task loopTask = Task.Run(() => HealthCheckLoopAsync(endpoint, state, cts.Token));
            _RunningTasks[endpoint.Id] = loopTask;

            _Logging.Info(_Header + "started monitoring endpoint " + endpoint.Id + " (" + endpoint.Model + ") every " + endpoint.HealthCheckIntervalMs + "ms");
        }

        private void StopLoop(string endpointId)
        {
            if (_CancellationTokens.TryRemove(endpointId, out CancellationTokenSource? cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            if (_RunningTasks.TryRemove(endpointId, out Task? task))
            {
                // Fire-and-forget cleanup; the loop should exit on cancellation
                _ = task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        _Logging.Warn(_Header + "loop for " + endpointId + " faulted: " + t.Exception?.Message);
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        private async Task HealthCheckLoopAsync(EmbeddingEndpoint endpoint, EndpointHealthState state, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(endpoint.HealthCheckIntervalMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                bool success = false;
                string? errorMessage = null;

                try
                {
                    success = await PerformCheckAsync(endpoint, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    success = false;
                    errorMessage = ex.Message;
                }

                UpdateState(state, success, errorMessage, endpoint);
            }
        }

        private async Task<bool> PerformCheckAsync(EmbeddingEndpoint endpoint, CancellationToken token)
        {
            string url = !string.IsNullOrEmpty(endpoint.HealthCheckUrl)
                ? endpoint.HealthCheckUrl
                : endpoint.Endpoint;

            HttpMethod method = endpoint.HealthCheckMethod == HealthCheckMethodEnum.HEAD
                ? HttpMethod.Head
                : HttpMethod.Get;

            HttpRequestMessage request = new HttpRequestMessage(method, url);

            if (endpoint.HealthCheckUseAuth && !string.IsNullOrEmpty(endpoint.ApiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", endpoint.ApiKey);
            }

            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeoutCts.CancelAfter(endpoint.HealthCheckTimeoutMs);

            HttpResponseMessage response = await _HttpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            return (int)response.StatusCode == endpoint.HealthCheckExpectedStatusCode;
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
                DateTime cutoff = now - HistoryRetention;
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

                        _Logging.Warn(_Header + "endpoint " + state.EndpointId + " (" + state.EndpointName + ") transitioned to UNHEALTHY: " + (errorMessage ?? "check failed"));
                    }
                }
            }
        }
    }
}
