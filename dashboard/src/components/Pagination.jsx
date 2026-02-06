import React from 'react';
import './Pagination.css';

export default function Pagination({ hasMore, onNext, onReset, loading }) {
  return (
    <div className="pagination">
      <button className="secondary" onClick={onReset} disabled={loading}>First Page</button>
      {hasMore && <button className="primary" onClick={onNext} disabled={loading}>Next Page</button>}
    </div>
  );
}
