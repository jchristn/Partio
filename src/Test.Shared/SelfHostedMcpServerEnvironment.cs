namespace Test.Shared
{
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text.Json;

    /// <summary>
    /// Starts the Partio MCP server (<c>partio-mcp</c>) as a child process pointed at an already-running
    /// Partio server. The MCP server runs out of process so the test's HTTP client and Voltaic's listener
    /// never share a thread pool.
    /// </summary>
    public sealed class SelfHostedMcpServerEnvironment : IDisposable, IAsyncDisposable
    {
        private readonly Process _ServerProcess;
        private readonly List<string> _ServerOutput = new List<string>();
        private readonly object _OutputLock = new object();
        private bool _Disposed;

        private SelfHostedMcpServerEnvironment(Process serverProcess, string workingDirectory, string baseUrl)
        {
            _ServerProcess = serverProcess;
            WorkingDirectory = workingDirectory;
            BaseUrl = baseUrl;
        }

        /// <summary>Base URL of the MCP server, for example http://127.0.0.1:12345.</summary>
        public string BaseUrl { get; }

        /// <summary>URL of the MCP Streamable HTTP endpoint.</summary>
        public string McpUrl => BaseUrl + "/mcp";

        /// <summary>URL of the plain JSON-RPC endpoint.</summary>
        public string RpcUrl => BaseUrl + "/rpc";

        /// <summary>Temporary working directory holding the generated settings file.</summary>
        public string WorkingDirectory { get; }

        /// <summary>
        /// Start the MCP server against the supplied Partio endpoint with bearer authentication required.
        /// </summary>
        /// <param name="partioEndpoint">Base URL of the running Partio server.</param>
        /// <param name="fallbackApiKey">Partio API key used only when authentication is disabled.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The started environment.</returns>
        public static async Task<SelfHostedMcpServerEnvironment> StartAsync(string partioEndpoint, string fallbackApiKey, CancellationToken token = default)
        {
            string workingDirectory = Path.Combine(Path.GetTempPath(), "partio-mcp-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            int port = GetAvailablePort();
            string configPath = Path.Combine(workingDirectory, "partio.mcp.json");
            object settings = new
            {
                McpHost = "127.0.0.1",
                McpPort = port,
                PartioEndpoint = partioEndpoint,
                PartioApiKey = fallbackApiKey,
                RequireAuthentication = true,
                LogLevel = 1
            };
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(settings), token).ConfigureAwait(false);

            SelfHostedMcpServerEnvironment? environment = null;
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
                psi.ArgumentList.Add(FindServerAssemblyPath());
                psi.ArgumentList.Add("--config=" + configPath);

                // Clear inherited overrides so the generated settings file is authoritative.
                psi.Environment.Remove("PARTIO_ENDPOINT");
                psi.Environment.Remove("PARTIO_API_KEY");
                psi.Environment.Remove("PARTIO_MCP_HOST");
                psi.Environment.Remove("PARTIO_MCP_PORT");

                Process serverProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
                environment = new SelfHostedMcpServerEnvironment(serverProcess, workingDirectory, "http://127.0.0.1:" + port);
                SelfHostedMcpServerEnvironment outputTarget = environment;
                serverProcess.OutputDataReceived += (_, e) => outputTarget.RecordServerOutput(e.Data);
                serverProcess.ErrorDataReceived += (_, e) => outputTarget.RecordServerOutput(e.Data);

                if (!serverProcess.Start())
                    throw new InvalidOperationException("Unable to start the Partio MCP server process.");

                serverProcess.BeginOutputReadLine();
                serverProcess.BeginErrorReadLine();

                await environment.WaitUntilReadyAsync(token).ConfigureAwait(false);
                SelfHostedMcpServerEnvironment started = environment;
                environment = null;
                return started;
            }
            catch
            {
                if (environment != null) await environment.DisposeAsync().ConfigureAwait(false);
                else TryDeleteDirectory(workingDirectory);
                throw;
            }
        }

        /// <summary>
        /// Return the most recent lines the MCP server wrote to stdout/stderr.
        /// </summary>
        /// <returns>Captured server output.</returns>
        public string GetServerOutput()
        {
            lock (_OutputLock)
            {
                return string.Join(Environment.NewLine, _ServerOutput);
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
                        "Partio MCP server exited before becoming ready. Exit code: "
                        + _ServerProcess.ExitCode
                        + Environment.NewLine
                        + GetServerOutput());
                }

                try
                {
                    // GET / is the unauthenticated health endpoint.
                    using HttpResponseMessage response = await http.GetAsync(BaseUrl + "/", token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                        return;
                }
                catch when (!token.IsCancellationRequested)
                {
                }

                await Task.Delay(250, token).ConfigureAwait(false);
            }

            throw new TimeoutException(
                "Partio MCP server did not become ready at " + BaseUrl + "."
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

        private static string FindServerAssemblyPath()
        {
            string baseDirectory = AppContext.BaseDirectory;
            List<string> candidates = new List<string>
            {
                Path.Combine(baseDirectory, "partio-mcp.dll")
            };

            DirectoryInfo baseInfo = new DirectoryInfo(baseDirectory);
            string configuration = baseInfo.Parent?.Name ?? "Debug";
            DirectoryInfo? runnerProjectDirectory = baseInfo.Parent?.Parent?.Parent;
            if (runnerProjectDirectory?.Parent != null)
            {
                candidates.Add(Path.Combine(
                    runnerProjectDirectory.Parent.FullName,
                    "Partio.McpServer",
                    "bin",
                    configuration,
                    "net10.0",
                    "partio-mcp.dll"));
            }

            foreach (string candidate in candidates)
            {
                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            throw new FileNotFoundException(
                "Unable to locate partio-mcp.dll. Build Partio.McpServer before running MCP integration tests. Checked: "
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
