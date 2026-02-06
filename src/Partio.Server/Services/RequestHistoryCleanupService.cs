namespace Partio.Server.Services
{
    using Partio.Core.Database;
    using Partio.Core.Settings;
    using SyslogLogging;

    /// <summary>
    /// Background service that periodically cleans up expired request history entries and their filesystem files.
    /// </summary>
    public class RequestHistoryCleanupService
    {
        private readonly ServerSettings _Settings;
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[RequestHistoryCleanup] ";
        private CancellationTokenSource? _Cts;
        private Task? _BackgroundTask;

        /// <summary>
        /// Initialize a new RequestHistoryCleanupService.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="database">Database driver.</param>
        /// <param name="logging">Logging module.</param>
        public RequestHistoryCleanupService(ServerSettings settings, DatabaseDriverBase database, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        /// <summary>
        /// Start the background cleanup task.
        /// </summary>
        public void Start()
        {
            _Cts = new CancellationTokenSource();
            _BackgroundTask = Task.Run(() => CleanupLoopAsync(_Cts.Token));
            _Logging.Info(_Header + "cleanup service started, interval " + _Settings.RequestHistory.CleanupIntervalMinutes + " minutes");
        }

        /// <summary>
        /// Stop the background cleanup task.
        /// </summary>
        public async Task StopAsync()
        {
            if (_Cts != null)
            {
                _Cts.Cancel();
                if (_BackgroundTask != null)
                {
                    try
                    {
                        await _BackgroundTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                }
                _Cts.Dispose();
            }
            _Logging.Info(_Header + "cleanup service stopped");
        }

        private async Task CleanupLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(_Settings.RequestHistory.CleanupIntervalMinutes), token).ConfigureAwait(false);

                    DateTime cutoff = DateTime.UtcNow.AddDays(-_Settings.RequestHistory.RetentionDays);
                    _Logging.Info(_Header + "cleaning up entries older than " + cutoff.ToString("o"));

                    // Get expired object keys for filesystem cleanup
                    List<string> objectKeys = await _Database.RequestHistory.GetExpiredObjectKeysAsync(cutoff, token).ConfigureAwait(false);

                    // Delete filesystem files
                    int filesDeleted = 0;
                    foreach (string key in objectKeys)
                    {
                        string filePath = Path.Combine(_Settings.RequestHistory.Directory, key + ".json");
                        if (File.Exists(filePath))
                        {
                            try
                            {
                                File.Delete(filePath);
                                filesDeleted++;
                            }
                            catch (Exception ex)
                            {
                                _Logging.Warn(_Header + "failed to delete file " + filePath + ": " + ex.Message);
                            }
                        }
                    }

                    // Delete expired DB entries
                    await _Database.RequestHistory.DeleteExpiredAsync(cutoff, token).ConfigureAwait(false);

                    _Logging.Info(_Header + "cleanup complete, " + filesDeleted + " files deleted, " + objectKeys.Count + " entries cleaned");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "cleanup error: " + ex.Message);
                }
            }
        }
    }
}
