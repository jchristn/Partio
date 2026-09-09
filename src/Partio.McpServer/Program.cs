namespace Partio.McpServer
{
    using Partio.McpServer.Commands;
    using Partio.McpServer.Settings;
    using Partio.Sdk;
    using SerializationHelper;
    using SyslogLogging;

    /// <summary>
    /// Entry point for the Partio MCP server executable.
    /// </summary>
    public static class Program
    {
        private static readonly string _Header = "[PartioMcp] ";
        private const string DefaultConfigFile = "partio.mcp.json";

        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Process exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            if (HasFlag(args, "--help") || HasFlag(args, "-h") || HasFlag(args, "-?"))
            {
                PrintHelp();
                return 0;
            }

            string configPath = GetOption(args, "--config") ?? DefaultConfigFile;
            McpServerSettings settings = LoadSettings(configPath);

            if (HasFlag(args, "--showconfig"))
            {
                Console.WriteLine(new Serializer().SerializeJson(settings, true));
                return 0;
            }

            // Verb: mcp install / mcp remove / mcp stdio (with --install / --remove flag aliases).
            if (IsVerb(args, "mcp", "install") || HasFlag(args, "--install"))
                return McpInstallCommand.Run(settings, args);
            if (IsVerb(args, "mcp", "remove") || HasFlag(args, "--remove") || HasFlag(args, "--uninstall"))
                return McpInstallCommand.Remove(settings, args);
            if (IsVerb(args, "mcp", "stdio"))
                return await StdioBridge.RunAsync(settings).ConfigureAwait(false);

            PrintBanner(settings);

            LoggingModule logging = new LoggingModule();
            logging.Settings.MinimumSeverity = (Severity)settings.LogLevel;

            using PartioClient client = new PartioClient(settings.PartioEndpoint, settings.PartioApiKey);
            using Mcp.PartioMcpServer server = new Mcp.PartioMcpServer(settings, logging, client);

            using CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await server.StartAsync(cts.Token).ConfigureAwait(false);
                Console.WriteLine("Partio MCP server is running. Press Ctrl-C to stop.");
                await WaitForCancellationAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                logging.Alert(_Header + "fatal error: " + ex.Message);
                return 1;
            }

            logging.Info(_Header + "shutting down");
            return 0;
        }

        private static McpServerSettings LoadSettings(string configPath)
        {
            McpServerSettings settings;
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                settings = new Serializer().DeserializeJson<McpServerSettings>(json) ?? new McpServerSettings();
            }
            else
            {
                settings = new McpServerSettings();
            }

            // Environment variable overrides take precedence over the file.
            string? endpoint = Environment.GetEnvironmentVariable("PARTIO_ENDPOINT");
            if (!string.IsNullOrWhiteSpace(endpoint)) settings.PartioEndpoint = endpoint;

            string? apiKey = Environment.GetEnvironmentVariable("PARTIO_API_KEY");
            if (!string.IsNullOrWhiteSpace(apiKey)) settings.PartioApiKey = apiKey;

            string? host = Environment.GetEnvironmentVariable("PARTIO_MCP_HOST");
            if (!string.IsNullOrWhiteSpace(host)) settings.McpHost = host;

            string? port = Environment.GetEnvironmentVariable("PARTIO_MCP_PORT");
            if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out int parsedPort)) settings.McpPort = parsedPort;

            return settings;
        }

        private static async Task WaitForCancellationAsync(CancellationToken token)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(static s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }

        private static void PrintBanner(McpServerSettings settings)
        {
            Console.WriteLine("");
            Console.WriteLine("Partio MCP Server");
            Console.WriteLine("  Partio endpoint : " + settings.PartioEndpoint);
            Console.WriteLine("  MCP bind        : http://" + settings.McpHost + ":" + settings.McpPort + settings.McpPath);
            Console.WriteLine("  Authentication  : " + (settings.RequireAuthentication ? "bearer required" : "disabled"));
            Console.WriteLine("  CORS            : " + (settings.Cors.Enabled ? "enabled (" + settings.Cors.AllowedOrigins + ")" : "disabled"));
            Console.WriteLine("");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Partio MCP Server");
            Console.WriteLine("");
            Console.WriteLine("Usage: partio-mcp [options] [verb]");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("  --config=<path>   Path to the settings file (default: partio.mcp.json)");
            Console.WriteLine("  --showconfig      Print the effective configuration and exit");
            Console.WriteLine("  --help            Show this help and exit");
            Console.WriteLine("");
            Console.WriteLine("Verbs:");
            Console.WriteLine("  mcp stdio         Run a stdio<->HTTP JSON-RPC bridge to the MCP server");
            Console.WriteLine("  mcp install       Configure supported MCP clients to use this server (alias: --install)");
            Console.WriteLine("  mcp remove        Remove Partio entries from supported MCP clients (alias: --remove)");
            Console.WriteLine("");
            Console.WriteLine("Environment overrides: PARTIO_ENDPOINT, PARTIO_API_KEY, PARTIO_MCP_HOST, PARTIO_MCP_PORT");
        }

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (string arg in args)
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string? GetOption(string[] args, string name)
        {
            string prefix = name + "=";
            foreach (string arg in args)
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return arg.Substring(prefix.Length);
            return null;
        }

        private static bool IsVerb(string[] args, string first, string second)
        {
            List<string> positional = new List<string>();
            foreach (string arg in args)
                if (!arg.StartsWith("-", StringComparison.Ordinal)) positional.Add(arg);
            return positional.Count >= 2
                && string.Equals(positional[0], first, StringComparison.OrdinalIgnoreCase)
                && string.Equals(positional[1], second, StringComparison.OrdinalIgnoreCase);
        }
    }
}
