import React from 'react';
import './JsonEditor.css';

export default function JsonEditor({ value, onChange, rows = 10 }) {
  return (
    <textarea className="json-editor" value={value} onChange={e => onChange(e.target.value)} rows={rows} spellCheck={false} />
  );
}
