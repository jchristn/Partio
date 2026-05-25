namespace Partio.Core.ThirdParty
{
    using System.Collections.Concurrent;
    using Partio.Core.Exceptions;

    /// <summary>
    /// Shared in-memory limiter for concurrent upstream provider requests per endpoint.
    /// </summary>
    internal static class ProviderConcurrencyLimiter
    {
        private static readonly ConcurrentDictionary<string, CounterState> _States = new ConcurrentDictionary<string, CounterState>(StringComparer.Ordinal);

        /// <summary>
        /// Acquire a concurrency slot for the supplied endpoint key.
        /// </summary>
        public static IDisposable Acquire(string concurrencyKey, int maxConcurrentRequests)
        {
            string effectiveKey = string.IsNullOrWhiteSpace(concurrencyKey) ? "provider" : concurrencyKey;
            int effectiveMax = maxConcurrentRequests < 1 ? 1 : maxConcurrentRequests;
            CounterState state = _States.GetOrAdd(effectiveKey, static _ => new CounterState());

            lock (state.SyncRoot)
            {
                if (state.InFlight >= effectiveMax)
                {
                    throw new ProviderConcurrencyLimitException(
                        effectiveKey,
                        effectiveMax,
                        "Endpoint '" + effectiveKey + "' has reached its max concurrent request limit of " + effectiveMax + ".");
                }

                state.InFlight++;
            }

            return new Lease(state);
        }

        private sealed class CounterState
        {
            public object SyncRoot { get; } = new object();

            public int InFlight { get; set; }
        }

        private sealed class Lease : IDisposable
        {
            private readonly CounterState _State;
            private bool _Disposed;

            public Lease(CounterState state)
            {
                _State = state;
            }

            public void Dispose()
            {
                if (_Disposed) return;
                _Disposed = true;

                lock (_State.SyncRoot)
                {
                    if (_State.InFlight > 0)
                        _State.InFlight--;
                }
            }
        }
    }
}
