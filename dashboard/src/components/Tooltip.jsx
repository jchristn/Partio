import React, { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';

const VIEWPORT_PADDING = 12;
const TOOLTIP_OFFSET = 10;

export default function Tooltip({ content, children, block = false, className = '' }) {
  const anchorRef = useRef(null);
  const tooltipRef = useRef(null);
  const [visible, setVisible] = useState(false);
  const [position, setPosition] = useState({ top: -9999, left: -9999, placement: 'top' });

  const updatePosition = () => {
    if (!anchorRef.current || !tooltipRef.current) return;

    const anchorRect = anchorRef.current.getBoundingClientRect();
    const tooltipRect = tooltipRef.current.getBoundingClientRect();

    let placement = 'top';
    let top = anchorRect.top - tooltipRect.height - TOOLTIP_OFFSET;
    if (top < VIEWPORT_PADDING) {
      placement = 'bottom';
      top = anchorRect.bottom + TOOLTIP_OFFSET;
    }

    if (top + tooltipRect.height > window.innerHeight - VIEWPORT_PADDING) {
      top = Math.max(VIEWPORT_PADDING, window.innerHeight - tooltipRect.height - VIEWPORT_PADDING);
    }

    let left = anchorRect.left + (anchorRect.width / 2) - (tooltipRect.width / 2);
    left = Math.min(
      Math.max(VIEWPORT_PADDING, left),
      window.innerWidth - tooltipRect.width - VIEWPORT_PADDING
    );

    setPosition({ top, left, placement });
  };

  useLayoutEffect(() => {
    if (!visible) return;
    updatePosition();
  }, [visible, content]);

  useEffect(() => {
    if (!visible) return undefined;

    const handleUpdate = () => updatePosition();
    window.addEventListener('resize', handleUpdate);
    window.addEventListener('scroll', handleUpdate, true);

    return () => {
      window.removeEventListener('resize', handleUpdate);
      window.removeEventListener('scroll', handleUpdate, true);
    };
  }, [visible]);

  if (!content) {
    return children;
  }

  return (
    <>
      <span
        ref={anchorRef}
        className={`tooltip-anchor ${block ? 'tooltip-anchor-block' : ''} ${className}`.trim()}
        onMouseEnter={() => setVisible(true)}
        onMouseLeave={() => setVisible(false)}
        onFocus={() => setVisible(true)}
        onBlur={() => setVisible(false)}
      >
        {children}
      </span>
      {visible && createPortal(
        <div
          ref={tooltipRef}
          className={`dashboard-tooltip dashboard-tooltip-${position.placement}`}
          style={{ top: `${position.top}px`, left: `${position.left}px` }}
          role="tooltip"
        >
          {content}
        </div>,
        document.body
      )}
    </>
  );
}
