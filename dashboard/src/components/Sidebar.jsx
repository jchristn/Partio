import React from 'react';
import { NavLink } from 'react-router-dom';
import { useTour } from '../context/TourContext';
import './Sidebar.css';

const adminLinks = [
  { to: '/tenants', label: 'Tenants', tourId: 'nav-tenants' },
  { to: '/users', label: 'Users', tourId: 'nav-users' },
  { to: '/credentials', label: 'Credentials', tourId: 'nav-credentials' },
];

const endpointLinks = [
  { to: '/endpoints/embeddings', label: 'Embeddings', tourId: 'nav-embeddings' },
  { to: '/endpoints/inference', label: 'Inference', tourId: 'nav-inference' },
];

const processingLinks = [
  { to: '/history', label: 'Request History', tourId: 'nav-history' },
  { to: '/process', label: 'Process Cells', tourId: 'nav-process' },
];

export default function Sidebar() {
  const { startTour, startWizard } = useTour();

  return (
    <aside className="sidebar">
      <div className="sidebar-header">
        <img src="/logo-light-text.png" alt="Partio" className="sidebar-logo" />
      </div>
      <nav className="sidebar-nav">
        <div className="nav-section-header" data-tour-id="nav-section-admin">Administration</div>
        {adminLinks.map(link => (
          <NavLink key={link.to} to={link.to} data-tour-id={link.tourId} className={({ isActive }) => isActive ? 'nav-link active' : 'nav-link'}>
            {link.label}
          </NavLink>
        ))}
        <div className="nav-divider" />
        <div className="nav-section-header" data-tour-id="nav-section-endpoints">Endpoints</div>
        {endpointLinks.map(link => (
          <NavLink key={link.to} to={link.to} data-tour-id={link.tourId} className={({ isActive }) => isActive ? 'nav-link active' : 'nav-link'}>
            {link.label}
          </NavLink>
        ))}
        <div className="nav-divider" />
        <div className="nav-section-header" data-tour-id="nav-section-processing">Processing</div>
        {processingLinks.map(link => (
          <NavLink key={link.to} to={link.to} data-tour-id={link.tourId} className={({ isActive }) => isActive ? 'nav-link active' : 'nav-link'}>
            {link.label}
          </NavLink>
        ))}
      </nav>
      <div className="sidebar-footer">
        <button className="sidebar-footer-btn" onClick={startTour}>Tour</button>
        <button className="sidebar-footer-btn" onClick={startWizard}>Setup Wizard</button>
      </div>
    </aside>
  );
}
