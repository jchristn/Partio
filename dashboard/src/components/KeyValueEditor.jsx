import React from 'react';
import Tooltip from './Tooltip';
import './KeyValueEditor.css';

export default function KeyValueEditor({
  value = {},
  onChange,
  keyTooltip,
  valueTooltip,
  addEntryTooltip,
  removeEntryTooltip
}) {
  const entries = Object.entries(value);

  const handleKeyChange = (oldKey, newKey) => {
    const newValue = { ...value };
    const val = newValue[oldKey];
    delete newValue[oldKey];
    newValue[newKey] = val;
    onChange(newValue);
  };

  const handleValueChange = (key, newVal) => {
    onChange({ ...value, [key]: newVal });
  };

  const addEntry = () => {
    onChange({ ...value, '': '' });
  };

  const removeEntry = (key) => {
    const newValue = { ...value };
    delete newValue[key];
    onChange(newValue);
  };

  return (
    <div className="kv-editor">
      {entries.map(([k, v], i) => (
        <div key={i} className="kv-row">
          <Tooltip content={keyTooltip || 'Tag key or metadata field name. Use short, stable names such as source, team, or documentType.'} block>
            <input value={k} onChange={e => handleKeyChange(k, e.target.value)} placeholder="Key" />
          </Tooltip>
          <Tooltip content={valueTooltip || 'Tag value paired with the key. Values are stored as strings.'} block>
            <input value={v} onChange={e => handleValueChange(k, e.target.value)} placeholder="Value" />
          </Tooltip>
          <Tooltip content={removeEntryTooltip || `Remove the ${k || 'current'} tag entry.`}>
            <button className="danger kv-remove" onClick={() => removeEntry(k)}>x</button>
          </Tooltip>
        </div>
      ))}
      <Tooltip content={addEntryTooltip || 'Add another key/value metadata entry.'}>
        <button className="secondary" onClick={addEntry}>+ Add Tag</button>
      </Tooltip>
    </div>
  );
}
