import React, { useState, useRef, useEffect, useLayoutEffect } from 'react';
import { createPortal } from 'react-dom';
import './ActionMenu.css';

export default function ActionMenu({ actions }) {
  const [isOpen, setIsOpen] = useState(false);
  const [position, setPosition] = useState({ top: 0, left: 0 });
  const menuRef = useRef(null);
  const dropdownRef = useRef(null);

  const updatePosition = () => {
    if (!menuRef.current || !dropdownRef.current) return;

    const triggerRect = menuRef.current.getBoundingClientRect();
    const dropdownRect = dropdownRef.current.getBoundingClientRect();
    const viewportPadding = 8;

    let left = triggerRect.right - dropdownRect.width;
    left = Math.max(viewportPadding, Math.min(left, window.innerWidth - dropdownRect.width - viewportPadding));

    let top = triggerRect.bottom + 4;
    if (top + dropdownRect.height > window.innerHeight - viewportPadding) {
      top = Math.max(viewportPadding, triggerRect.top - dropdownRect.height - 4);
    }

    setPosition({ top, left });
  };

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (
        menuRef.current
        && !menuRef.current.contains(event.target)
        && (!dropdownRef.current || !dropdownRef.current.contains(event.target))
      ) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  useLayoutEffect(() => {
    if (isOpen) updatePosition();
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) return undefined;
    const handleUpdate = () => updatePosition();
    window.addEventListener('resize', handleUpdate);
    window.addEventListener('scroll', handleUpdate, true);
    return () => {
      window.removeEventListener('resize', handleUpdate);
      window.removeEventListener('scroll', handleUpdate, true);
    };
  }, [isOpen]);

  const handleTriggerClick = (e) => {
    e.stopPropagation();
    e.preventDefault();
    setIsOpen(!isOpen);
  };

  const handleAction = (e, action) => {
    e.stopPropagation();
    e.preventDefault();
    setIsOpen(false);
    if (action.onClick) {
      action.onClick();
    }
  };

  return (
    <div className={`action-menu${isOpen ? ' action-menu-open' : ''}`} ref={menuRef} onClick={(e) => e.stopPropagation()}>
      <button className="action-menu-trigger" onClick={handleTriggerClick}>
        <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor">
          <circle cx="8" cy="3" r="1.5" />
          <circle cx="8" cy="8" r="1.5" />
          <circle cx="8" cy="13" r="1.5" />
        </svg>
      </button>
      {isOpen && createPortal(
        <div
          ref={dropdownRef}
          className="action-menu-dropdown action-menu-dropdown-portal"
          style={{ top: `${position.top}px`, left: `${position.left}px` }}
        >
          {actions.map((action, index) =>
            action.divider ? (
              <div key={index} className="action-menu-divider"></div>
            ) : (
              <button
                key={index}
                className={`action-menu-item ${action.danger ? 'danger' : ''}`}
                onClick={(e) => handleAction(e, action)}
                disabled={action.disabled}
              >
                {action.icon && <span className="action-icon">{action.icon}</span>}
                {action.label}
              </button>
            )
          )}
        </div>,
        document.body
      )}
    </div>
  );
}
