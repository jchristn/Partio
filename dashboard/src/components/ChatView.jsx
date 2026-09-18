import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import FormFieldLabel from './FormFieldLabel';
import Tooltip from './Tooltip';
import './ChatView.css';

// Convert a form string to a typed number, or undefined when blank.
function num(v) {
  if (v === '' || v === null || v === undefined) return undefined;
  const n = Number(v);
  return Number.isFinite(n) ? n : undefined;
}

// Nanoseconds (Ollama durations) to milliseconds.
function nsToMs(ns) {
  return typeof ns === 'number' ? ns / 1e6 : undefined;
}

// Build the native provider request for a chat turn, based on the endpoint's dialect (ApiFormat) and the
// configured generation parameters. The request is massaged so the provider returns token usage/timing:
// OpenAI/vLLM streaming adds stream_options.include_usage; Ollama and Gemini return counts by default.
function buildChatRequest(apiFormat, model, messages, systemPrompt, params) {
  const fmt = (apiFormat || '').toLowerCase();
  const temperature = num(params.temperature);
  const topP = num(params.topP);
  const maxTokens = num(params.maxTokens);
  const stream = !!params.stream;
  const withSystem = (arr) => (systemPrompt && systemPrompt.trim())
    ? [{ role: 'system', content: systemPrompt }, ...arr]
    : arr;

  if (fmt === 'ollama') {
    const options = {};
    if (temperature !== undefined) options.temperature = temperature;
    if (topP !== undefined) options.top_p = topP;
    if (maxTokens !== undefined) options.num_predict = maxTokens;
    const body = { model, messages: withSystem(messages), stream };
    if (Object.keys(options).length) body.options = options;
    return { subpath: 'api/chat', body, canStream: true };
  }

  if (fmt === 'gemini') {
    const contents = messages.map(m => ({
      role: m.role === 'assistant' ? 'model' : 'user',
      parts: [{ text: m.content }]
    }));
    const generationConfig = {};
    if (temperature !== undefined) generationConfig.temperature = temperature;
    if (topP !== undefined) generationConfig.topP = topP;
    if (maxTokens !== undefined) generationConfig.maxOutputTokens = maxTokens;
    const body = { contents };
    if (systemPrompt && systemPrompt.trim()) body.systemInstruction = { parts: [{ text: systemPrompt }] };
    if (Object.keys(generationConfig).length) body.generationConfig = generationConfig;
    // Gemini streams via :streamGenerateContent with ?alt=sse (server-sent events); the proxy relays it
    // verbatim. Fall back to the buffered :generateContent when streaming is off.
    const op = stream ? 'streamGenerateContent?alt=sse' : 'generateContent';
    return { subpath: `v1beta/models/${model}:${op}`, body, canStream: true };
  }

  // openai, vllm
  const body = { model, messages: withSystem(messages), stream };
  if (temperature !== undefined) body.temperature = temperature;
  if (topP !== undefined) body.top_p = topP;
  if (maxTokens !== undefined) body.max_tokens = maxTokens;
  if (stream) body.stream_options = { include_usage: true };
  return { subpath: 'v1/chat/completions', body, canStream: true };
}

// Assistant reply text from a buffered native provider response.
function parseChatReply(apiFormat, json) {
  const fmt = (apiFormat || '').toLowerCase();
  if (fmt === 'ollama') return json?.message?.content ?? '';
  if (fmt === 'gemini') {
    const parts = json?.candidates?.[0]?.content?.parts;
    return Array.isArray(parts) ? parts.map(p => p?.text ?? '').join('') : '';
  }
  return json?.choices?.[0]?.message?.content ?? '';
}

// Token usage / timing from a buffered native provider response.
function extractMetrics(apiFormat, json) {
  const fmt = (apiFormat || '').toLowerCase();
  if (fmt === 'ollama') {
    return {
      promptTokens: json?.prompt_eval_count,
      outputTokens: json?.eval_count,
      loadMs: nsToMs(json?.load_duration),
      promptEvalMs: nsToMs(json?.prompt_eval_duration),
      evalMs: nsToMs(json?.eval_duration),
      totalDurationMs: nsToMs(json?.total_duration)
    };
  }
  if (fmt === 'gemini') {
    const u = json?.usageMetadata;
    return {
      promptTokens: u?.promptTokenCount,
      outputTokens: u?.candidatesTokenCount,
      totalTokens: u?.totalTokenCount,
      cachedTokens: u?.cachedContentTokenCount
    };
  }
  const u = json?.usage;
  return {
    promptTokens: u?.prompt_tokens,
    outputTokens: u?.completion_tokens,
    totalTokens: u?.total_tokens,
    cachedTokens: u?.prompt_tokens_details?.cached_tokens
  };
}

// Stateful incremental parser for streamed responses. Accumulates the text delta and, from the final
// chunk, token usage/timing into `metrics`.
function createStreamParser(apiFormat) {
  const fmt = (apiFormat || '').toLowerCase();
  let buffer = '';
  const metrics = {};
  function feed(chunk) {
    buffer += chunk;
    const lines = buffer.split('\n');
    buffer = lines.pop() ?? '';
    let out = '';
    for (const raw of lines) {
      const line = raw.trim();
      if (!line) continue;
      if (fmt === 'ollama') {
        try {
          const j = JSON.parse(line);
          out += j?.message?.content ?? '';
          if (j?.done) {
            metrics.promptTokens = j.prompt_eval_count;
            metrics.outputTokens = j.eval_count;
            metrics.loadMs = nsToMs(j.load_duration);
            metrics.promptEvalMs = nsToMs(j.prompt_eval_duration);
            metrics.evalMs = nsToMs(j.eval_duration);
            metrics.totalDurationMs = nsToMs(j.total_duration);
          }
        } catch { /* partial line */ }
      } else if (fmt === 'gemini') {
        // Gemini SSE (alt=sse): each event is a JSON chunk with candidates[].content.parts[].text.
        if (!line.startsWith('data:')) continue;
        const payload = line.slice(5).trim();
        try {
          const j = JSON.parse(payload);
          const parts = j?.candidates?.[0]?.content?.parts;
          if (Array.isArray(parts)) out += parts.map(p => p?.text ?? '').join('');
          if (j?.usageMetadata) {
            metrics.promptTokens = j.usageMetadata.promptTokenCount;
            metrics.outputTokens = j.usageMetadata.candidatesTokenCount;
            metrics.totalTokens = j.usageMetadata.totalTokenCount;
            metrics.cachedTokens = j.usageMetadata.cachedContentTokenCount;
          }
        } catch { /* partial line */ }
      } else {
        if (!line.startsWith('data:')) continue;
        const payload = line.slice(5).trim();
        if (payload === '[DONE]') continue;
        try {
          const j = JSON.parse(payload);
          out += j?.choices?.[0]?.delta?.content ?? '';
          if (j?.usage) {
            metrics.promptTokens = j.usage.prompt_tokens;
            metrics.outputTokens = j.usage.completion_tokens;
            metrics.totalTokens = j.usage.total_tokens;
            metrics.cachedTokens = j.usage.prompt_tokens_details?.cached_tokens;
          }
        } catch { /* partial line */ }
      }
    }
    return out;
  }
  return { feed, metrics };
}

function fmtMs(ms) {
  if (ms === undefined || ms === null) return null;
  return ms >= 1000 ? (ms / 1000).toFixed(2) + ' s' : Math.round(ms) + ' ms';
}

function MetricsPopover({ m }) {
  const rows = [
    ['Time to first token', fmtMs(m.ttftMs)],
    ['Total time', fmtMs(m.totalMs)],
    ['Prompt tokens', m.promptTokens],
    ['Output tokens', m.outputTokens],
    ['Total tokens', m.totalTokens],
    ['Cached tokens', m.cachedTokens],
    ['Model load', fmtMs(m.loadMs)],
    ['Prompt eval', fmtMs(m.promptEvalMs)],
    ['Generation', fmtMs(m.evalMs)],
    ['Upstream total', fmtMs(m.totalDurationMs)]
  ].filter(([, v]) => v !== null && v !== undefined);
  return (
    <div className="chat-metrics">
      {rows.map(([k, v]) => (
        <div className="chat-metric-row" key={k}><span>{k}</span><strong>{v}</strong></div>
      ))}
    </div>
  );
}

const DEFAULT_PARAMS = { stream: true, temperature: '', topP: '', maxTokens: '' };

export default function ChatView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);

  const [endpoints, setEndpoints] = useState([]);
  const [loadingEndpoints, setLoadingEndpoints] = useState(true);
  const [endpointId, setEndpointId] = useState('');
  const [systemPrompt, setSystemPrompt] = useState('You are a helpful, concise assistant.');
  const [params, setParams] = useState(DEFAULT_PARAMS);
  const [showParams, setShowParams] = useState(false);
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [streamingActive, setStreamingActive] = useState(false);
  const [error, setError] = useState(null);
  const scrollRef = useRef(null);
  const abortRef = useRef(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await api.enumerateCompletionEndpoints({ MaxResults: 1000 });
        if (cancelled) return;
        const list = (res?.Data || []).filter(e => e.Active !== false);
        setEndpoints(list);
        setEndpointId(prev => prev || list[0]?.Id || '');
      } catch (err) {
        if (!cancelled) { setEndpoints([]); setError(err.message); }
      } finally {
        if (!cancelled) setLoadingEndpoints(false);
      }
    })();
    return () => { cancelled = true; };
  }, [serverUrl, bearerToken]);

  const activeEndpoint = useMemo(
    () => endpoints.find(e => e.Id === endpointId) || null,
    [endpoints, endpointId]
  );

  useEffect(() => {
    if (scrollRef.current) scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
  }, [messages, sending]);

  const setParam = (key, value) => setParams(prev => ({ ...prev, [key]: value }));

  const patchLast = (patch) => {
    setMessages(prev => {
      const copy = [...prev];
      const last = copy[copy.length - 1];
      copy[copy.length - 1] = typeof patch === 'function' ? patch(last) : { ...last, ...patch };
      return copy;
    });
  };

  const stop = () => {
    if (abortRef.current) abortRef.current.abort();
  };

  const send = async () => {
    const text = input.trim();
    if (!text || !activeEndpoint || sending) return;

    setError(null);
    const history = [...messages, { role: 'user', content: text }];
    setMessages(history);
    setInput('');
    setSending(true);

    const controller = new AbortController();
    abortRef.current = controller;
    const started = performance.now();
    let firstTokenAt = null;

    try {
      const model = activeEndpoint.Model;
      const { subpath, body, canStream } = buildChatRequest(activeEndpoint.ApiFormat, model, history, systemPrompt, params);
      const useStream = !!params.stream && canStream;

      if (useStream) {
        setStreamingActive(true);
        setMessages(prev => [...prev, { role: 'assistant', content: '' }]);
        const parser = createStreamParser(activeEndpoint.ApiFormat);
        const res = await api.proxyStream(endpointId, subpath, {
          method: 'POST',
          body,
          signal: controller.signal,
          onChunk: (chunkText) => {
            const delta = parser.feed(chunkText);
            if (delta) {
              if (firstTokenAt === null) firstTokenAt = performance.now();
              patchLast(last => ({ ...last, content: last.content + delta }));
            }
          }
        });
        setStreamingActive(false);

        if (res.statusCode < 200 || res.statusCode >= 300) {
          patchLast({ role: 'error', content: `Upstream ${res.statusCode}: ${res.body || ''}` });
          return;
        }
        const metrics = {
          ...parser.metrics,
          ttftMs: firstTokenAt ? firstTokenAt - started : undefined,
          totalMs: performance.now() - started
        };
        patchLast(last => ({
          role: 'assistant',
          content: last.content || '(empty response)',
          metrics
        }));
        return;
      }

      // Buffered path
      const result = await api.proxy(endpointId, subpath, { method: 'POST', body, signal: controller.signal });
      if (result.statusCode < 200 || result.statusCode >= 300) {
        setMessages(prev => [...prev, { role: 'error', content: `Upstream ${result.statusCode}: ${result.body}` }]);
        return;
      }
      let reply = '';
      let metrics = {};
      try {
        const json = JSON.parse(result.body);
        reply = parseChatReply(activeEndpoint.ApiFormat, json);
        metrics = extractMetrics(activeEndpoint.ApiFormat, json);
      } catch { reply = ''; }
      if (!reply) reply = result.body || '(empty response)';
      metrics.totalMs = performance.now() - started;
      setMessages(prev => [...prev, { role: 'assistant', content: reply, metrics }]);
    } catch (err) {
      setStreamingActive(false);
      if (err?.name === 'AbortError') {
        // Keep whatever streamed so far; mark it stopped.
        patchLast(last => (last && last.role === 'assistant')
          ? { ...last, content: (last.content || '') + '\n\n⏹ stopped', stopped: true }
          : last);
      } else {
        setMessages(prev => [...prev, { role: 'error', content: err.message }]);
      }
    } finally {
      abortRef.current = null;
      setSending(false);
    }
  };

  const onKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); if (!sending) send(); }
  };

  const clearConversation = () => { setMessages([]); setError(null); };

  return (
    <div className="chat-view">
      <div className="header-row">
        <div className="page-title-block">
          <h2>Chat</h2>
          <p className="view-subtitle">Hold a live conversation with a configured inference endpoint. Messages are relayed through the Partio proxy in the endpoint's native provider format — no provider key needed client-side.</p>
        </div>
      </div>

      <div className="chat-controls card">
        <div className="chat-controls-row">
          <div className="chat-endpoint-select">
            <FormFieldLabel text="Inference Endpoint" tooltip="The completion endpoint to chat with. Its ApiFormat determines the native chat API the proxy relays to." />
            <select value={endpointId} onChange={e => setEndpointId(e.target.value)} disabled={loadingEndpoints}>
              <option value="">{loadingEndpoints ? 'Loading...' : '-- Select endpoint --'}</option>
              {endpoints.map(e => (
                <option key={e.Id} value={e.Id}>{(e.Name || e.Model)} ({e.ApiFormat})</option>
              ))}
            </select>
          </div>
          {activeEndpoint && (
            <div className="chat-endpoint-summary">
              <div><span>Model</span><strong>{activeEndpoint.Model}</strong></div>
              <div><span>Provider</span><strong>{activeEndpoint.ApiFormat}</strong></div>
              <div><span>Base URL</span><code>{activeEndpoint.Endpoint}</code></div>
            </div>
          )}
          <button className="chat-params-toggle" onClick={() => setShowParams(v => !v)}>
            {showParams ? '▾' : '▸'} Parameters
          </button>
        </div>

        {showParams && (
          <div className="chat-params">
            <div className="form-group">
              <FormFieldLabel text="System Prompt" tooltip="Optional instruction applied before the conversation. Sent as a system message (Ollama/OpenAI) or systemInstruction (Gemini)." />
              <textarea rows={2} value={systemPrompt} onChange={e => setSystemPrompt(e.target.value)} placeholder="Optional system prompt..." />
            </div>
            <div className="chat-params-grid">
              <div className="checkbox-group">
                <input id="chat-stream" type="checkbox" checked={params.stream} onChange={e => setParam('stream', e.target.checked)} />
                <Tooltip content="Stream tokens as they are generated. Ollama (NDJSON), OpenAI/vLLM (SSE), and Gemini (SSE via :streamGenerateContent) are all supported through the proxy.">
                  <label htmlFor="chat-stream">Stream</label>
                </Tooltip>
              </div>
              <div className="form-group">
                <FormFieldLabel text="Temperature" tooltip="Sampling temperature. Blank = provider default." />
                <input type="number" step="0.1" min="0" max="2" value={params.temperature} onChange={e => setParam('temperature', e.target.value)} placeholder="default" />
              </div>
              <div className="form-group">
                <FormFieldLabel text="Top P" tooltip="Nucleus sampling. Blank = provider default." />
                <input type="number" step="0.05" min="0" max="1" value={params.topP} onChange={e => setParam('topP', e.target.value)} placeholder="default" />
              </div>
              <div className="form-group">
                <FormFieldLabel text="Max Tokens" tooltip="Maximum tokens to generate. Blank = provider default." />
                <input type="number" step="1" min="1" value={params.maxTokens} onChange={e => setParam('maxTokens', e.target.value)} placeholder="default" />
              </div>
            </div>
          </div>
        )}
      </div>

      {error && <div className="card chat-error">Error: {error}</div>}

      <div className="chat-conversation card">
        <div className="chat-messages" ref={scrollRef}>
          {messages.length === 0 && !sending && (
            <div className="chat-empty">Start the conversation — your messages are relayed live through the selected endpoint.</div>
          )}
          {messages.map((m, i) => (
            <div key={i} className={`chat-bubble chat-${m.role}`}>
              <div className="chat-role">
                <span>{m.role === 'user' ? 'You' : m.role === 'assistant' ? (activeEndpoint?.Model || 'Assistant') : 'Error'}</span>
                {m.role === 'assistant' && m.metrics && (
                  <Tooltip content={<MetricsPopover m={m.metrics} />}>
                    <span className="chat-info" aria-label="Response metrics">&#9432;</span>
                  </Tooltip>
                )}
              </div>
              <div className="chat-content">{m.content}</div>
            </div>
          ))}
          {sending && !streamingActive && (
            <div className="chat-bubble chat-assistant">
              <div className="chat-role"><span>{activeEndpoint?.Model || 'Assistant'}</span></div>
              <div className="chat-content chat-typing"><span></span><span></span><span></span></div>
            </div>
          )}
        </div>

        <div className="chat-composer">
          <textarea
            className="chat-input"
            rows={3}
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={onKeyDown}
            placeholder={activeEndpoint ? 'Type a message… (Enter to send, Shift+Enter for newline)' : 'Select an endpoint to begin…'}
            disabled={!activeEndpoint || loadingEndpoints}
          />
          <div className="chat-composer-actions">
            {sending ? (
              <button className="danger" onClick={stop}>Stop</button>
            ) : (
              <button className="primary" onClick={send} disabled={!activeEndpoint || !input.trim()}>Send</button>
            )}
            <button className="secondary" onClick={clearConversation} disabled={sending || messages.length === 0}>Clear</button>
          </div>
          <div className="chat-disclaimer">AI can make mistakes. Check all answers.</div>
        </div>
      </div>
    </div>
  );
}
