import React, { useState, useEffect, useMemo } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import { copyToClipboard } from '../utils/clipboard';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import './ChunkEmbedView.css';

const STRATEGY_COMPATIBILITY = {
  FixedTokenCount: ['Text', 'Code', 'Hyperlink', 'Meta', 'List', 'Table'],
  SentenceBased: ['Text', 'Code', 'Hyperlink', 'Meta', 'List', 'Table'],
  ParagraphBased: ['Text', 'Code', 'Hyperlink', 'Meta', 'List', 'Table'],
  WholeList: ['List'],
  ListEntry: ['List'],
  Row: ['Table'],
  RowWithHeaders: ['Table'],
  RowGroupWithHeaders: ['Table'],
  KeyValuePairs: ['Table'],
  WholeTable: ['Table']
};

const STRATEGY_LABELS = {
  FixedTokenCount: 'Fixed Token Count',
  SentenceBased: 'Sentence Based',
  ParagraphBased: 'Paragraph Based',
  WholeList: 'Whole List',
  ListEntry: 'List Entry',
  Row: 'Row',
  RowWithHeaders: 'Row With Headers',
  RowGroupWithHeaders: 'Row Group With Headers',
  KeyValuePairs: 'Key-Value Pairs',
  WholeTable: 'Whole Table'
};

const DEFAULT_SUMMARIZATION_PROMPT = 'Please summarize the following content in no more than {tokens} tokens.\n\nContent:\n{content}\n\nContext:\n{context}';

function renderCellTree(cell, depth, copiedChunk, setCopiedChunk, expandedEmbeddings, setExpandedEmbeddings, keyPrefix) {
  const isSummary = cell.Type === 'Summary';
  const cellKey = keyPrefix || '0';
  return (
    <div key={cellKey} style={{ marginLeft: depth * 16 }}>
      <div className={`chunk-result ${isSummary ? 'chunk-result-summary' : ''}`}>
        <div className="chunk-header">
          <span>
            {isSummary && <span className="summary-badge">Summary</span>}
            {cell.Type || 'Cell'}{cell.GUID ? ` (${cell.GUID.substring(0, 8)}...)` : ''}
          </span>
        </div>
        {cell.Text && (
          <div className="chunk-field"><strong>Text:</strong> {cell.Text.length > 300 ? cell.Text.slice(0, 300) + '...' : cell.Text}</div>
        )}
        {cell.Chunks && cell.Chunks.length > 0 && (
          <div className="chunk-field">
            <strong>Chunks:</strong> {cell.Chunks.length}
            {cell.Chunks.map((chunk, i) => {
              const chunkKey = `${cellKey}-chunk-${i}`;
              return (
                <div key={chunkKey} className="chunk-result" style={{ marginTop: 4, marginLeft: 8 }}>
                  <div className="chunk-header">
                    <span>Chunk {i + 1}{chunk.CellGUID ? ` [${chunk.CellGUID.substring(0, 8)}]` : ''}</span>
                    <button
                      type="button"
                      className={`chunk-copy-btn ${copiedChunk === chunkKey ? 'copied' : ''}`}
                      onClick={async () => {
                        const success = await copyToClipboard(JSON.stringify(chunk, null, 2));
                        if (success) {
                          setCopiedChunk(chunkKey);
                          setTimeout(() => setCopiedChunk(null), 1500);
                        }
                      }}
                      title={copiedChunk === chunkKey ? 'Copied!' : 'Copy chunk JSON'}
                    >
                      {copiedChunk === chunkKey ? (
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
                  <div className="chunk-field">
                    <strong>Embeddings:</strong> [{chunk.Embeddings ? chunk.Embeddings.slice(0, 8).map(e => e.toFixed(4)).join(', ') + (chunk.Embeddings.length > 8 ? ', ...' : '') : 'none'}] ({chunk.Embeddings ? chunk.Embeddings.length : 0} dims)
                    {chunk.Embeddings && chunk.Embeddings.length > 8 && (
                      <a
                        className="embeddings-toggle"
                        onClick={() => setExpandedEmbeddings(prev => {
                          const next = new Set(prev);
                          if (next.has(chunkKey)) next.delete(chunkKey); else next.add(chunkKey);
                          return next;
                        })}
                      >
                        {expandedEmbeddings.has(chunkKey) ? 'Collapse' : 'Show all'}
                      </a>
                    )}
                    {expandedEmbeddings.has(chunkKey) && chunk.Embeddings && (
                      <pre className="embeddings-expanded">{JSON.stringify(chunk.Embeddings, null, 2)}</pre>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
      {cell.Children && cell.Children.map((child, ci) =>
        renderCellTree(child, depth + 1, copiedChunk, setCopiedChunk, expandedEmbeddings, setExpandedEmbeddings, `${cellKey}-${ci}`)
      )}
    </div>
  );
}

export default function ChunkEmbedView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [endpoints, setEndpoints] = useState([]);
  const [completionEndpoints, setCompletionEndpoints] = useState([]);
  const [endpointsLoading, setEndpointsLoading] = useState(true);
  const [copiedChunk, setCopiedChunk] = useState(null);
  const [expandedEmbeddings, setExpandedEmbeddings] = useState(new Set());

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [embRes, compRes] = await Promise.all([
          api.enumerateEndpoints(),
          api.enumerateCompletionEndpoints()
        ]);
        if (!cancelled) {
          if (embRes && embRes.Data) setEndpoints(embRes.Data.filter(ep => ep.Active !== false));
          if (compRes && compRes.Data) setCompletionEndpoints(compRes.Data.filter(ep => ep.Active !== false));
        }
      } catch (err) {
        if (!cancelled) { setEndpoints([]); setCompletionEndpoints([]); }
      }
      if (!cancelled) setEndpointsLoading(false);
    })();
    return () => { cancelled = true; };
  }, [serverUrl, bearerToken]);

  const [form, setForm] = useState({
    InputType: 'Text',
    Text: '',
    TableInput: '',
    ListInput: '',
    Strategy: 'FixedTokenCount',
    FixedTokenCount: 256,
    OverlapCount: 0,
    OverlapStrategy: 'SlidingWindow',
    ContextPrefix: '',
    EndpointId: '',
    L2Normalization: false,
    Labels: [],
    Tags: {},
    RowGroupSize: 5,
    EnableSummarization: false,
    CompletionEndpointId: '',
    SummarizationOrder: 'BottomUp',
    MaxSummaryTokens: 1024,
    MinCellLength: 128,
    MaxParallelTasks: 4,
    SummarizationPrompt: DEFAULT_SUMMARIZATION_PROMPT,
    ShowPrompt: false
  });

  const availableStrategies = useMemo(() => {
    return Object.entries(STRATEGY_COMPATIBILITY)
      .filter(([, types]) => types.includes(form.InputType))
      .map(([key]) => key);
  }, [form.InputType]);

  useEffect(() => {
    if (!availableStrategies.includes(form.Strategy)) {
      update('Strategy', availableStrategies[0] || 'FixedTokenCount');
    }
  }, [availableStrategies]);

  const parseTableInput = (input) => {
    if (!input.trim()) return [];
    return input.trim().split('\n').map(line =>
      line.split(',').map(cell => cell.trim())
    );
  };

  const parseListInput = (input) => {
    if (!input.trim()) return [];
    return input.trim().split('\n').filter(line => line.trim());
  };

  const handleSubmit = async () => {
    setLoading(true);
    setError(null);
    setResult(null);

    const chunkingConfig = {
      Strategy: form.Strategy,
      FixedTokenCount: parseInt(form.FixedTokenCount) || 256,
      OverlapCount: parseInt(form.OverlapCount) || 0,
      OverlapStrategy: form.OverlapStrategy,
      ContextPrefix: form.ContextPrefix || null,
      RowGroupSize: parseInt(form.RowGroupSize) || 5
    };

    let request = {
      Type: form.InputType,
      ChunkingConfiguration: chunkingConfig,
      EmbeddingConfiguration: {
        EmbeddingEndpointId: form.EndpointId,
        L2Normalization: form.L2Normalization
      },
      Labels: form.Labels.length > 0 ? form.Labels : null,
      Tags: Object.keys(form.Tags).length > 0 ? form.Tags : null
    };

    if (form.EnableSummarization && form.CompletionEndpointId) {
      request.SummarizationConfiguration = {
        CompletionEndpointId: form.CompletionEndpointId,
        Order: form.SummarizationOrder,
        MaxSummaryTokens: parseInt(form.MaxSummaryTokens) || 1024,
        MinCellLength: parseInt(form.MinCellLength) || 128,
        MaxParallelTasks: parseInt(form.MaxParallelTasks) || 4,
        SummarizationPrompt: form.SummarizationPrompt || null
      };
    }

    if (form.InputType === 'Table') {
      request.Table = parseTableInput(form.TableInput);
    } else if (form.InputType === 'List') {
      request.UnorderedList = parseListInput(form.ListInput);
    } else {
      request.Text = form.Text;
    }

    try {
      const res = await api.process(request);
      setResult(res);
    } catch (err) {
      setError(err.message);
    }
    setLoading(false);
  };

  const update = (key, value) => setForm(prev => ({ ...prev, [key]: value }));

  const hasHierarchicalResult = result && (result.Children || result.Type);

  return (
    <div className="chunk-embed">
      <div className="header-row"><h2>Process Cells</h2></div>
      <div className="chunk-embed-layout">
        <div className="chunk-embed-form card">
          <div className="form-group">
            <label>Input Type</label>
            <select value={form.InputType} onChange={e => update('InputType', e.target.value)}>
              <option value="Text">Text</option>
              <option value="Code">Code</option>
              <option value="Hyperlink">Hyperlink</option>
              <option value="Meta">Meta</option>
              <option value="List">List</option>
              <option value="Table">Table</option>
            </select>
          </div>

          {form.InputType === 'Table' ? (
            <div className="form-group">
              <label>Table (CSV format, first row = headers)</label>
              <textarea rows={8} value={form.TableInput} onChange={e => update('TableInput', e.target.value)} placeholder={"Name, Age, City\nAlice, 30, New York\nBob, 25, London"} />
            </div>
          ) : form.InputType === 'List' ? (
            <div className="form-group">
              <label>List Items (one per line)</label>
              <textarea rows={8} value={form.ListInput} onChange={e => update('ListInput', e.target.value)} placeholder={"First item\nSecond item\nThird item"} />
            </div>
          ) : (
            <div className="form-group">
              <label>Text Content</label>
              <textarea rows={8} value={form.Text} onChange={e => update('Text', e.target.value)} placeholder="Enter text to chunk and embed..." />
            </div>
          )}

          <div className="form-group">
            <label>Chunking Strategy</label>
            <select value={form.Strategy} onChange={e => update('Strategy', e.target.value)}>
              {availableStrategies.map(key => (
                <option key={key} value={key}>{STRATEGY_LABELS[key]}</option>
              ))}
            </select>
          </div>

          {form.Strategy === 'RowGroupWithHeaders' && (
            <div className="form-group">
              <label>Row Group Size</label>
              <input type="number" min="1" value={form.RowGroupSize} onChange={e => update('RowGroupSize', e.target.value)} />
            </div>
          )}

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

          {/* Summarization Section */}
          <div className="form-section-divider" />
          <div className="checkbox-group" style={{ marginBottom: 8 }}>
            <input type="checkbox" checked={form.EnableSummarization} onChange={e => update('EnableSummarization', e.target.checked)} id="enableSumm" />
            <label htmlFor="enableSumm"><strong>Enable Summarization</strong></label>
          </div>

          {form.EnableSummarization && (
            <div className="summarization-config">
              <div className="form-group">
                <label>Inference Endpoint</label>
                <select value={form.CompletionEndpointId} onChange={e => update('CompletionEndpointId', e.target.value)} disabled={endpointsLoading}>
                  <option value="">{endpointsLoading ? 'Loading...' : '-- Select endpoint --'}</option>
                  {completionEndpoints.map(ep => (
                    <option key={ep.Id} value={ep.Id}>{ep.Name || ep.Model} ({ep.Id})</option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label>Order</label>
                <div className="radio-group">
                  <label className="radio-label">
                    <input type="radio" name="summOrder" value="TopDown" checked={form.SummarizationOrder === 'TopDown'} onChange={e => update('SummarizationOrder', e.target.value)} />
                    Top Down
                  </label>
                  <label className="radio-label">
                    <input type="radio" name="summOrder" value="BottomUp" checked={form.SummarizationOrder === 'BottomUp'} onChange={e => update('SummarizationOrder', e.target.value)} />
                    Bottom Up
                  </label>
                </div>
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label>Max Summary Tokens</label>
                  <input type="number" min="128" value={form.MaxSummaryTokens} onChange={e => update('MaxSummaryTokens', e.target.value)} />
                </div>
                <div className="form-group">
                  <label>Min Cell Length</label>
                  <input type="number" min="0" value={form.MinCellLength} onChange={e => update('MinCellLength', e.target.value)} />
                </div>
              </div>
              <div className="form-group">
                <label>Max Parallel Tasks</label>
                <input type="number" min="1" max="32" value={form.MaxParallelTasks} onChange={e => update('MaxParallelTasks', e.target.value)} />
              </div>
              <div className="form-group">
                <a className="embeddings-toggle" onClick={() => update('ShowPrompt', !form.ShowPrompt)}>
                  {form.ShowPrompt ? 'Hide Custom Prompt' : 'Show Custom Prompt'}
                </a>
                {form.ShowPrompt && (
                  <textarea
                    rows={5}
                    value={form.SummarizationPrompt}
                    onChange={e => update('SummarizationPrompt', e.target.value)}
                    placeholder="Summarization prompt template. Use {tokens}, {content}, {context} placeholders."
                    style={{ marginTop: 4, fontFamily: 'monospace', fontSize: '0.85em' }}
                  />
                )}
              </div>
            </div>
          )}

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
              {hasHierarchicalResult ? (
                <>
                  <p>Response contains hierarchical cell data{result.Children ? ` with ${result.Children.length} child cells` : ''}.</p>
                  {renderCellTree(result, 0, copiedChunk, setCopiedChunk, expandedEmbeddings, setExpandedEmbeddings, 'root')}
                </>
              ) : (
                <>
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
                </>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
