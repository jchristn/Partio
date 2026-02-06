import React from 'react';
import { NavLink } from 'react-router-dom';
import './Sidebar.css';

const links = [
  { to: '/tenants', label: 'Tenants' },
  { to: '/users', label: 'Users' },
  { to: '/credentials', label: 'Credentials' },
  { to: '/endpoints', label: 'Endpoints' },
  { to: '/history', label: 'Request History' },
  { to: '/process', label: 'Chunk & Embed' },
];

export default function Sidebar() {
  return (
    <aside className="sidebar">
      <div className="sidebar-header">
        <h2>Partio</h2>
      </div>
      <nav className="sidebar-nav">
        {links.map(link => (
          <NavLink key={link.to} to={link.to} className={({ isActive }) => isActive ? 'nav-link active' : 'nav-link'}>
            {link.label}
          </NavLink>
        ))}
      </nav>
    </aside>
  );
}
