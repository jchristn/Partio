<!-- Paste this into your Gemini system prompt / config to teach the agent how to use Partio over MCP. -->

# Using the Partio MCP server (Gemini)

## Connecting

Add the Partio MCP server to Gemini over HTTP with the Gemini CLI:

```bash
gemini mcp add --scope user --transport http partio http://localhost:8500/mcp
```

That registers `partio` as a user-scoped HTTP MCP server pointing at the Partio MCP endpoint. The Partio installer can do the same step for you:

```bash
partio-mcp mcp install
```

If the server requires a bearer token, supply it through Gemini's mechanism for HTTP MCP auth headers (`Authorization: Bearer <token>`). Unauthenticated requests are rejected with `401` before any tool runs. After adding the server, confirm the Partio tools appear in Gemini's tool list.

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
