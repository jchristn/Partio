import React from 'react';
import FormFieldLabel from './FormFieldLabel';
import Tooltip from './Tooltip';
import './EndpointMetadataEditor.css';

export function createLabelRows(labels = []) {
  const rows = Array.isArray(labels) ? labels.map(label => String(label ?? '')) : [];
  return rows.length > 0 ? rows : [''];
}

export function normalizeLabelRows(rows = []) {
  return rows
    .map(label => String(label ?? '').trim())
    .filter(label => label.length > 0);
}

export function createTagRows(tags = {}) {
  if (Array.isArray(tags)) {
    const rows = tags.map(row => ({
      Key: String(row?.Key ?? row?.key ?? ''),
      Value: String(row?.Value ?? row?.value ?? '')
    }));
    return rows.length > 0 ? rows : [{ Key: '', Value: '' }];
  }

  const rows = tags && typeof tags === 'object'
    ? Object.entries(tags).map(([Key, Value]) => ({ Key, Value: String(Value ?? '') }))
    : [];

  return rows.length > 0 ? rows : [{ Key: '', Value: '' }];
}

export function normalizeTagRows(rows = []) {
  return rows.reduce((tags, row) => {
    const key = String(row?.Key ?? '').trim();
    if (key.length > 0) {
      tags[key] = String(row?.Value ?? '').trim();
    }
    return tags;
  }, {});
}

function PlusIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <line x1="12" y1="5" x2="12" y2="19" />
      <line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  );
}

function TrashIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <polyline points="3 6 5 6 21 6" />
      <path d="M19 6l-1 14H6L5 6" />
      <path d="M10 11v6" />
      <path d="M14 11v6" />
      <path d="M9 6V4h6v2" />
    </svg>
  );
}

function IconButton({ label, variant = 'neutral', onClick, children }) {
  return (
    <Tooltip content={label}>
      <button type="button" className={`metadata-icon-button metadata-icon-button-${variant}`} onClick={onClick} aria-label={label} title={label}>
        {children}
      </button>
    </Tooltip>
  );
}

export default function EndpointMetadataEditor({ labels, tags, onLabelsChange, onTagsChange }) {
  const labelRows = createLabelRows(labels);
  const tagRows = createTagRows(tags);

  const updateLabel = (index, value) => {
    const next = [...labelRows];
    next[index] = value;
    onLabelsChange(next);
  };

  const removeLabel = (index) => {
    const next = labelRows.filter((_, i) => i !== index);
    onLabelsChange(next.length > 0 ? next : ['']);
  };

  const updateTag = (index, field, value) => {
    const next = tagRows.map((row, i) => i === index ? { ...row, [field]: value } : row);
    onTagsChange(next);
  };

  const removeTag = (index) => {
    const next = tagRows.filter((_, i) => i !== index);
    onTagsChange(next.length > 0 ? next : [{ Key: '', Value: '' }]);
  };

  return (
    <div className="endpoint-metadata-editor">
      <div className="form-group">
        <FormFieldLabel text="Labels" tooltip="Free-form endpoint labels stored with this endpoint and returned by the endpoint API." />
        <div className="endpoint-metadata-list">
          {labelRows.map((label, index) => (
            <div className="endpoint-metadata-row endpoint-metadata-label-row" key={`label-${index}`}>
              <Tooltip content="Endpoint label stored as a string." block>
                <input
                  value={label}
                  onChange={e => updateLabel(index, e.target.value)}
                  placeholder="Label"
                />
              </Tooltip>
              <IconButton label="Delete label" variant="danger" onClick={() => removeLabel(index)}>
                <TrashIcon />
              </IconButton>
              {index === labelRows.length - 1 ? (
                <IconButton label="Add label" onClick={() => onLabelsChange([...labelRows, ''])}>
                  <PlusIcon />
                </IconButton>
              ) : (
                <span className="metadata-icon-spacer" aria-hidden="true" />
              )}
            </div>
          ))}
        </div>
      </div>

      <div className="form-group">
        <FormFieldLabel text="Tags" tooltip="Free-form endpoint key/value metadata stored with this endpoint and returned by the endpoint API." />
        <div className="endpoint-metadata-list">
          {tagRows.map((tag, index) => (
            <div className="endpoint-metadata-row endpoint-metadata-tag-row" key={`tag-${index}`}>
              <Tooltip content="Tag key stored as a string." block>
                <input
                  value={tag.Key}
                  onChange={e => updateTag(index, 'Key', e.target.value)}
                  placeholder="Key"
                />
              </Tooltip>
              <Tooltip content="Tag value stored as a string." block>
                <input
                  value={tag.Value}
                  onChange={e => updateTag(index, 'Value', e.target.value)}
                  placeholder="Value"
                />
              </Tooltip>
              <IconButton label="Delete tag" variant="danger" onClick={() => removeTag(index)}>
                <TrashIcon />
              </IconButton>
              {index === tagRows.length - 1 ? (
                <IconButton label="Add tag" onClick={() => onTagsChange([...tagRows, { Key: '', Value: '' }])}>
                  <PlusIcon />
                </IconButton>
              ) : (
                <span className="metadata-icon-spacer" aria-hidden="true" />
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
