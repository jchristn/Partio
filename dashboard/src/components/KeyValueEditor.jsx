import React from 'react';
import './KeyValueEditor.css';

export default function KeyValueEditor({ value = {}, onChange }) {
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
          <input value={k} onChange={e => handleKeyChange(k, e.target.value)} placeholder="Key" />
          <input value={v} onChange={e => handleValueChange(k, e.target.value)} placeholder="Value" />
          <button className="danger kv-remove" onClick={() => removeEntry(k)}>x</button>
        </div>
      ))}
      <button className="secondary" onClick={addEntry}>+ Add Tag</button>
    </div>
  );
}
