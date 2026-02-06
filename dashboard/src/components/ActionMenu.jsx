import React, { useState, useRef, useEffect } from 'react';
import './ActionMenu.css';

export default function ActionMenu({ actions }) {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);

  useEffect(() => {
    function handleClick(e) {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, []);

  return (
    <div className="action-menu" ref={ref}>
      <button className="action-menu-trigger" onClick={() => setOpen(!open)}>...</button>
      {open && (
        <div className="action-menu-dropdown">
          {actions.map((action, i) => (
            <button key={i} className="action-menu-item" onClick={() => { setOpen(false); action.onClick(); }}>
              {action.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
