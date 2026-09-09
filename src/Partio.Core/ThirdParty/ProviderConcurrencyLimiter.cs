namespace Partio.Core.ThirdParty
{
    using System.Collections.Concurrent;
    using Partio.Core.Exceptions;

    /// <summary>
    /// Shared in-memory limiter for concurrent upstream provider requests per endpoint.
    /// When the concurrency limit is reached, callers may wait in a bounded FIFO queue for a slot to
    /// free up rather than being rejected immediately. Thread-safe: per-endpoint state is guarded by a
    /// private lock and slots are transferred to waiting callers in arrival order.
    /// </summary>
    internal static class ProviderConcurrencyLimiter
    {
        private static readonly ConcurrentDictionary<string, CounterState> _States = new ConcurrentDictionary<string, CounterState>(StringComparer.Ordinal);

        /// <summary>
        /// Acquire a concurrency slot for the supplied endpoint key, waiting in a bounded FIFO queue if all
        /// slots are in use. If a slot is available it is granted immediately. If all slots are busy and the
        /// wait queue has room (fewer than <paramref name="maxQueueDepth"/> waiters), the caller parks until a
        /// slot frees; the wait is bounded by <paramref name="token"/>. If the queue is full the call throws.
        /// </summary>
        /// <param name="concurrencyKey">Endpoint concurrency key.</param>
        /// <param name="maxConcurrentRequests">Maximum concurrent slots (clamped to <c>&gt;= 1</c>).</param>
        /// <param name="maxQueueDepth">Maximum number of waiters allowed (clamped to <c>&gt;= 0</c>). <c>0</c> means reject immediately when full.</param>
        /// <param name="token">Cancellation/timeout token bounding the wait.</param>
        /// <returns>A disposable lease; disposing it releases the slot (or transfers it to the next waiter).</returns>
        /// <exception cref="ProviderConcurrencyLimitException">Thrown when all slots are busy and the wait queue is full.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="token"/> is cancelled while queued.</exception>
        public static async Task<IDisposable> AcquireAsync(string concurrencyKey, int maxConcurrentRequests, int maxQueueDepth, CancellationToken token)
        {
            string effectiveKey = string.IsNullOrWhiteSpace(concurrencyKey) ? "provider" : concurrencyKey;
            int effectiveMax = maxConcurrentRequests < 1 ? 1 : maxConcurrentRequests;
            int effectiveQueueDepth = maxQueueDepth < 0 ? 0 : maxQueueDepth;
            CounterState state = _States.GetOrAdd(effectiveKey, static _ => new CounterState());

            TaskCompletionSource<bool>? waiter = null;

            lock (state.SyncRoot)
            {
                if (state.InFlight < effectiveMax)
                {
                    state.InFlight++;
                    return new Lease(state);
                }

                // Drop waiters that already cancelled (e.g. timed out) so they do not count against the queue depth.
                PruneDeadWaiters(state);

                if (state.Waiters.Count >= effectiveQueueDepth)
                {
                    throw new ProviderConcurrencyLimitException(
                        effectiveKey,
                        effectiveMax,
                        effectiveQueueDepth,
                        "Endpoint '" + effectiveKey + "' has reached its max concurrent request limit of " + effectiveMax + " and queue depth of " + effectiveQueueDepth + ".");
                }

                waiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                state.Waiters.Enqueue(waiter);
            }

            using (token.Register(static s => ((TaskCompletionSource<bool>)s!).TrySetCanceled(), waiter))
            {
                await waiter.Task.ConfigureAwait(false);
            }

            // A releasing lease transferred a slot to this waiter; InFlight already accounts for it.
            return new Lease(state);
        }

        /// <summary>
        /// Remove waiters whose task has already completed (cancelled while queued), preserving FIFO order of the
        /// remaining live waiters. Must be called while holding <see cref="CounterState.SyncRoot"/>.
        /// </summary>
        private static void PruneDeadWaiters(CounterState state)
        {
            int count = state.Waiters.Count;
            for (int i = 0; i < count; i++)
            {
                TaskCompletionSource<bool> waiter = state.Waiters.Dequeue();
                if (!waiter.Task.IsCompleted) state.Waiters.Enqueue(waiter);
            }
        }

        private sealed class CounterState
        {
            public object SyncRoot { get; } = new object();

            public int InFlight { get; set; }

            public Queue<TaskCompletionSource<bool>> Waiters { get; } = new Queue<TaskCompletionSource<bool>>();
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
                    // Hand the freed slot to the next live waiter; skip any that cancelled while queued.
                    while (_State.Waiters.Count > 0)
                    {
                        TaskCompletionSource<bool> next = _State.Waiters.Dequeue();
                        if (next.TrySetResult(true)) return; // slot transferred, InFlight unchanged
                    }

                    if (_State.InFlight > 0) _State.InFlight--; // no waiter took it, release the slot
                }
            }
        }
    }
}
