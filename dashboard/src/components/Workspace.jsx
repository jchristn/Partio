import React from 'react';
import { Outlet } from 'react-router-dom';
import Sidebar from './Sidebar';
import Topbar from './Topbar';
import './Workspace.css';

export default function Workspace() {
  return (
    <div className="workspace">
      <Sidebar />
      <div className="workspace-main">
        <Topbar />
        <div className="workspace-content">
          <Outlet />
        </div>
      </div>
    </div>
  );
}
