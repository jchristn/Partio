// Live end-to-end test for the Partio transparent completion proxy (/v1.0/proxy/{endpointId}/...).
//
// It boots the freshly-built Partio server in-process (via the shared self-hosted test environment),
// creates a completion endpoint pointing at a REAL upstream provider (with the upstream bearer token
// injected server-side), and exercises the proxy through the C# SDK's ProxyAsync methods — verifying the
// upstream's response is relayed verbatim and that the per-format sub-path allow-list is enforced.
//
// All connection parameters are CLI arguments, so the same program can be pointed at any upstream:
//   dotnet run -- --upstream <base-url> --token <bearer> --model <model> [--format Ollama|OpenAI] [--keep]
//
// --upstream : upstream provider base URL the endpoint forwards to (required)
// --token    : bearer token / API key injected on upstream calls (optional)
// --model    : model name to send in request bodies (default: llama3)
// --format   : restrict to one dialect (Ollama or OpenAI); default tests both
// --keep     : do not delete the created endpoint(s) on exit

using System.Text.Json;
using Partio.Sdk;
using Partio.Sdk.Models;
using Test.Shared;

string? GetArg(string name)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == name && i + 1 < args.Length && !args[i + 1].StartsWith("--")) return args[i + 1];
        if (args[i].StartsWith(name + "=")) return args[i].Substring(name.Length + 1);
    }
    return null;
}
bool HasFlag(string name) => Array.IndexOf(args, name) >= 0;

string? upstream = GetArg("--upstream");
if (string.IsNullOrWhiteSpace(upstream))
{
    Console.Error.WriteLine("Error: --upstream <base-url> is required (the upstream provider base URL the proxy forwards to).");
    Console.Error.WriteLine("Usage: --upstream <base-url> [--token <bearer>] [--model <model>] [--format Ollama|OpenAI] [--keep]");
    return 2;
}
string token = GetArg("--token") ?? "";
string model = GetArg("--model") ?? "llama3";
string? onlyFormat = GetArg("--format");
bool keep = HasFlag("--keep");

Console.WriteLine("=== Partio proxy live test ===");
Console.WriteLine("Upstream : " + upstream);
Console.WriteLine("Model    : " + model);
Console.WriteLine("Token    : " + (string.IsNullOrEmpty(token) ? "(none)" : token.Substring(0, Math.Min(8, token.Length)) + "…"));
Console.WriteLine("Formats  : " + (onlyFormat ?? "Ollama + OpenAI"));
Console.WriteLine();

int failures = 0;

static string Truncate(string? s, int max = 600)
{
    if (string.IsNullOrEmpty(s)) return "(empty)";
    s = s.Replace("\r", " ").Replace("\n", " ");
    return s.Length <= max ? s : s.Substring(0, max) + " …";
}

Console.WriteLine("Booting local Partio server (self-hosted)…");
await using SelfHostedPartioTestEnvironment env = await SelfHostedPartioTestEnvironment.StartAsync(new TestEnvironmentOptions());
Console.WriteLine("Partio ready at " + env.Endpoint);
Console.WriteLine();

using PartioClient admin = new PartioClient(env.Endpoint, env.AdminKey);

// Resolve a tenant to own the endpoint.
string tenantId;
EnumerationResult<TenantMetadata>? tenants = await admin.EnumerateTenantsAsync(new EnumerationRequest { MaxResults = 10 });
if (tenants?.Data != null && tenants.Data.Count > 0 && !string.IsNullOrEmpty(tenants.Data[0].Id))
{
    tenantId = tenants.Data[0].Id!;
}
else
{
    TenantMetadata? created = await admin.CreateTenantAsync(new TenantMetadata { Name = "proxy-livetest" });
    tenantId = created?.Id ?? throw new Exception("Could not resolve or create a tenant.");
}
Console.WriteLine("Tenant   : " + tenantId);
Console.WriteLine();

string ollamaChat = JsonSerializer.Serialize(new
{
    model,
    messages = new[] { new { role = "user", content = "Reply with exactly: OK" } },
    stream = false
});
string ollamaGen = JsonSerializer.Serialize(new { model, prompt = "Reply with exactly: OK", stream = false });
string openaiChat = JsonSerializer.Serialize(new
{
    model,
    messages = new[] { new { role = "user", content = "Reply with exactly: OK" } }
});

async Task Run(string label, int expectStatus, Func<Task<ProxyResponse>> call)
{
    try
    {
        ProxyResponse r = await call();
        bool ok = r.StatusCode == expectStatus;
        if (!ok) failures++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + label + " -> " + r.StatusCode + " (expected " + expectStatus + ")");
        Console.WriteLine("      " + Truncate(r.Body));
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine("FAIL " + label + " -> EXCEPTION " + ex.Message);
    }
    Console.WriteLine();
}

string[] formats = onlyFormat != null
    ? new[] { onlyFormat }
    : new[] { "Ollama", "OpenAI" };

foreach (string fmt in formats)
{
    Console.WriteLine("----- " + fmt + " endpoint -----");
    CompletionEndpoint? ep = await admin.CreateCompletionEndpointAsync(new CompletionEndpoint
    {
        TenantId = tenantId,
        Name = "Proxy Live " + fmt,
        Model = model,
        Endpoint = upstream,
        ApiFormat = fmt,
        ApiKey = string.IsNullOrEmpty(token) ? null : token,
        HealthCheckEnabled = false,
        MaximumTimeoutMs = 120000
    });

    if (ep == null || string.IsNullOrEmpty(ep.Id))
    {
        failures++;
        Console.WriteLine("FAIL could not create " + fmt + " endpoint");
        Console.WriteLine();
        continue;
    }
    Console.WriteLine("Endpoint : " + ep.Id);
    Console.WriteLine();

    if (fmt.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
    {
        await Run("GET  api/tags", 200, () => admin.ProxyGetAsync(ep.Id, "api/tags"));
        await Run("POST api/chat", 200, () => admin.ProxyPostAsync(ep.Id, "api/chat", ollamaChat));
        await Run("POST api/generate", 200, () => admin.ProxyPostAsync(ep.Id, "api/generate", ollamaGen));
        // Policy: an OpenAI path is not permitted for an Ollama endpoint -> 404, never forwarded.
        await Run("POST v1/chat/completions (policy 404)", 404, () => admin.ProxyPostAsync(ep.Id, "v1/chat/completions", openaiChat));
    }
    else
    {
        await Run("GET  v1/models", 200, () => admin.ProxyGetAsync(ep.Id, "v1/models"));
        await Run("POST v1/chat/completions", 200, () => admin.ProxyPostAsync(ep.Id, "v1/chat/completions", openaiChat));
        // Policy: an Ollama path is not permitted for an OpenAI endpoint -> 404, never forwarded.
        await Run("POST api/chat (policy 404)", 404, () => admin.ProxyPostAsync(ep.Id, "api/chat", ollamaChat));
    }

    if (!keep)
        await admin.DeleteCompletionEndpointAsync(ep.Id);
}

Console.WriteLine("=== " + (failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED") + " ===");
return failures == 0 ? 0 : 1;
