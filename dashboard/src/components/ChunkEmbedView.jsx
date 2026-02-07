import React, { useState, useEffect } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import { copyToClipboard } from '../utils/clipboard';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import './ChunkEmbedView.css';

export default function ChunkEmbedView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [endpoints, setEndpoints] = useState([]);
  const [endpointsLoading, setEndpointsLoading] = useState(true);
  const [copiedChunk, setCopiedChunk] = useState(null);
  const [expandedEmbeddings, setExpandedEmbeddings] = useState(new Set());

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await api.enumerateEndpoints();
        if (!cancelled && res && res.Data) {
          setEndpoints(res.Data.filter(ep => ep.Active !== false));
        }
      } catch (err) {
        if (!cancelled) setEndpoints([]);
      }
      if (!cancelled) setEndpointsLoading(false);
    })();
    return () => { cancelled = true; };
  }, [serverUrl, bearerToken]);

  const [form, setForm] = useState({
    Type: 'Text',
    Text: '',
    UnorderedList: '',
    OrderedList: '',
    Strategy: 'FixedTokenCount',
    FixedTokenCount: 256,
    OverlapCount: 0,
    OverlapStrategy: 'SlidingWindow',
    ContextPrefix: '',
    EndpointId: '',
    L2Normalization: false,
    Labels: [],
    Tags: {}
  });

  const handleSubmit = async () => {
    setLoading(true);
    setError(null);
    setResult(null);

    const request = {
      Type: form.Type,
      Text: form.Type === 'Text' ? form.Text : null,
      UnorderedList: form.Type === 'List' && form.UnorderedList ? form.UnorderedList.split('\n').filter(Boolean) : null,
      OrderedList: form.Type === 'List' && form.OrderedList ? form.OrderedList.split('\n').filter(Boolean) : null,
      ChunkingConfiguration: {
        Strategy: form.Strategy,
        FixedTokenCount: parseInt(form.FixedTokenCount) || 256,
        OverlapCount: parseInt(form.OverlapCount) || 0,
        OverlapStrategy: form.OverlapStrategy,
        ContextPrefix: form.ContextPrefix || null
      },
      EmbeddingConfiguration: {
        L2Normalization: form.L2Normalization
      },
      Labels: form.Labels.length > 0 ? form.Labels : null,
      Tags: Object.keys(form.Tags).length > 0 ? form.Tags : null
    };

    try {
      const res = await api.process(form.EndpointId, request);
      setResult(res);
    } catch (err) {
      setError(err.message);
    }
    setLoading(false);
  };

  const update = (key, value) => setForm(prev => ({ ...prev, [key]: value }));

  return (
    <div className="chunk-embed">
      <div className="header-row"><h2>Process Cells</h2></div>
      <div className="chunk-embed-layout">
        <div className="chunk-embed-form card">
          <div className="form-group">
            <label>Atom Type</label>
            <select value={form.Type} onChange={e => update('Type', e.target.value)}>
              <option value="Text">Text</option>
              <option value="List">List</option>
            </select>
          </div>

          {form.Type === 'Text' && (
            <div className="form-group">
              <label>Text Content</label>
              <textarea rows={8} value={form.Text} onChange={e => update('Text', e.target.value)} placeholder="Enter text to chunk and embed..." />
            </div>
          )}

          {form.Type === 'List' && (
            <div className="form-group">
              <label>List Items (one per line)</label>
              <textarea rows={8} value={form.UnorderedList} onChange={e => update('UnorderedList', e.target.value)} placeholder="Item 1&#10;Item 2&#10;Item 3" />
            </div>
          )}

          <div className="form-group">
            <label>Chunking Strategy</label>
            <select value={form.Strategy} onChange={e => update('Strategy', e.target.value)}>
              <option value="FixedTokenCount">Fixed Token Count</option>
              <option value="SentenceBased">Sentence Based</option>
              <option value="ParagraphBased">Paragraph Based</option>
              <option value="WholeList">Whole List</option>
              <option value="ListEntry">List Entry</option>
            </select>
          </div>

          <div className="form-row">
            <div className="form-group"><label>Token Count</label><input type="number" value={form.FixedTokenCount} onChange={e => update('FixedTokenCount', e.target.value)} /></div>
            <div className="form-group"><label>Overlap</label><input type="number" value={form.OverlapCount} onChange={e => update('OverlapCount', e.target.value)} /></div>
          </div>

          <div className="form-group">
            <label>Overlap Strategy</label>
            <select value={form.OverlapStrategy} onChange={e => update('OverlapStrategy', e.target.value)}>
              <option value="SlidingWindow">Sliding Window</option>
              <option value="SentenceBoundaryAware">Sentence Boundary Aware</option>
              <option value="SemanticBoundaryAware">Semantic Boundary Aware</option>
            </select>
          </div>

          <div className="form-group"><label>Context Prefix</label><input value={form.ContextPrefix} onChange={e => update('ContextPrefix', e.target.value)} placeholder="Optional prefix prepended to each chunk" /></div>

          <div className="form-row">
            <div className="form-group">
              <label>Embedding Endpoint</label>
              <select value={form.EndpointId} onChange={e => update('EndpointId', e.target.value)} disabled={endpointsLoading}>
                <option value="">{endpointsLoading ? 'Loading...' : '-- Select endpoint --'}</option>
                {endpoints.map(ep => (
                  <option key={ep.Id} value={ep.Id}>{ep.Model} ({ep.Id})</option>
                ))}
              </select>
            </div>
            <div className="checkbox-group">
              <input type="checkbox" checked={form.L2Normalization} onChange={e => update('L2Normalization', e.target.checked)} id="l2norm" />
              <label htmlFor="l2norm">L2 Normalize</label>
            </div>
          </div>

          <div className="form-group"><label>Labels</label><TagInput value={form.Labels} onChange={v => update('Labels', v)} /></div>
          <div className="form-group"><label>Tags</label><KeyValueEditor value={form.Tags} onChange={v => update('Tags', v)} /></div>

          <button className="primary" onClick={handleSubmit} disabled={loading || !form.EndpointId} style={{ marginTop: 12 }}>
            {loading ? 'Processing...' : 'Process'}
          </button>
        </div>

        <div className="chunk-embed-results">
          {error && <div className="card" style={{ color: 'var(--danger-color)' }}>Error: {error}</div>}
          {result && (
            <div className="card">
              <h3>Results</h3>
              <p>Total Chunks: {result.Chunks ? result.Chunks.length : 0}</p>
              {result.Text && (
                <div className="chunk-field" style={{ marginBottom: 12 }}>
                  <strong>Input Text:</strong> {result.Text.length > 200 ? result.Text.slice(0, 200) + '...' : result.Text}
                </div>
              )}
              {result.Chunks && result.Chunks.map((chunk, i) => (
                <div key={i} className="chunk-result">
                  <div className="chunk-header">
                    <span>Chunk {i + 1}</span>
                    <button
                      type="button"
                      className={`chunk-copy-btn ${copiedChunk === i ? 'copied' : ''}`}
                      onClick={async () => {
                        const success = await copyToClipboard(JSON.stringify(chunk, null, 2));
                        if (success) {
                          setCopiedChunk(i);
                          setTimeout(() => setCopiedChunk(null), 1500);
                        }
                      }}
                      title={copiedChunk === i ? 'Copied!' : 'Copy chunk JSON'}
                    >
                      {copiedChunk === i ? (
                        <svg width="14" height="14" viewBox="0 0 20 20" fill="currentColor">
                          <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                        </svg>
                      ) : (
                        <svg width="14" height="14" viewBox="0 0 20 20" fill="currentColor">
                          <path d="M8 3a1 1 0 011-1h2a1 1 0 110 2H9a1 1 0 01-1-1z" />
                          <path d="M6 3a2 2 0 00-2 2v11a2 2 0 002 2h8a2 2 0 002-2V5a2 2 0 00-2-2 3 3 0 01-3 3H9a3 3 0 01-3-3z" />
                        </svg>
                      )}
                    </button>
                  </div>
                  <div className="chunk-field"><strong>Text:</strong> {chunk.Text}</div>
                  {chunk.Labels && chunk.Labels.length > 0 && (
                    <div className="chunk-field"><strong>Labels:</strong> {chunk.Labels.join(', ')}</div>
                  )}
                  {chunk.Tags && Object.keys(chunk.Tags).length > 0 && (
                    <div className="chunk-field"><strong>Tags:</strong> {Object.entries(chunk.Tags).map(([k, v]) => `${k}=${v}`).join(', ')}</div>
                  )}
                  <div className="chunk-field">
                    <strong>Embeddings:</strong> [{chunk.Embeddings ? chunk.Embeddings.slice(0, 8).map(e => e.toFixed(4)).join(', ') + (chunk.Embeddings.length > 8 ? ', ...' : '') : 'none'}] ({chunk.Embeddings ? chunk.Embeddings.length : 0} dims)
                    {chunk.Embeddings && chunk.Embeddings.length > 8 && (
                      <a
                        className="embeddings-toggle"
                        onClick={() => setExpandedEmbeddings(prev => {
                          const next = new Set(prev);
                          if (next.has(i)) next.delete(i); else next.add(i);
                          return next;
                        })}
                      >
                        {expandedEmbeddings.has(i) ? 'Collapse' : 'Show all'}
                      </a>
                    )}
                    {expandedEmbeddings.has(i) && chunk.Embeddings && (
                      <pre className="embeddings-expanded">{JSON.stringify(chunk.Embeddings, null, 2)}</pre>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
