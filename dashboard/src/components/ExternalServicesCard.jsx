import React, { useState } from 'react';
import { copyToClipboard } from '../utils/clipboard';
import './ExternalServicesCard.css';

// Observability and diagnostics tools bundled with the local Docker stack (docker/compose.yaml).
// Ports are the host-published ports the browser actually reaches, not internal container ports.
const SERVICES = [
  { name: 'Grafana', port: 3000, cred: 'admin / admin', desc: 'Dashboards for metrics, traces, and logs' },
  { name: 'Prometheus', port: 9090, cred: 'no auth', desc: 'Metrics store and query UI' },
  { name: 'Tempo', port: 3200, cred: 'no auth', desc: 'Distributed traces (view via Grafana)' },
  { name: 'Loki', port: 3100, cred: 'no auth', desc: 'Log aggregation (view via Grafana)' },
  { name: 'Ollama', port: 11434, cred: 'no auth', desc: 'Local model runtime' },
];

export default function ExternalServicesCard() {
  const host =
    typeof window !== 'undefined' && window.location && window.location.hostname
      ? window.location.hostname
      : 'localhost';
  const [copied, setCopied] = useState(null);

  const handleCopy = async (name, cred) => {
    const ok = await copyToClipboard(cred);
    if (ok) {
      setCopied(name);
      setTimeout(() => setCopied(null), 1500);
    }
  };

  return (
    <div className="external-services" data-tour-id="external-services">
      <h3>External Services</h3>
      <p className="external-services-hint">
        Observability and diagnostics tools bundled with the local Docker stack. Links use the current host;
        default credentials are for local development only — change them before any shared deployment.
      </p>
      <div className="external-services-grid">
        {SERVICES.map((svc) => {
          const url = `http://${host}:${svc.port}`;
          const display = `${host}:${svc.port}`;
          const hasCred = svc.cred !== 'no auth';
          return (
            <div key={svc.name} className="service-card">
              <a
                className="service-card-link"
                href={url}
                target="_blank"
                rel="noopener noreferrer"
                title={`Open ${svc.name} at ${url} in a new tab`}
              >
                <span className="service-name">{svc.name}</span>
                <span className="service-url">{display}</span>
              </a>
              <p className="service-desc">{svc.desc}</p>
              <div className="service-cred-row">
                <code className="service-cred">{svc.cred}</code>
                {hasCred && (
                  <button
                    type="button"
                    className="service-cred-copy"
                    onClick={() => handleCopy(svc.name, svc.cred)}
                    title="Copy credentials"
                  >
                    {copied === svc.name ? '✓ Copied' : 'Copy'}
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
