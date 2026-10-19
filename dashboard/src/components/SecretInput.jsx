import React, { useState } from 'react';
import { isMaskedSecret } from '../utils/providers';
import './ProviderForm.css';

function EyeIcon({ open }) {
  return open ? (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94" />
      <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19" />
      <line x1="1" y1="1" x2="23" y2="23" />
    </svg>
  ) : (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  );
}

/**
 * Secret field. A masked value returned by the server (****abcd) is shown as a stored-secret chip with
 * Reveal (fetches the real value through the reveal API) and Replace (clears the field for a new value)
 * actions; leaving it untouched keeps the stored secret on save. Plain values use a password input with a
 * show/hide toggle, or a textarea with an optional file loader when multiline is set.
 */
export default function SecretInput({
  id,
  value,
  onChange,
  placeholder,
  multiline = false,
  onReveal,
  revealing = false,
  invalid = false,
  fileAccept,
  ariaLabel,
}) {
  const [visible, setVisible] = useState(false);
  const [fileError, setFileError] = useState('');

  if (isMaskedSecret(value)) {
    return (
      <div className={`secret-stored${invalid ? ' invalid' : ''}`}>
        <span className="secret-stored-text">
          Stored{multiline ? '' : ` (${value})`}. Leave as is to keep it.
        </span>
        <span className="secret-stored-actions">
          {onReveal && (
            <button type="button" className="secondary" onClick={onReveal} disabled={revealing}>
              {revealing ? 'Revealing…' : 'Reveal'}
            </button>
          )}
          <button type="button" className="secondary" onClick={() => onChange('')}>Replace</button>
        </span>
      </div>
    );
  }

  const loadFile = (event) => {
    const file = event.target.files && event.target.files[0];
    event.target.value = '';
    if (!file) return;
    setFileError('');
    file.text()
      .then(text => onChange(text.trim()))
      .catch(() => setFileError('Could not read the file.'));
  };

  if (multiline) {
    return (
      <div className="secret-multiline">
        <textarea
          id={id}
          aria-label={ariaLabel}
          className={`secret-textarea${visible ? '' : ' secret-concealed'}${invalid ? ' invalid' : ''}`}
          value={value || ''}
          onChange={e => onChange(e.target.value)}
          placeholder={placeholder}
          rows={visible ? 6 : 3}
          spellCheck={false}
        />
        <div className="secret-multiline-actions">
          {fileAccept && (
            <label className="secondary secret-file-btn">
              Load file…
              <input type="file" accept={fileAccept} onChange={loadFile} hidden />
            </label>
          )}
          {value && (
            <button type="button" className="secondary" onClick={() => setVisible(!visible)}>{visible ? 'Hide' : 'Show'}</button>
          )}
          {fileError && <span className="field-error">{fileError}</span>}
        </div>
      </div>
    );
  }

  return (
    <div className="password-input-wrapper">
      <input
        id={id}
        aria-label={ariaLabel}
        className={invalid ? 'invalid' : undefined}
        type={visible ? 'text' : 'password'}
        value={value || ''}
        onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        autoComplete="off"
      />
      <button type="button" className="password-toggle-btn" onClick={() => setVisible(!visible)} title={visible ? 'Hide' : 'Show'}>
        <EyeIcon open={visible} />
      </button>
    </div>
  );
}
