<!-- Paste this into your Mux system prompt / rules to teach the agent how to use Partio over MCP. -->

# Using the Partio MCP server (Mux)

## Connecting

There are two ways to connect Mux to Partio. Pick based on whether the server requires a bearer token.

**Option A — HTTP entry in `mcp-servers.json`.** Add Partio to Mux's MCP config file, at `<MUX_CONFIG_DIR>/mcp-servers.json` (or `~/.mux/mcp-servers.json` if `MUX_CONFIG_DIR` is unset). Partio is an element in the `servers` array:

```json
{
  "servers": [
    {
      "name": "partio",
      "transport": "http",
      "url": "http://localhost:8500",
      "mcpPath": "/mcp"
    }
  ]
}
```

Note that Mux's HTTP MCP client has **no per-server auth field**. If your Partio MCP server requires a bearer token, this option cannot supply it — use Option B instead.

**Option B — stdio bridge.** Run Partio through its stdio bridge, `partio-mcp mcp stdio`, and register that command as a stdio MCP server in Mux. The bridge holds the credential itself (via `partio.mcp.json` or the `PARTIO_*` environment variables), so this is the path to use whenever authentication is on.

The Partio installer configures Mux automatically, but only when Mux is detected — that is, when `~/.mux` exists or `mux` is on your `PATH`:

```bash
partio-mcp mcp install
```

After connecting, verify Mux can see the Partio tools:

```bash
mux probe --output-format json --require-tools
```

---

## How to use Partio's MCP tools

Partio exposes fourteen tools over MCP. They fall into two groups: **endpoint management** (configure the embedding and inference endpoints Partio calls) and **inference** (run chunking, embedding, and summarization).

### Start here

Call `partio_capabilities` first when you are unsure of the state of things. It returns the MCP server version, whether the connected Partio server is healthy, the MCP protocol version, and the list of tools you can call. It is the cheapest way to confirm you are actually connected before doing real work.

### Managing endpoints

There are two parallel families of tools, one for **completion (inference)** endpoints and one for **embedding** endpoints. Each family has the same five operations:

- `partio_enumerate_completion_endpoints` / `partio_enumerate_embedding_endpoints` — list endpoints as a bounded summary.
- `partio_get_completion_endpoint` / `partio_get_embedding_endpoint` — fetch one full endpoint by id.
- `partio_create_completion_endpoint` / `partio_create_embedding_endpoint` — create one from a full definition.
- `partio_update_completion_endpoint` / `partio_update_embedding_endpoint` — update one by id.
- `partio_delete_completion_endpoint` / `partio_delete_embedding_endpoint` — delete one by id.

**Follow an enumerate → get discipline.** Enumerate returns summaries, not full objects. When you need an endpoint's complete definition — or before you update it — call the matching `get` with the id from the summary. Do not assume the fields you need are in the enumerate result; they often are not.

**Paging.** Enumerate tools accept `maxResults`, `continuationToken`, and `search`:

- `maxResults` is **clamped server-side** to a cap of `100`. Requesting more returns `100`, not an error.
- `continuationToken` is the cursor. Omit it for the first page; when the result reports `HasMore: true`, pass the returned `ContinuationToken` back to get the next page.
- `search` filters by name.

Iterate pages until `HasMore` is `false` rather than assuming everything fit on one page.

**Concurrency controls on create/update.** The create and update tools accept the **full endpoint definition**, and that includes two throttling fields you should set deliberately:

- **`MaxConcurrentRequests`** — the maximum number of upstream provider calls Partio runs at once for this endpoint. Clamped to `>= 1`; default `2`.
- **`MaxQueueDepth`** — how many additional requests may wait for a free slot once `MaxConcurrentRequests` is saturated. Clamped to `>= 0`; default `0`, which means over-limit requests are rejected immediately with `429` instead of queuing.

When you update an endpoint, send the whole definition, not just the field you changed, and preserve `MaxConcurrentRequests` and `MaxQueueDepth` unless you intend to change them — fetch the current object with `get` first so you are updating from a complete, current definition.

### Running inference

Three tools operate on content directly:

- `partio_summarize` — summarize text through a completion endpoint. No chunking, no embedding.
- `partio_chunk` — split a semantic cell into text chunks, without embedding them.
- `partio_embed` — embed one or more input strings through an embedding endpoint, without chunking.

These take an endpoint id (or the inputs) as arguments; use the enumerate/get tools first if you need to discover which endpoint to target.

### Results

Every `tools/call` returns **structured content** — a typed object you can read directly, not prose you have to parse. Read the structured fields rather than scraping the text rendering.
