import React from 'react';
import Tooltip from './Tooltip';

export default function TooltipIcon({ content }) {
  if (!content) return null;

  return (
    <Tooltip content={content}>
      <span className="tooltip-icon" tabIndex={0} aria-label="Field help">
        <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="8" cy="8" r="7" />
          <line x1="8" y1="7" x2="8" y2="11" />
          <circle cx="8" cy="5" r="0.5" fill="currentColor" stroke="none" />
        </svg>
      </span>
    </Tooltip>
  );
}
