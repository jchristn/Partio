import React, { useState } from 'react';
import { useApp } from '../context/AppContext';
import { PartioApi } from '../utils/api';
import TagInput from './TagInput';
import KeyValueEditor from './KeyValueEditor';
import './ChunkEmbedView.css';

export default function ChunkEmbedView() {
  const { serverUrl, bearerToken } = useApp();
  const api = new PartioApi(serverUrl, bearerToken);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);

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
    Model: 'all-minilm',
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
        Model: form.Model,
        L2Normalization: form.L2Normalization
      },
      Labels: form.Labels.length > 0 ? form.Labels : null,
      Tags: Object.keys(form.Tags).length > 0 ? form.Tags : null
    };

    try {
      const res = await api.process(request);
      setResult(res);
    } catch (err) {
      setError(err.message);
    }
    setLoading(false);
  };

  const update = (key, value) => setForm(prev => ({ ...prev, [key]: value }));

  return (
    <div className="chunk-embed">
      <div className="header-row"><h2>Chunk & Embed</h2></div>
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
            <div className="form-group"><label>Embedding Model</label><input value={form.Model} onChange={e => update('Model', e.target.value)} placeholder="all-minilm" /></div>
            <div className="form-group" style={{ display: 'flex', alignItems: 'flex-end', paddingBottom: 16 }}>
              <label><input type="checkbox" checked={form.L2Normalization} onChange={e => update('L2Normalization', e.target.checked)} /> L2 Normalize</label>
            </div>
          </div>

          <div className="form-group"><label>Labels</label><TagInput value={form.Labels} onChange={v => update('Labels', v)} /></div>
          <div className="form-group"><label>Tags</label><KeyValueEditor value={form.Tags} onChange={v => update('Tags', v)} /></div>

          <button className="primary" onClick={handleSubmit} disabled={loading} style={{ marginTop: 12 }}>
            {loading ? 'Processing...' : 'Process'}
          </button>
        </div>

        <div className="chunk-embed-results">
          {error && <div className="card" style={{ color: 'var(--danger)' }}>Error: {error}</div>}
          {result && (
            <div className="card">
              <h3>Results</h3>
              <p>Cells: {result.Cells} | Total Chunks: {result.TotalChunks}</p>
              {result.Chunks && result.Chunks.map((chunk, i) => (
                <div key={i} className="chunk-result">
                  <div className="chunk-header">Chunk {i + 1}</div>
                  <div className="chunk-field"><strong>Text:</strong> {chunk.Text}</div>
                  <div className="chunk-field"><strong>Chunked Text:</strong> {chunk.ChunkedText}</div>
                  <div className="chunk-field"><strong>Embeddings:</strong> [{chunk.Embeddings ? chunk.Embeddings.slice(0, 8).map(e => e.toFixed(4)).join(', ') + (chunk.Embeddings.length > 8 ? ', ...' : '') : 'none'}] ({chunk.Embeddings ? chunk.Embeddings.length : 0} dims)</div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
