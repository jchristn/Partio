import React, { useState } from 'react';
import Tooltip from './Tooltip';
import './TagInput.css';

export default function TagInput({ value = [], onChange, inputTooltip, addButtonTooltip, removeTooltip }) {
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
            <Tooltip content={removeTooltip || `Remove label "${tag}" from this item.`}>
              <button className="tag-remove" onClick={() => removeTag(tag)}>&times;</button>
            </Tooltip>
          </span>
        ))}
      </div>
      <div className="tag-input-row">
        <Tooltip content={inputTooltip} block>
          <input value={input} onChange={e => setInput(e.target.value)} onKeyDown={handleKeyDown} placeholder="Add label..." />
        </Tooltip>
        <Tooltip content={addButtonTooltip || 'Add the typed label to this item. Duplicate labels are ignored.'}>
          <button className="secondary" onClick={addTag}>Add</button>
        </Tooltip>
      </div>
    </div>
  );
}
