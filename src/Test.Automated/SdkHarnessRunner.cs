namespace Test.Automated
{
    using System.Diagnostics;
    using Test.Shared;

    public sealed class SdkHarnessRunSummary
    {
        public int FailedCount { get; set; }
    }

    public static class SdkHarnessRunner
    {
        public static async Task<SdkHarnessRunSummary> RunAsync(SelfHostedPartioTestEnvironment environment, TestEnvironmentOptions? options = null, CancellationToken token = default)
        {
            if (environment == null) throw new ArgumentNullException(nameof(environment));

            string embeddingModel = string.IsNullOrWhiteSpace(options?.EmbeddingModel) ? "nomic-embed-text" : options!.EmbeddingModel;
            string completionModel = string.IsNullOrWhiteSpace(options?.InferenceModel) ? "gemma3:4b" : options!.InferenceModel;
            string? providerBearer = options?.UpstreamApiKey;

            // The proxy exercise in each harness forwards through the provider; the bearer (when set) is
            // injected on the upstream call so the harnesses can be pointed at an authenticated provider.
            string[] WithBearer(params string[] baseArgs) =>
                string.IsNullOrEmpty(providerBearer) ? baseArgs : baseArgs.Append(providerBearer!).ToArray();

            string repositoryRoot = FindRepositoryRoot();
            List<HarnessCommand> commands = new List<HarnessCommand>
            {
                new HarnessCommand(
                    "C# SDK harness",
                    "dotnet",
                    WithBearer(
                        "run",
                        "--project",
                        Path.Combine(repositoryRoot, "sdk", "csharp", "Partio.Sdk.TestHarness", "Partio.Sdk.TestHarness.csproj"),
                        "--",
                        environment.Endpoint,
                        environment.AdminKey,
                        environment.TestToken,
                        environment.UpstreamEndpoint,
                        embeddingModel,
                        completionModel),
                    repositoryRoot),
                new HarnessCommand(
                    "JavaScript SDK harness",
                    "node",
                    WithBearer(
                        Path.Combine(repositoryRoot, "sdk", "js", "test-harness.js"),
                        environment.Endpoint,
                        environment.AdminKey,
                        environment.UpstreamEndpoint,
                        embeddingModel,
                        completionModel),
                    repositoryRoot),
                new HarnessCommand(
                    "Python SDK harness",
                    "python",
                    WithBearer(
                        Path.Combine(repositoryRoot, "sdk", "python", "test_harness.py"),
                        environment.Endpoint,
                        environment.AdminKey,
                        environment.UpstreamEndpoint,
                        embeddingModel,
                        completionModel),
                    repositoryRoot)
            };

            int failed = 0;
            Console.WriteLine("Self-hosted SDK harness environment");
            Console.WriteLine("Partio endpoint: " + environment.Endpoint);
            Console.WriteLine("Provider endpoint: " + environment.UpstreamEndpoint);
            Console.WriteLine();

            foreach (HarnessCommand command in commands)
            {
                int exitCode = await RunCommandAsync(command, token).ConfigureAwait(false);
                if (exitCode != 0)
                    failed++;
            }

            Console.WriteLine();
            Console.WriteLine("SDK harness summary: " + (commands.Count - failed) + " passed, " + failed + " failed");
            return new SdkHarnessRunSummary { FailedCount = failed };
        }

        private static async Task<int> RunCommandAsync(HarnessCommand command, CancellationToken token)
        {
            Console.WriteLine("=== " + command.Name + " ===");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = command.FileName,
                WorkingDirectory = command.WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (string argument in command.Arguments)
                psi.ArgumentList.Add(argument);

            using Process process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Console.WriteLine(e.Data);
            };

            if (!process.Start())
                throw new InvalidOperationException("Unable to start " + command.Name);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            Console.WriteLine("=== " + command.Name + " exit code: " + process.ExitCode + " ===");
            Console.WriteLine();
            return process.ExitCode;
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "sdk"))
                    && Directory.Exists(Path.Combine(directory.FullName, "src")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate repository root from " + Directory.GetCurrentDirectory());
        }

        private sealed record HarnessCommand(string Name, string FileName, string[] Arguments, string WorkingDirectory);
    }
}
