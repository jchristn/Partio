namespace Partio.McpServer.Commands
{
    using System.Diagnostics;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Partio.McpServer.Settings;

    /// <summary>
    /// Implements the multi-harness <c>mcp install</c> / <c>mcp remove</c> verbs. Each harness is an
    /// idempotent upsert: JSON harnesses are merged in place (never clobbering unrelated entries), and
    /// CLI-backed harnesses (Codex, Gemini) delegate to the harness's own CLI. Managed instruction blocks
    /// are written into AGENTS.md / GEMINI.md between fenced delimiters so <c>remove</c> can reverse them.
    /// </summary>
    public static class McpInstallCommand
    {
        private const string ManagedBegin = "<!-- partio:mcp:begin -->";
        private const string ManagedEnd = "<!-- partio:mcp:end -->";

        /// <summary>
        /// Install the Partio MCP server into every supported, detected harness.
        /// </summary>
        /// <param name="settings">MCP server settings (supplies host/port/path).</param>
        /// <param name="args">Command-line arguments (supports --dry-run and --yes).</param>
        /// <returns>Process exit code.</returns>
        public static int Run(McpServerSettings settings, string[] args)
        {
            bool dryRun = Has(args, "--dry-run");
            string url = BuildUrl(settings);
            string home = HomeDir();

            Console.WriteLine("Partio MCP install" + (dryRun ? " (dry-run — no files will be written)" : ""));
            Console.WriteLine("  URL: " + url);
            Console.WriteLine("");

            InstallClaude(home, settings, url, dryRun);
            InstallCursor(settings, url, dryRun);
            InstallMux(home, settings, url, dryRun);
            DelegateCli("Codex", "codex", new[] { "mcp", "add", "partio", "--", ExecutablePath(), "mcp", "stdio" }, dryRun);
            DelegateCli("Gemini", "gemini", new[] { "mcp", "add", "--scope", "user", "--transport", "http", "partio", url }, dryRun);
            WriteManagedInstructions("AGENTS.md", url, dryRun);
            WriteManagedInstructions("GEMINI.md", url, dryRun);

            Console.WriteLine("");
            Console.WriteLine("Done. Start the Partio server and the MCP server, then restart your MCP client.");
            return 0;
        }

        /// <summary>
        /// Remove Partio entries and managed instruction blocks from every supported harness.
        /// </summary>
        /// <param name="settings">MCP server settings.</param>
        /// <param name="args">Command-line arguments (supports --dry-run).</param>
        /// <returns>Process exit code.</returns>
        public static int Remove(McpServerSettings settings, string[] args)
        {
            bool dryRun = Has(args, "--dry-run");
            string home = HomeDir();

            Console.WriteLine("Partio MCP remove" + (dryRun ? " (dry-run — no files will be written)" : ""));
            Console.WriteLine("");

            RemoveJsonObjectEntry(Path.Combine(home, ".claude.json"), new[] { "mcpServers", "partio" }, dryRun, "Claude Code");
            DeleteFileIfExists(Path.Combine(home, ".claude", "agents", "partio.md"), dryRun, "Claude sub-agent");
            RemoveJsonObjectEntry(Path.Combine(Directory.GetCurrentDirectory(), ".cursor", "mcp.json"), new[] { "mcpServers", "partio" }, dryRun, "Cursor");
            RemoveMux(home, dryRun);
            DelegateCli("Codex", "codex", new[] { "mcp", "remove", "partio" }, dryRun);
            DelegateCli("Gemini", "gemini", new[] { "mcp", "remove", "partio" }, dryRun);
            RemoveManagedInstructions("AGENTS.md", dryRun);
            RemoveManagedInstructions("GEMINI.md", dryRun);

            Console.WriteLine("");
            Console.WriteLine("Done.");
            return 0;
        }

        // ---------------- Per-harness installers ----------------

        private static void InstallClaude(string home, McpServerSettings settings, string url, bool dryRun)
        {
            string path = Path.Combine(home, ".claude.json");
            JsonObject entry = new JsonObject { ["type"] = "http", ["url"] = url };
            UpsertJsonObjectEntry(path, new[] { "mcpServers", "partio" }, entry, dryRun, "Claude Code");

            string agentPath = Path.Combine(home, ".claude", "agents", "partio.md");
            string agentBody =
                "---\nname: partio\ndescription: Manage Partio endpoints and run inference via the Partio MCP server.\nallowedTools: mcp__partio__*\n---\n\n"
                + "Use the Partio MCP tools to manage embedding and completion endpoints (including MaxConcurrentRequests and MaxQueueDepth) and to run summarize/chunk/embed.\n";
            WriteFile(agentPath, agentBody, dryRun, "Claude sub-agent");
        }

        private static void InstallCursor(McpServerSettings settings, string url, bool dryRun)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), ".cursor", "mcp.json");
            JsonObject entry = new JsonObject { ["url"] = url, ["transport"] = "http" };
            UpsertJsonObjectEntry(path, new[] { "mcpServers", "partio" }, entry, dryRun, "Cursor");
        }

        private static void InstallMux(string home, McpServerSettings settings, string url, bool dryRun)
        {
            if (!IsMuxAvailable(home))
            {
                Console.WriteLine("Mux         : not detected, skipped.");
                return;
            }

            string configDir = Environment.GetEnvironmentVariable("MUX_CONFIG_DIR") ?? Path.Combine(home, ".mux");
            string path = Path.Combine(configDir, "mcp-servers.json");
            // Mux uses a host-only url + separate mcpPath.
            string hostUrl = "http://" + (settings.McpHost == "*" ? "localhost" : settings.McpHost) + ":" + settings.McpPort;
            JsonObject entry = new JsonObject
            {
                ["name"] = "partio",
                ["transport"] = "http",
                ["url"] = hostUrl,
                ["mcpPath"] = settings.McpPath
            };
            UpsertJsonArrayEntry(path, "servers", "name", "partio", entry, dryRun, "Mux");
        }

        // ---------------- JSON upsert helpers ----------------

        private static void UpsertJsonObjectEntry(string path, string[] keys, JsonObject entry, bool dryRun, string label)
        {
            JsonObject root = ReadJsonObject(path);
            JsonObject cursor = root;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (cursor[keys[i]] is not JsonObject child)
                {
                    child = new JsonObject();
                    cursor[keys[i]] = child;
                }
                cursor = child;
            }

            string leaf = keys[^1];
            if (JsonEquals(cursor[leaf], entry))
            {
                Console.WriteLine(Pad(label) + ": already up to date (" + path + ")");
                return;
            }

            cursor[leaf] = entry;
            WriteJson(path, root, dryRun, label);
        }

        private static void UpsertJsonArrayEntry(string path, string arrayKey, string idKey, string idValue, JsonObject entry, bool dryRun, string label)
        {
            JsonObject root = ReadJsonObject(path);
            if (root[arrayKey] is not JsonArray array)
            {
                array = new JsonArray();
                root[arrayKey] = array;
            }

            for (int i = 0; i < array.Count; i++)
            {
                if (array[i] is JsonObject obj && (string?)obj[idKey] == idValue)
                {
                    if (JsonEquals(obj, entry))
                    {
                        Console.WriteLine(Pad(label) + ": already up to date (" + path + ")");
                        return;
                    }
                    array[i] = entry;
                    WriteJson(path, root, dryRun, label);
                    return;
                }
            }

            array.Add(entry);
            WriteJson(path, root, dryRun, label);
        }

        private static void RemoveJsonObjectEntry(string path, string[] keys, bool dryRun, string label)
        {
            if (!File.Exists(path)) { Console.WriteLine(Pad(label) + ": nothing to remove."); return; }
            JsonObject root = ReadJsonObject(path);
            JsonObject cursor = root;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (cursor[keys[i]] is not JsonObject child) { Console.WriteLine(Pad(label) + ": nothing to remove."); return; }
                cursor = child;
            }

            if (cursor.Remove(keys[^1]))
                WriteJson(path, root, dryRun, label);
            else
                Console.WriteLine(Pad(label) + ": nothing to remove.");
        }

        private static void RemoveMux(string home, bool dryRun)
        {
            string configDir = Environment.GetEnvironmentVariable("MUX_CONFIG_DIR") ?? Path.Combine(home, ".mux");
            string path = Path.Combine(configDir, "mcp-servers.json");
            if (!File.Exists(path)) { Console.WriteLine(Pad("Mux") + ": nothing to remove."); return; }

            JsonObject root = ReadJsonObject(path);
            if (root["servers"] is JsonArray array)
            {
                for (int i = array.Count - 1; i >= 0; i--)
                {
                    if (array[i] is JsonObject obj && (string?)obj["name"] == "partio")
                        array.RemoveAt(i);
                }
                WriteJson(path, root, dryRun, "Mux");
            }
            else
            {
                Console.WriteLine(Pad("Mux") + ": nothing to remove.");
            }
        }

        // ---------------- CLI delegation ----------------

        private static void DelegateCli(string label, string exe, string[] cliArgs, bool dryRun)
        {
            if (dryRun)
            {
                Console.WriteLine(Pad(label) + ": would run '" + exe + " " + string.Join(" ", cliArgs) + "'");
                return;
            }

            if (!IsOnPath(exe))
            {
                Console.WriteLine(Pad(label) + ": CLI '" + exe + "' not found on PATH, skipped. Manual: " + exe + " " + string.Join(" ", cliArgs));
                return;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                foreach (string a in cliArgs) psi.ArgumentList.Add(a);
                using Process? proc = Process.Start(psi);
                proc?.WaitForExit(30000);
                Console.WriteLine(Pad(label) + ": " + (proc != null && proc.ExitCode == 0 ? "configured via CLI." : "CLI returned a non-zero exit code; check manually."));
            }
            catch (Exception ex)
            {
                Console.WriteLine(Pad(label) + ": CLI invocation failed (" + ex.Message + "). Manual: " + exe + " " + string.Join(" ", cliArgs));
            }
        }

        // ---------------- Managed instruction blocks ----------------

        private static void WriteManagedInstructions(string fileName, string url, bool dryRun)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            string block = ManagedBegin + "\n"
                + "## Partio MCP\n"
                + "This project is wired to the Partio MCP server at " + url + ".\n"
                + "Use the `partio_*` tools to manage embedding/completion endpoints (including MaxConcurrentRequests and MaxQueueDepth) and to run summarize/chunk/embed.\n"
                + ManagedEnd;

            string existing = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            string updated = ReplaceManagedBlock(existing, block);
            if (updated == existing)
            {
                Console.WriteLine(Pad(fileName) + ": instruction block already current.");
                return;
            }
            WriteFile(path, updated, dryRun, fileName + " instructions");
        }

        private static void RemoveManagedInstructions(string fileName, bool dryRun)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            if (!File.Exists(path)) { Console.WriteLine(Pad(fileName) + ": nothing to remove."); return; }
            string existing = File.ReadAllText(path);
            string updated = StripManagedBlock(existing);
            if (updated == existing) { Console.WriteLine(Pad(fileName) + ": no managed block present."); return; }
            WriteFile(path, updated, dryRun, fileName + " instructions");
        }

        private static string ReplaceManagedBlock(string content, string block)
        {
            string stripped = StripManagedBlock(content);
            if (stripped.Length > 0 && !stripped.EndsWith("\n")) stripped += "\n";
            return stripped + block + "\n";
        }

        private static string StripManagedBlock(string content)
        {
            int start = content.IndexOf(ManagedBegin, StringComparison.Ordinal);
            int end = content.IndexOf(ManagedEnd, StringComparison.Ordinal);
            if (start < 0 || end < 0 || end < start) return content;
            end += ManagedEnd.Length;
            string before = content.Substring(0, start).TrimEnd('\n', '\r');
            string after = end < content.Length ? content.Substring(end) : string.Empty;
            after = after.TrimStart('\n', '\r');
            if (before.Length > 0 && after.Length > 0) return before + "\n\n" + after;
            return before + after;
        }

        // ---------------- Low-level IO ----------------

        private static JsonObject ReadJsonObject(string path)
        {
            if (!File.Exists(path)) return new JsonObject();
            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
            try
            {
                return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
            }
            catch
            {
                return new JsonObject();
            }
        }

        private static void WriteJson(string path, JsonObject root, bool dryRun, string label)
        {
            string json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            WriteFile(path, json, dryRun, label);
        }

        private static void WriteFile(string path, string content, bool dryRun, string label)
        {
            if (dryRun)
            {
                Console.WriteLine(Pad(label) + ": would write " + path);
                return;
            }
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, content);
            Console.WriteLine(Pad(label) + ": wrote " + path);
        }

        private static void DeleteFileIfExists(string path, bool dryRun, string label)
        {
            if (!File.Exists(path)) { Console.WriteLine(Pad(label) + ": nothing to remove."); return; }
            if (dryRun) { Console.WriteLine(Pad(label) + ": would delete " + path); return; }
            File.Delete(path);
            Console.WriteLine(Pad(label) + ": deleted " + path);
        }

        // ---------------- Utilities ----------------

        private static bool JsonEquals(JsonNode? a, JsonNode? b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a.ToJsonString(), b.ToJsonString(), StringComparison.Ordinal);
        }

        private static bool IsMuxAvailable(string home)
        {
            return Directory.Exists(Path.Combine(home, ".mux")) || IsOnPath("mux");
        }

        private static bool IsOnPath(string exe)
        {
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return false;
            string[] exts = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", ".bat", "" } : new[] { "" };
            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                foreach (string ext in exts)
                {
                    try
                    {
                        if (File.Exists(Path.Combine(dir, exe + ext))) return true;
                    }
                    catch { /* ignore malformed PATH entries */ }
                }
            }
            return false;
        }

        private static string BuildUrl(McpServerSettings settings)
        {
            string host = settings.McpHost == "*" || string.IsNullOrWhiteSpace(settings.McpHost) ? "localhost" : settings.McpHost;
            return "http://" + host + ":" + settings.McpPort + settings.McpPath;
        }

        private static string ExecutablePath()
        {
            return Environment.ProcessPath ?? "partio-mcp";
        }

        private static string HomeDir()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private static bool Has(string[] args, string flag)
        {
            foreach (string a in args)
                if (string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string Pad(string label)
        {
            return label.Length >= 12 ? label : label + new string(' ', 12 - label.Length);
        }
    }
}
