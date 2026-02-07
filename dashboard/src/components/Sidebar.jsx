import React from 'react';
import { NavLink } from 'react-router-dom';
import './Sidebar.css';

const adminLinks = [
  { to: '/tenants', label: 'Tenants' },
  { to: '/users', label: 'Users' },
  { to: '/credentials', label: 'Credentials' },
  { to: '/endpoints', label: 'Endpoints' },
];

const processingLinks = [
  { to: '/history', label: 'Request History' },
  { to: '/process', label: 'Process Cells' },
];

export default function Sidebar() {
  return (
    <aside className="sidebar">
      <div className="sidebar-header">
        <img src="/logo-light-text.png" alt="Partio" className="sidebar-logo" />
      </div>
      <nav className="sidebar-nav">
        <div className="nav-section-header">Administration</div>
        {adminLinks.map(link => (
          <NavLink key={link.to} to={link.to} className={({ isActive }) => isActive ? 'nav-link active' : 'nav-link'}>
            {link.label}
          </NavLink>
        ))}
        <div className="nav-divider" />
        <div className="nav-section-header">Processing</div>
        {processingLinks.map(link => (
          <NavLink key={link.to} to={link.to} className={({ isActive }) => isActive ? 'nav-link active' : 'nav-link'}>
            {link.label}
          </NavLink>
        ))}
      </nav>
    </aside>
  );
}
