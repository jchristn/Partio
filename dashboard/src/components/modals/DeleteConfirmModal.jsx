import Modal from '../Modal';
import './DeleteConfirmModal.css';

const DeleteConfirmModal = ({
  isOpen,
  onClose,
  onConfirm,
  entityName,
  entityType,
  title = 'Confirm Delete',
  actionLabel = 'Delete',
  message,
  warningMessage = 'This action cannot be undone.'
}) => {
  if (!isOpen) return null;

  const defaultMessage = `Are you sure you want to ${actionLabel.toLowerCase()} this ${entityType}?`;

  return (
    <Modal title={title} onClose={onClose}>
      <div className="delete-confirm-modal">
        <div className="delete-warning-icon">
          <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="12" y1="8" x2="12" y2="12"></line>
            <line x1="12" y1="16" x2="12.01" y2="16"></line>
          </svg>
        </div>
        <p className="delete-message">
          {message || defaultMessage}
        </p>
        {entityName && (
          <p className="delete-entity-name">{entityName}</p>
        )}
        {warningMessage && (
          <p className="delete-warning">{warningMessage}</p>
        )}
        <div className="delete-actions">
          <button className="btn-delete-cancel" onClick={onClose}>
            Cancel
          </button>
          <button className="btn-delete-confirm" onClick={onConfirm}>
            {actionLabel}
          </button>
        </div>
      </div>
    </Modal>
  );
};

export default DeleteConfirmModal;
