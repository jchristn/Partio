import React, { useState } from 'react';
import Modal from '../Modal';
import { copyToClipboard } from '../../utils/clipboard';
import './JsonViewModal.css';

export default function JsonViewModal({ isOpen, onClose, title, data }) {
  const [copied, setCopied] = useState(false);

  if (!isOpen) return null;

  const json = JSON.stringify(data, null, 2);

  const handleCopy = async () => {
    const success = await copyToClipboard(json);
    if (success) {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    }
  };

  const copyButton = (
    <button type="button" className={`json-header-copy-btn ${copied ? 'copied' : ''}`} onClick={handleCopy} title="Copy JSON">
      {copied ? (
        <svg width="16" height="16" viewBox="0 0 20 20" fill="currentColor">
          <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
        </svg>
      ) : (
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <rect x="9" y="9" width="13" height="13" rx="2" ry="2" /><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1" />
        </svg>
      )}
    </button>
  );

  return (
    <Modal title={title || 'View JSON'} onClose={onClose} className="modal-json" headerActions={copyButton}>
      <div className="json-view-modal">
        <pre className="json-view-content">{json}</pre>
      </div>
    </Modal>
  );
}
