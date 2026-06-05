namespace Partio.Server.Services
{
    using System.Collections.Concurrent;
    using System.Net.Http;
    using Partio.Core.Enums;
    using SyslogLogging;

    /// <summary>
    /// Coordinates health probes across endpoint types so one target URL is checked once per interval.
    /// </summary>
    public sealed class SharedHealthCheckCoordinator
    {
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[HealthCheck] ";
        private readonly ConcurrentDictionary<string, string> _SubscriptionMonitorKeys = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, SharedHealthCheckSubscription>> _MonitorSubscriptions = new ConcurrentDictionary<string, ConcurrentDictionary<string, SharedHealthCheckSubscription>>();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _CancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly ConcurrentDictionary<string, Task> _RunningTasks = new ConcurrentDictionary<string, Task>();
        private readonly HttpClient _HttpClient = new HttpClient();

        /// <summary>
        /// Initialize a new shared health check coordinator.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        public SharedHealthCheckCoordinator(LoggingModule logging)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Register an endpoint state callback against a shared health target.
        /// </summary>
        /// <param name="subscription">Health check subscription.</param>
        public void Register(SharedHealthCheckSubscription subscription)
        {
            if (subscription == null) throw new ArgumentNullException(nameof(subscription));
            if (string.IsNullOrWhiteSpace(subscription.SubscriptionId))
                throw new ArgumentException("SubscriptionId is required.", nameof(subscription));
            if (string.IsNullOrWhiteSpace(subscription.MonitorKey))
                throw new ArgumentException("MonitorKey is required.", nameof(subscription));

            Unregister(subscription.SubscriptionId);

            _SubscriptionMonitorKeys[subscription.SubscriptionId] = subscription.MonitorKey;

            ConcurrentDictionary<string, SharedHealthCheckSubscription> subscriptions = _MonitorSubscriptions.GetOrAdd(
                subscription.MonitorKey,
                _ => new ConcurrentDictionary<string, SharedHealthCheckSubscription>());
            subscriptions[subscription.SubscriptionId] = subscription;

            CancellationTokenSource cts = new CancellationTokenSource();
            if (_CancellationTokens.TryAdd(subscription.MonitorKey, cts))
            {
                Task loopTask = Task.Run(() => HealthCheckLoopAsync(subscription.MonitorKey, cts.Token));
                _RunningTasks[subscription.MonitorKey] = loopTask;
                _Logging.Info(_Header + "started shared health target " + DescribeMonitorKey(subscription.MonitorKey));
            }
            else
            {
                cts.Dispose();
            }

            _Logging.Info(_Header + "attached " + subscription.Description + " to shared health target " + DescribeMonitorKey(subscription.MonitorKey));
        }

        /// <summary>
        /// Unregister an endpoint from its shared health target.
        /// </summary>
        /// <param name="subscriptionId">Subscription identifier.</param>
        public void Unregister(string subscriptionId)
        {
            if (string.IsNullOrWhiteSpace(subscriptionId)) return;

            if (!_SubscriptionMonitorKeys.TryRemove(subscriptionId, out string? monitorKey))
                return;

            if (_MonitorSubscriptions.TryGetValue(monitorKey, out ConcurrentDictionary<string, SharedHealthCheckSubscription>? subscriptions))
            {
                subscriptions.TryRemove(subscriptionId, out _);
                if (!subscriptions.IsEmpty)
                    return;

                _MonitorSubscriptions.TryRemove(monitorKey, out _);
            }

            if (_CancellationTokens.TryRemove(monitorKey, out CancellationTokenSource? cts))
            {
                cts.Cancel();
                cts.Dispose();
            }

            if (_RunningTasks.TryRemove(monitorKey, out Task? task))
            {
                _ = task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        _Logging.Warn(_Header + "loop for " + DescribeMonitorKey(monitorKey) + " faulted: " + t.Exception?.Message);
                }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        /// <summary>
        /// Stop every shared health loop.
        /// </summary>
        public async Task StopAsync()
        {
            foreach (string key in _CancellationTokens.Keys)
            {
                if (_CancellationTokens.TryGetValue(key, out CancellationTokenSource? cts))
                    cts.Cancel();
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
                    }
                    catch (Exception ex)
                    {
                        _Logging.Warn(_Header + "error during shutdown for " + DescribeMonitorKey(key) + ": " + ex.Message);
                    }
                }
            }

            foreach (string key in _CancellationTokens.Keys)
            {
                if (_CancellationTokens.TryRemove(key, out CancellationTokenSource? cts))
                    cts.Dispose();
            }

            _RunningTasks.Clear();
            _SubscriptionMonitorKeys.Clear();
            _MonitorSubscriptions.Clear();
        }

        /// <summary>
        /// Build a normalized health target key.
        /// </summary>
        public static string BuildMonitorKey(
            string url,
            HealthCheckMethodEnum method,
            int expectedStatusCode,
            bool useAuth,
            ApiFormatEnum apiFormat)
        {
            string normalizedUrl = NormalizeHealthCheckUrl(url);
            string methodText = method == HealthCheckMethodEnum.HEAD ? "HEAD" : "GET";
            string auth = useAuth ? "auth:" + apiFormat : "anon";
            return methodText + "|" + expectedStatusCode + "|" + auth + "|" + normalizedUrl;
        }

        /// <summary>
        /// Describe the URL portion of a monitor key for logs.
        /// </summary>
        public static string DescribeMonitorKey(string monitorKey)
        {
            int index = monitorKey.LastIndexOf('|');
            return index >= 0 ? monitorKey.Substring(index + 1) : monitorKey;
        }

        private async Task HealthCheckLoopAsync(string monitorKey, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                List<SharedHealthCheckSubscription> subscriptions = GetSubscriptions(monitorKey);
                if (subscriptions.Count == 0)
                    break;

                int intervalMs = Math.Max(1, subscriptions.Min(subscription => subscription.IntervalMs));

                try
                {
                    await Task.Delay(intervalMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                subscriptions = GetSubscriptions(monitorKey);
                if (subscriptions.Count == 0)
                    break;

                SharedHealthCheckSubscription probeSubscription = subscriptions
                    .OrderByDescending(subscription => subscription.TimeoutMs)
                    .ThenBy(subscription => subscription.SubscriptionId)
                    .First();

                bool success = false;
                string? errorMessage = null;

                try
                {
                    success = await PerformCheckAsync(probeSubscription, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    success = false;
                    errorMessage = "health check timed out after " + probeSubscription.TimeoutMs + "ms";
                    _Logging.Debug(_Header + "timeout for shared health target " + DescribeMonitorKey(monitorKey) + ": " + errorMessage);
                }
                catch (Exception ex)
                {
                    success = false;
                    errorMessage = ex.Message;
                    _Logging.Debug(_Header + "error for shared health target " + DescribeMonitorKey(monitorKey) + ": " + errorMessage);
                }

                foreach (SharedHealthCheckSubscription subscription in subscriptions)
                {
                    try
                    {
                        subscription.UpdateState(success, errorMessage);
                    }
                    catch (Exception ex)
                    {
                        _Logging.Warn(_Header + "failed to update health state for " + subscription.Description + ": " + ex.Message);
                    }
                }
            }
        }

        private List<SharedHealthCheckSubscription> GetSubscriptions(string monitorKey)
        {
            if (!_MonitorSubscriptions.TryGetValue(monitorKey, out ConcurrentDictionary<string, SharedHealthCheckSubscription>? subscriptions))
                return new List<SharedHealthCheckSubscription>();
            return subscriptions.Values.ToList();
        }

        private async Task<bool> PerformCheckAsync(SharedHealthCheckSubscription subscription, CancellationToken token)
        {
            HttpMethod method = subscription.Method == HealthCheckMethodEnum.HEAD
                ? HttpMethod.Head
                : HttpMethod.Get;

            _Logging.Debug(_Header + "sending shared " + method + " " + subscription.Url + " for " + subscription.Description);

            HttpRequestMessage request = new HttpRequestMessage(method, subscription.Url);

            if (subscription.UseAuth && !string.IsNullOrEmpty(subscription.ApiKey))
            {
                if (subscription.ApiFormat == ApiFormatEnum.Gemini)
                    request.Headers.Add("x-goog-api-key", subscription.ApiKey);
                else
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", subscription.ApiKey);
            }

            using (CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeoutCts.CancelAfter(subscription.TimeoutMs);

                using HttpResponseMessage response = await _HttpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
                int statusCode = (int)response.StatusCode;
                bool success = statusCode == subscription.ExpectedStatusCode;

                _Logging.Debug(_Header + "received " + statusCode + " from " + subscription.Url + " for shared target " + DescribeMonitorKey(subscription.MonitorKey) + ", success: " + success);

                return success;
            }
        }

        private static string NormalizeHealthCheckUrl(string url)
        {
            string trimmed = (url ?? string.Empty).Trim().TrimEnd('/');
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
            {
                UriBuilder builder = new UriBuilder(uri);
                builder.Host = builder.Host.ToLowerInvariant();
                return builder.Uri.ToString().TrimEnd('/');
            }

            return trimmed.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Describes one endpoint state subscription to a shared health check target.
    /// </summary>
    public sealed class SharedHealthCheckSubscription
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string MonitorKey { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public HealthCheckMethodEnum Method { get; set; } = HealthCheckMethodEnum.GET;
        public int ExpectedStatusCode { get; set; } = 200;
        public int IntervalMs { get; set; } = 5000;
        public int TimeoutMs { get; set; } = 2000;
        public bool UseAuth { get; set; }
        public ApiFormatEnum ApiFormat { get; set; } = ApiFormatEnum.Ollama;
        public string? ApiKey { get; set; }
        public string Description { get; set; } = "endpoint";
        public Action<bool, string?> UpdateState { get; set; } = (_, _) => { };
    }
}
