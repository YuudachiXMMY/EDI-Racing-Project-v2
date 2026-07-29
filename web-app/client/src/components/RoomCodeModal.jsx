import RoomStatusBadge from './RoomStatusBadge.jsx';

/**
 * Shared shell for the room-code send modals (Send-to-Game / Send-Config).
 * Owns the overlay, room-code input, status badge, message line, and action buttons.
 * Callers supply the room-status state (via useRoomStatus), the send handler, and any
 * extra content (e.g. a "Watch Live Race" link) through `children`.
 */
export default function RoomCodeModal({
  title,
  hint,
  sendLabel,
  roomCode,
  setRoomCode,
  roomStatus,
  checking,
  status,
  message,
  canSend,
  onSend,
  onClose,
  children,
}) {
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content send-to-game-modal" onClick={e => e.stopPropagation()}>
        <h3>{title}</h3>
        <p className="modal-hint">{hint}</p>

        <div className="modal-field">
          <label>Room Code</label>
          <input
            type="text"
            value={roomCode}
            onChange={e => setRoomCode(e.target.value.toUpperCase())}
            placeholder="e.g. ABCDEF"
            maxLength={8}
            disabled={status === 'sending'}
          />
        </div>

        <RoomStatusBadge status={roomStatus} checking={checking} />

        {children}

        {message && (
          <p className={`modal-message ${status}`}>{message}</p>
        )}

        <div className="modal-actions">
          {status !== 'success' && (
            <button
              onClick={onSend}
              className="btn-primary"
              disabled={!canSend}
            >
              {status === 'sending' ? 'Sending...' : sendLabel}
            </button>
          )}
          <button onClick={onClose} className="btn-secondary">
            {status === 'success' ? 'Done' : 'Cancel'}
          </button>
        </div>
      </div>
    </div>
  );
}
