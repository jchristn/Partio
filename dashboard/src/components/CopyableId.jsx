import React, { useState } from 'react';
import './CopyableId.css';

export default function CopyableId({ value }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    await navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <span className="copyable-id" onClick={handleCopy} title="Click to copy">
      <span className="copyable-id-text">{value}</span>
      {copied && <span className="copied-badge">Copied!</span>}
    </span>
  );
}
