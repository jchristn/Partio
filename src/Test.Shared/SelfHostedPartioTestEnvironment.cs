namespace Test.Shared
{
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text.Json;
    using Partio.Core.Enums;

    /// <summary>
    /// Starts the services required by integration tests: an isolated Partio server process backed by a
    /// configurable database, and (by default) an in-process Ollama-compatible upstream stub. When the
    /// caller supplies real embedding/inference endpoints and/or an external database via
    /// <see cref="TestEnvironmentOptions"/>, the stub is not started and the server is pointed at the
    /// supplied backends instead.
    /// </summary>
    public sealed class SelfHostedPartioTestEnvironment : IDisposable, IAsyncDisposable
    {
        private readonly SlowOllamaCompatibleServer? _Ollama;
        private readonly Process _ServerProcess;
        private readonly List<string> _ServerOutput = new List<string>();
        private readonly object _OutputLock = new object();
        private bool _Disposed;

        private SelfHostedPartioTestEnvironment(
            SlowOllamaCompatibleServer? ollama,
            Process serverProcess,
            string workingDirectory,
            string endpoint,
            string serverAssemblyPath,
            string embeddingUpstreamEndpoint,
            string inferenceUpstreamEndpoint,
            bool usesRealEndpoints)
        {
            _Ollama = ollama;
            _ServerProcess = serverProcess;
            WorkingDirectory = workingDirectory;
            Endpoint = endpoint;
            ServerAssemblyPath = serverAssemblyPath;
            EmbeddingUpstreamEndpoint = embeddingUpstreamEndpoint;
            InferenceUpstreamEndpoint = inferenceUpstreamEndpoint;
            UpstreamEndpoint = inferenceUpstreamEndpoint;
            UsesRealEndpoints = usesRealEndpoints;
        }

        public string Endpoint { get; }

        public string UpstreamEndpoint { get; }

        public string EmbeddingUpstreamEndpoint { get; }

        public string InferenceUpstreamEndpoint { get; }

        /// <summary>True when the server was pointed at real upstream endpoints (no in-process stub).</summary>
        public bool UsesRealEndpoints { get; }

        public int UpstreamTagsRequestCount => _Ollama?.TagsRequestCount ?? 0;

        public string AdminKey { get; } = "partioadmin";

        public string TestToken { get; } = "default";

        public string WorkingDirectory { get; }

        public string ServerAssemblyPath { get; }

        public int GetUpstreamRawPathRequestCount(string rawPath)
        {
            return _Ollama?.GetRawPathRequestCount(rawPath) ?? 0;
        }

        public async Task WaitForUpstreamRawPathRequestCountAsync(string rawPath, int minCount, int timeoutMs = 5000)
        {
            if (_Ollama == null) return;
            await _Ollama.WaitForRawPathRequestCountAsync(rawPath, minCount, timeoutMs).ConfigureAwait(false);
        }

        /// <summary>
        /// Start a default self-hosted environment: temp SQLite plus an in-process Ollama-compatible stub.
        /// </summary>
        public static Task<SelfHostedPartioTestEnvironment> StartAsync(CancellationToken token = default)
        {
            return StartAsync(new TestEnvironmentOptions(), token);
        }

        /// <summary>
        /// Start a self-hosted environment configured by <paramref name="options"/> — an arbitrary database
        /// backend and, optionally, real upstream embedding/inference endpoints.
        /// </summary>
        /// <param name="options">Database and upstream-endpoint configuration.</param>
        /// <param name="token">Cancellation token.</param>
        public static async Task<SelfHostedPartioTestEnvironment> StartAsync(TestEnvironmentOptions options, CancellationToken token = default)
        {
            if (options == null) options = new TestEnvironmentOptions();

            SlowOllamaCompatibleServer? ollama = options.UsesRealEndpoints ? null : new SlowOllamaCompatibleServer();
            string stubUrl = ollama?.BaseUrl ?? "http://127.0.0.1:11434";
            string embeddingUrl = !string.IsNullOrWhiteSpace(options.EmbeddingEndpoint) ? options.EmbeddingEndpoint! : stubUrl;
            string inferenceUrl = !string.IsNullOrWhiteSpace(options.InferenceEndpoint) ? options.InferenceEndpoint! : stubUrl;

            string workingDirectory = Path.Combine(Path.GetTempPath(), "partio-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            int partioPort = GetAvailablePort();
            string endpoint = "http://127.0.0.1:" + partioPort;
            string serverAssemblyPath = FindServerAssemblyPath();
            string settingsPath = Path.Combine(workingDirectory, "partio.json");
            await File.WriteAllTextAsync(settingsPath, BuildSettingsJson(partioPort, options, embeddingUrl, inferenceUrl), token).ConfigureAwait(false);

            SelfHostedPartioTestEnvironment? environment = null;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = FindDotnetExecutable(),
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add(serverAssemblyPath);

                Process serverProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                environment = new SelfHostedPartioTestEnvironment(
                    ollama, serverProcess, workingDirectory, endpoint, serverAssemblyPath, embeddingUrl, inferenceUrl, options.UsesRealEndpoints);
                SelfHostedPartioTestEnvironment outputTarget = environment;
                serverProcess.OutputDataReceived += (_, e) => outputTarget.RecordServerOutput(e.Data);
                serverProcess.ErrorDataReceived += (_, e) => outputTarget.RecordServerOutput(e.Data);

                if (!serverProcess.Start())
                    throw new InvalidOperationException("Unable to start Partio server process.");

                serverProcess.BeginOutputReadLine();
                serverProcess.BeginErrorReadLine();

                await environment.WaitUntilReadyAsync(token).ConfigureAwait(false);
                SelfHostedPartioTestEnvironment started = environment;
                environment = null;
                return started;
            }
            catch
            {
                if (environment != null)
                {
                    await environment.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    if (ollama != null) await ollama.DisposeAsync().ConfigureAwait(false);
                    TryDeleteDirectory(workingDirectory);
                }

                throw;
            }
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (_Disposed) return;
            _Disposed = true;

            try
            {
                if (!_ServerProcess.HasExited)
                {
                    try { _ServerProcess.Kill(entireProcessTree: true); } catch { }
                    try { await _ServerProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); } catch { }
                }
            }
            finally
            {
                _ServerProcess.Dispose();
                if (_Ollama != null) await _Ollama.DisposeAsync().ConfigureAwait(false);
                TryDeleteDirectory(WorkingDirectory);
            }
        }

        private async Task WaitUntilReadyAsync(CancellationToken token)
        {
            using HttpClient http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(2);
            Stopwatch sw = Stopwatch.StartNew();

            while (sw.Elapsed < TimeSpan.FromSeconds(60))
            {
                token.ThrowIfCancellationRequested();

                if (_ServerProcess.HasExited)
                {
                    throw new InvalidOperationException(
                        "Partio server exited before becoming ready. Exit code: "
                        + _ServerProcess.ExitCode
                        + Environment.NewLine
                        + GetServerOutput());
                }

                try
                {
                    using HttpResponseMessage response = await http.GetAsync(Endpoint, token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                        return;
                }
                catch when (!token.IsCancellationRequested)
                {
                }

                await Task.Delay(250, token).ConfigureAwait(false);
            }

            throw new TimeoutException(
                "Partio server did not become ready at " + Endpoint + "."
                + Environment.NewLine
                + GetServerOutput());
        }

        private void RecordServerOutput(string? line)
        {
            if (string.IsNullOrEmpty(line)) return;

            lock (_OutputLock)
            {
                _ServerOutput.Add(line);
                if (_ServerOutput.Count > 200)
                    _ServerOutput.RemoveAt(0);
            }
        }

        private string GetServerOutput()
        {
            lock (_OutputLock)
            {
                return string.Join(Environment.NewLine, _ServerOutput);
            }
        }

        private static string BuildSettingsJson(int partioPort, TestEnvironmentOptions options, string embeddingUrl, string inferenceUrl)
        {
            object settings = new
            {
                Rest = new { Hostname = "127.0.0.1", Port = partioPort, Ssl = false },
                Database = BuildDatabaseSection(options),
                Logging = new
                {
                    ConsoleLogging = false,
                    FileLogging = true,
                    LogDirectory = "./logs",
                    LogFilename = "partio.log",
                    IncludeDateInFilename = false,
                    MinimumSeverity = 0
                },
                Debug = new { Exceptions = true },
                RequestHistory = new
                {
                    Enabled = true,
                    Directory = "./request-history",
                    RetentionDays = 7,
                    CleanupIntervalMinutes = 60,
                    MaxRequestBodyBytes = 65536,
                    MaxResponseBodyBytes = 65536
                },
                AdminApiKeys = new[] { "partioadmin" },
                DefaultEmbeddingEndpoints = new[]
                {
                    new
                    {
                        Name = options.EmbeddingModel,
                        Model = options.EmbeddingModel,
                        Endpoint = embeddingUrl,
                        ApiFormat = options.UpstreamApiFormat,
                        ApiKey = options.UpstreamApiKey,
                        MaximumTimeoutMs = 60000,
                        MaxConcurrentRequests = 2,
                        MaxQueueDepth = 2
                    }
                },
                DefaultInferenceEndpoints = new[]
                {
                    new
                    {
                        Name = options.InferenceModel,
                        Model = options.InferenceModel,
                        Endpoint = inferenceUrl,
                        ApiFormat = options.UpstreamApiFormat,
                        ApiKey = options.UpstreamApiKey,
                        MaximumTimeoutMs = 60000,
                        MaxConcurrentRequests = 2,
                        MaxQueueDepth = 2
                    }
                }
            };

            return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        }

        private static object BuildDatabaseSection(TestEnvironmentOptions options)
        {
            if (options.DatabaseType == DatabaseTypeEnum.Sqlite)
            {
                return new { Type = "Sqlite", Filename = "./partio.db" };
            }

            return new
            {
                Type = options.DatabaseType.ToString(),
                Hostname = options.DatabaseHostname,
                Port = options.DatabasePort,
                DatabaseName = options.DatabaseName,
                Username = options.DatabaseUsername,
                Password = options.DatabasePassword,
                Instance = options.DatabaseInstance,
                Schema = options.DatabaseSchema,
                RequireEncryption = false,
                LogQueries = false
            };
        }

        private static string FindServerAssemblyPath()
        {
            string baseDirectory = AppContext.BaseDirectory;
            List<string> candidates = new List<string>
            {
                Path.Combine(baseDirectory, "Partio.Server.dll")
            };

            DirectoryInfo? baseInfo = new DirectoryInfo(baseDirectory);
            string configuration = baseInfo.Parent?.Name ?? "Debug";
            DirectoryInfo? runnerProjectDirectory = baseInfo.Parent?.Parent?.Parent;
            if (runnerProjectDirectory?.Parent != null)
            {
                candidates.Add(Path.Combine(
                    runnerProjectDirectory.Parent.FullName,
                    "Partio.Server",
                    "bin",
                    configuration,
                    "net10.0",
                    "Partio.Server.dll"));
            }

            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "Partio.Server.dll"));

            foreach (string candidate in candidates)
            {
                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            throw new FileNotFoundException(
                "Unable to locate Partio.Server.dll. Build Partio.Server before running integration tests. Checked: "
                + string.Join(", ", candidates.Select(Path.GetFullPath)));
        }

        private static string FindDotnetExecutable()
        {
            string? dotnetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrWhiteSpace(dotnetHostPath) && File.Exists(dotnetHostPath))
                return dotnetHostPath;

            return "dotnet";
        }

        private static int GetAvailablePort()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
