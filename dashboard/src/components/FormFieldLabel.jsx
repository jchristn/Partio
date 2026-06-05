import React from 'react';
import Tooltip from './Tooltip';

export default function FormFieldLabel({ text, tooltip, htmlFor, className = '' }) {
  return (
    <label htmlFor={htmlFor} className={className}>
      <Tooltip content={tooltip}>
        <span>{text}</span>
      </Tooltip>
    </label>
  );
}
