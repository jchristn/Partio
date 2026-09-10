# Partio MCP Server

`partio-mcp` is a standalone Model Context Protocol server that puts Partio's endpoint management and inference operations in front of an AI agent as callable tools. It is a separate executable from the Partio REST server — you point it at a running Partio instance, and it calls that server through the Partio C# SDK on your behalf, using your own bearer token. It is built on **Voltaic 0.7.1** and speaks **JSON-RPC 2.0** over MCP Streamable HTTP at `/mcp` (with a plain JSON-RPC endpoint at `/rpc` and an SSE stream at `/events`).

The MCP server does not invent its own authorization model. It carries the same credentials and enforces the same permissions as the REST API — every tool call is a Partio SDK call made with the caller's own token, so an agent can do through MCP exactly what that token can do directly against REST, and no more.

## Authentication

Inbound MCP requests authenticate with a bearer token, and MCP accepts **exactly what the REST API accepts** — because it validates the token against Partio itself rather than keeping its own key list. Present an `Authorization: Bearer <token>` header carrying either a Partio **admin API key** or a **tenant credential bearer token** (the same tokens REST honors). The server validates it against Partio (`GET /v1.0/whoami`) **before any tool runs**; if Partio rejects it, MCP returns **HTTP 401** and the tool body never executes. On success the very same token is forwarded on every SDK call the tool makes, so each operation runs as **your identity and tenant**, with the same authorization you would get calling REST directly.

A few things bypass this check by design:

- **CORS preflight** (`OPTIONS`) requests, so browsers can negotiate cross-origin access.
- The **health endpoint** `GET /`, so liveness probes work without a credential.
- The **`ping`** JSON-RPC method, so a client can confirm the transport is alive before authenticating.

Authentication can be turned off entirely by setting `RequireAuthentication: false` in configuration. Do that only on a trusted local socket; with it off, any caller that can reach the port can invoke every tool.

CORS is enabled by default and permissive: `Access-Control-Allow-Origin: *`, methods `GET, POST, PUT, DELETE, HEAD, OPTIONS`, and a 24-hour max-age. Preflight requests return `204`.

## Methods

The server implements the standard MCP JSON-RPC methods.

| Method | Purpose |
|---|---|
| `initialize` | Handshake. The client announces its protocol version and capabilities; the server returns its own, including server info. |
| `tools/list` | Return the catalog of available tools, each with a name, description, and JSON Schema for its arguments. |
| `tools/call` | Invoke one tool by name with an `arguments` object. Returns the tool result. |
| `ping` | Liveness check. Bypasses authentication. |

A `tools/call` result carries **structured content** — the tool's return value is a typed object, not just a formatted string — so a client can consume the result programmatically rather than parsing prose. A minimal call and reply:

```json
// --> request
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "partio_capabilities",
    "arguments": {}
  }
}
```

```json
// <-- response
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      { "type": "text", "text": "{ ...structured result rendered as text... }" }
    ],
    "structuredContent": {
      "McpServerVersion": "0.6.0",
      "PartioServerHealthy": true,
      "ProtocolVersion": "2025-06-18",
      "Tools": ["partio_capabilities", "partio_enumerate_completion_endpoints", "..."]
    }
  }
}
```

## Enumerating objects

Endpoints can be numerous, so the enumerate tools never dump the whole collection. They return a **bounded summary list** and a cursor, and you follow an **enumerate → get** discipline: enumerate to discover ids and summaries, then get a single object by id when you need its full definition.

Every enumerate tool accepts three arguments:

- `maxResults` — page size. It is **clamped server-side** to a default cap of `100`. Ask for more and you get `100`; ask for a huge number and you still get `100`.
- `continuationToken` — the paging cursor returned by the previous page. Omit it for the first page.
- `search` — an optional name filter.

Each result carries the summary list plus `ContinuationToken`, `TotalCount`, and `HasMore`. When `HasMore` is `true`, pass the returned `ContinuationToken` back to fetch the next page.

A two-page walk over completion endpoints:

```json
// --> page 1
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "partio_enumerate_completion_endpoints",
    "arguments": { "maxResults": 2 }
  }
}
```

```json
// <-- page 1
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "structuredContent": {
      "Items": [
        { "Id": "cep_abc", "Name": "Summarizer", "Model": "gemma3:4b" },
        { "Id": "cep_def", "Name": "Rewriter",  "Model": "llama3:8b" }
      ],
      "ContinuationToken": "eyJvIjoyfQ==",
      "TotalCount": 5,
      "HasMore": true
    }
  }
}
```

```json
// --> page 2 (pass the token back)
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "partio_enumerate_completion_endpoints",
    "arguments": { "maxResults": 2, "continuationToken": "eyJvIjoyfQ==" }
  }
}
```

Then, to act on one of them, get it by id:

```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "tools/call",
  "params": {
    "name": "partio_get_completion_endpoint",
    "arguments": { "id": "cep_abc" }
  }
}
```

The `get` result includes the full endpoint object, `MaxConcurrentRequests` and `MaxQueueDepth` among the fields — values the summary list omits.

## Tools

Fourteen tools are registered. All of them require authentication (subject to the bypass rules above); none is anonymous.

| Tool | Purpose | Auth |
|---|---|---|
| `partio_capabilities` | Report the MCP server version, connected Partio server health, MCP protocol version, and the list of available tools. | Yes |
| `partio_enumerate_completion_endpoints` | List completion (inference) endpoints as a bounded summary list; supports `maxResults`, `continuationToken`, `search`. | Yes |
| `partio_get_completion_endpoint` | Fetch one completion endpoint by id, including `MaxConcurrentRequests` and `MaxQueueDepth`. | Yes |
| `partio_create_completion_endpoint` | Create a completion endpoint from a full definition, including `MaxConcurrentRequests` and `MaxQueueDepth`. | Yes |
| `partio_update_completion_endpoint` | Update a completion endpoint by id from a full definition, including `MaxConcurrentRequests` and `MaxQueueDepth`. | Yes |
| `partio_delete_completion_endpoint` | Delete a completion endpoint by id. | Yes |
| `partio_enumerate_embedding_endpoints` | List embedding endpoints as a bounded summary list; supports `maxResults`, `continuationToken`, `search`. | Yes |
| `partio_get_embedding_endpoint` | Fetch one embedding endpoint by id, including `MaxConcurrentRequests` and `MaxQueueDepth`. | Yes |
| `partio_create_embedding_endpoint` | Create an embedding endpoint from a full definition, including `MaxConcurrentRequests` and `MaxQueueDepth`. | Yes |
| `partio_update_embedding_endpoint` | Update an embedding endpoint by id from a full definition, including `MaxConcurrentRequests` and `MaxQueueDepth`. | Yes |
| `partio_delete_embedding_endpoint` | Delete an embedding endpoint by id. | Yes |
| `partio_summarize` | Summarize text through a completion endpoint (no chunking or embedding). | Yes |
| `partio_chunk` | Chunk a semantic cell into text chunks without embedding them. | Yes |
| `partio_embed` | Embed one or more input strings through an embedding endpoint without chunking. | Yes |

The create and update tools — for both completion and embedding endpoints — accept the **full endpoint definition**. That includes the concurrency controls: **`MaxConcurrentRequests`** (clamped `>= 1`, default `2`) caps how many upstream provider calls run at once, and **`MaxQueueDepth`** (clamped `>= 0`, default `0`) sets how many further requests may wait for a slot before the endpoint sheds load with `429`.

## Client configuration

You can wire a harness up by hand, or let `partio-mcp mcp install` do it. The install command detects supported harnesses and writes the right config for each; `partio-mcp mcp install --dry-run` prints exactly what it would change and writes nothing. The per-harness details:

| Harness | Config file | Entry | Manual CLI |
|---|---|---|---|
| **Claude Code** | `~/.claude.json` (also writes `~/.claude/agents/partio.md`) | `mcpServers.partio = { "type": "http", "url": "http://localhost:8500/mcp" }` | `claude mcp add --transport http --scope user partio http://localhost:8500/mcp` |
| **Codex** | managed by Codex | (stdio bridge) | `codex mcp add partio -- partio-mcp mcp stdio` |
| **Gemini** | managed by Gemini | (added via CLI) | `gemini mcp add --scope user --transport http partio http://localhost:8500/mcp` |
| **Cursor** | `<CWD>/.cursor/mcp.json` | `mcpServers.partio = { "url": "http://localhost:8500/mcp", "transport": "http" }` | edit `.cursor/mcp.json` directly |
| **Mux** | `<MUX_CONFIG_DIR or ~/.mux>/mcp-servers.json` | element in `servers`: `{ "name": "partio", "transport": "http", "url": "http://localhost:8500", "mcpPath": "/mcp" }` | edit `mcp-servers.json` directly |

Notes:

- **Codex** connects over the stdio bridge rather than HTTP; the CLI command registers `partio-mcp mcp stdio` as the server process.
- **Mux** is configured only when it is detected — either `~/.mux` exists or `mux` is on your `PATH`. Mux's HTTP MCP client has no per-server auth field, so if your server requires a bearer token, use the stdio bridge instead of the HTTP entry.
- Paste-ready per-harness guides live in [`docs/`](docs/): `INSTRUCTIONS_FOR_CLAUDE_CODE.md`, `INSTRUCTIONS_FOR_CODEX.md`, `INSTRUCTIONS_FOR_GEMINI.md`, `INSTRUCTIONS_FOR_CURSOR.md`, and `INSTRUCTIONS_FOR_MUX.md`.

## Transport

By default the server binds to `localhost:8500`. The base URL is therefore `http://localhost:8500`, and the MCP Streamable HTTP endpoint is `http://localhost:8500/mcp`. Host and port are configurable (see below).

Three surfaces are exposed on that port:

- `/mcp` — MCP Streamable HTTP (the endpoint harnesses connect to).
- `/rpc` — plain JSON-RPC 2.0.
- `/events` — Server-Sent Events stream.

For harnesses that prefer a subprocess to an HTTP connection, `partio-mcp mcp stdio` runs a **stdio ↔ HTTP JSON-RPC bridge**: the harness speaks JSON-RPC over the process's stdin/stdout, and the bridge relays each call to the HTTP server. This is what Codex uses, and it is the recommended path anywhere you need a bearer token but the harness has no field to supply one.

### Configuration and precedence

Configuration is read from a JSON file `partio.mcp.json` (override the path with `--config=<path>`), and each value can be overridden by an environment variable:

| Setting | Env var |
|---|---|
| Partio server endpoint | `PARTIO_ENDPOINT` |
| Partio API key | `PARTIO_API_KEY` |
| MCP bind host | `PARTIO_MCP_HOST` |
| MCP bind port | `PARTIO_MCP_PORT` |

Environment variables win over the file. Run `partio-mcp --showconfig` to print the effective configuration after precedence is applied, and `partio-mcp --help` for usage.

### Command reference

| Command | What it does |
|---|---|
| `partio-mcp` | Run the MCP HTTP server. |
| `partio-mcp mcp stdio` | Run the stdio ↔ HTTP JSON-RPC bridge. |
| `partio-mcp mcp install` | Detect supported harnesses and write their MCP configuration. |
| `partio-mcp mcp install --dry-run` | Print the install plan without writing anything. |
| `partio-mcp mcp remove` | Remove the Partio MCP configuration from supported harnesses. |
| `partio-mcp --showconfig` | Print the effective configuration. |
| `partio-mcp --help` | Print usage. |
