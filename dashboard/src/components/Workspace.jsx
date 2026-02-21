import React from 'react';
import { Outlet } from 'react-router-dom';
import { TourProvider } from '../context/TourContext';
import Sidebar from './Sidebar';
import Topbar from './Topbar';
import Tour from './tour/Tour';
import SetupWizard from './wizard/SetupWizard';
import './Workspace.css';

export default function Workspace() {
  return (
    <TourProvider>
      <div className="workspace">
        <Sidebar />
        <div className="workspace-main">
          <Topbar />
          <div className="workspace-content">
            <Outlet />
          </div>
        </div>
        <Tour />
        <SetupWizard />
      </div>
    </TourProvider>
  );
}
