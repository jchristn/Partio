import React from 'react';
import TooltipIcon from './TooltipIcon';

export default function FormFieldLabel({ text, tooltip, htmlFor, className = '' }) {
  return (
    <label htmlFor={htmlFor} className={className}>
      {text}
      <TooltipIcon content={tooltip} />
    </label>
  );
}
