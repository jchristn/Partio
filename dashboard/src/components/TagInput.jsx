import React, { useState } from 'react';
import './TagInput.css';

export default function TagInput({ value = [], onChange }) {
  const [input, setInput] = useState('');

  const addTag = () => {
    const tag = input.trim();
    if (tag && !value.includes(tag)) {
      onChange([...value, tag]);
    }
    setInput('');
  };

  const removeTag = (tag) => {
    onChange(value.filter(t => t !== tag));
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') { e.preventDefault(); addTag(); }
  };

  return (
    <div className="tag-input">
      <div className="tag-list">
        {value.map(tag => (
          <span key={tag} className="tag-chip">
            {tag}
            <button className="tag-remove" onClick={() => removeTag(tag)}>&times;</button>
          </span>
        ))}
      </div>
      <div className="tag-input-row">
        <input value={input} onChange={e => setInput(e.target.value)} onKeyDown={handleKeyDown} placeholder="Add label..." />
        <button className="secondary" onClick={addTag}>Add</button>
      </div>
    </div>
  );
}
