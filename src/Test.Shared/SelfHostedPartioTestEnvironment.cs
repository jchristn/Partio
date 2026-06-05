namespace Test.Shared
{
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text.Json;

    /// <summary>
    /// Starts the services required by integration tests: an Ollama-compatible
    /// upstream stub and an isolated Partio server process backed by temp SQLite.
    /// </summary>
    public sealed class SelfHostedPartioTestEnvironment : IDisposable, IAsyncDisposable
    {
        private readonly SlowOllamaCompatibleServer _Ollama;
        private readonly Process _ServerProcess;
        private readonly List<string> _ServerOutput = new List<string>();
        private readonly object _OutputLock = new object();
        private bool _Disposed;

        private SelfHostedPartioTestEnvironment(
            SlowOllamaCompatibleServer ollama,
            Process serverProcess,
            string workingDirectory,
            string endpoint,
            string serverAssemblyPath)
        {
            _Ollama = ollama;
            _ServerProcess = serverProcess;
            WorkingDirectory = workingDirectory;
            Endpoint = endpoint;
            ServerAssemblyPath = serverAssemblyPath;
            UpstreamEndpoint = ollama.BaseUrl;
        }

        public string Endpoint { get; }

        public string UpstreamEndpoint { get; }

        public int UpstreamTagsRequestCount => _Ollama.TagsRequestCount;

        public string AdminKey { get; } = "partioadmin";

        public string TestToken { get; } = "default";

        public string WorkingDirectory { get; }

        public string ServerAssemblyPath { get; }

        public int GetUpstreamRawPathRequestCount(string rawPath)
        {
            return _Ollama.GetRawPathRequestCount(rawPath);
        }

        public async Task WaitForUpstreamRawPathRequestCountAsync(string rawPath, int minCount, int timeoutMs = 5000)
        {
            await _Ollama.WaitForRawPathRequestCountAsync(rawPath, minCount, timeoutMs).ConfigureAwait(false);
        }

        public static async Task<SelfHostedPartioTestEnvironment> StartAsync(CancellationToken token = default)
        {
            SlowOllamaCompatibleServer ollama = new SlowOllamaCompatibleServer();
            string workingDirectory = Path.Combine(Path.GetTempPath(), "partio-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            int partioPort = GetAvailablePort();
            string endpoint = "http://127.0.0.1:" + partioPort;
            string serverAssemblyPath = FindServerAssemblyPath();
            string settingsPath = Path.Combine(workingDirectory, "partio.json");
            await File.WriteAllTextAsync(settingsPath, BuildSettingsJson(partioPort, ollama.BaseUrl), token).ConfigureAwait(false);

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
                environment = new SelfHostedPartioTestEnvironment(ollama, serverProcess, workingDirectory, endpoint, serverAssemblyPath);
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
                    await ollama.DisposeAsync().ConfigureAwait(false);
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
                await _Ollama.DisposeAsync().ConfigureAwait(false);
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

        private static string BuildSettingsJson(int partioPort, string upstreamEndpoint)
        {
            object settings = new
            {
                Rest = new { Hostname = "127.0.0.1", Port = partioPort, Ssl = false },
                Database = new { Type = "Sqlite", Filename = "./partio.db" },
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
                        Name = "nomic-embed-text",
                        Model = "nomic-embed-text",
                        Endpoint = upstreamEndpoint,
                        ApiFormat = "Ollama",
                        MaximumTimeoutMs = 60000,
                        MaxConcurrentRequests = 2
                    }
                },
                DefaultInferenceEndpoints = new[]
                {
                    new
                    {
                        Name = "gemma3:4b",
                        Model = "gemma3:4b",
                        Endpoint = upstreamEndpoint,
                        ApiFormat = "Ollama",
                        MaximumTimeoutMs = 60000,
                        MaxConcurrentRequests = 2
                    }
                }
            };

            return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
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
