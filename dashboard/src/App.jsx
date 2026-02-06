import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { useApp } from './context/AppContext';
import Login from './components/Login';
import Workspace from './components/Workspace';
import TenantsView from './components/TenantsView';
import UsersView from './components/UsersView';
import CredentialsView from './components/CredentialsView';
import EmbeddingEndpointsView from './components/EmbeddingEndpointsView';
import RequestHistoryView from './components/RequestHistoryView';
import ChunkEmbedView from './components/ChunkEmbedView';

function ProtectedRoute({ children }) {
  const { isConnected } = useApp();
  if (!isConnected) return <Navigate to="/login" replace />;
  return children;
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/" element={<ProtectedRoute><Workspace /></ProtectedRoute>}>
          <Route index element={<TenantsView />} />
          <Route path="tenants" element={<TenantsView />} />
          <Route path="users" element={<UsersView />} />
          <Route path="credentials" element={<CredentialsView />} />
          <Route path="endpoints" element={<EmbeddingEndpointsView />} />
          <Route path="history" element={<RequestHistoryView />} />
          <Route path="process" element={<ChunkEmbedView />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
