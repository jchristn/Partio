import React, { useState } from 'react';
import { PROVIDER_GROUPS, getProvider, providersForKind } from '../utils/providers';
import './ProviderForm.css';

function kindBadges(provider) {
  return provider.kinds.map(k => (k === 'completion' ? 'Inference' : 'Embeddings'));
}

function ProviderBadges({ provider }) {
  return (
    <span className="provider-badges">
      {kindBadges(provider).map(b => <span key={b} className="provider-badge">{b}</span>)}
      <span className="provider-badge provider-badge-auth">{provider.auth}</span>
      {provider.health === 'probe' && <span className="provider-badge provider-badge-muted">Probe health</span>}
    </span>
  );
}

/**
 * Provider selector for endpoint forms. Shows a grouped card grid of the providers that can serve the
 * endpoint kind; once a provider is chosen it collapses to a summary bar with a Change button. When
 * confirmChange is set (editing an existing endpoint), switching providers asks for confirmation because
 * provider-specific connection fields are reset.
 */
export default function ProviderPicker({ kind, value, onChange, confirmChange = false, startExpanded = false }) {
  const [expanded, setExpanded] = useState(startExpanded || !value);
  const [pending, setPending] = useState(null);
  const providers = providersForKind(kind);
  const selected = value ? getProvider(value) : null;

  const choose = (provider) => {
    if (provider.value === value) {
      setExpanded(false);
      setPending(null);
      return;
    }
    if (confirmChange && value) {
      setPending(provider);
      return;
    }
    onChange(provider.value);
    setExpanded(false);
  };

  const confirm = () => {
    onChange(pending.value);
    setPending(null);
    setExpanded(false);
  };

  if (!expanded && selected) {
    return (
      <div className="provider-summary" data-testid="provider-summary">
        <div className="provider-summary-main">
          <span className="provider-summary-label">Provider</span>
          <strong className="provider-summary-name">{selected.label}</strong>
          <ProviderBadges provider={selected} />
        </div>
        <p className="provider-summary-text">{selected.summary}</p>
        <button type="button" className="secondary provider-change-btn" onClick={() => setExpanded(true)}>Change</button>
      </div>
    );
  }

  return (
    <div className="provider-picker" role="radiogroup" aria-label="Provider">
      {PROVIDER_GROUPS.map(group => {
        const items = providers.filter(p => p.group === group.key);
        if (items.length === 0) return null;
        return (
          <div key={group.key} className="provider-group">
            <div className="provider-group-label">{group.label}</div>
            <div className="provider-grid">
              {items.map(provider => (
                <button
                  type="button"
                  key={provider.value}
                  role="radio"
                  aria-checked={provider.value === value}
                  className={`provider-card${provider.value === value ? ' selected' : ''}${pending?.value === provider.value ? ' pending' : ''}`}
                  onClick={() => choose(provider)}
                >
                  <span className="provider-card-name">{provider.label}</span>
                  <span className="provider-card-text">{provider.summary}</span>
                  <ProviderBadges provider={provider} />
                </button>
              ))}
            </div>
          </div>
        );
      })}
      {pending && (
        <div className="provider-confirm" role="alert">
          <span>Switch to <strong>{pending.label}</strong>? The model, endpoint, credentials, and provider settings are reset to {pending.label} defaults.</span>
          <div className="btn-group">
            <button type="button" className="primary" onClick={confirm}>Switch</button>
            <button type="button" className="secondary" onClick={() => setPending(null)}>Cancel</button>
          </div>
        </div>
      )}
      {selected && !pending && (
        <div className="provider-picker-footer">
          <button type="button" className="secondary" onClick={() => setExpanded(false)}>Keep {selected.label}</button>
        </div>
      )}
    </div>
  );
}
