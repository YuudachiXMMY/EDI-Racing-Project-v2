export default function RoomStatusBadge({ status, checking }) {
  if (checking) {
    return (
      <div className="room-status room-status-setup">
        <span className="status-dot"></span>
        Checking room...
      </div>
    );
  }

  if (!status) return null;

  if (!status.exists) {
    return (
      <div className="room-status room-status-error">
        <span className="status-dot"></span>
        {status.error || 'Room not found'}
      </div>
    );
  }

  const phaseClass = {
    Setup: 'room-status-setup',
    Racing: 'room-status-racing',
    Paused: 'room-status-racing',
    Finished: 'room-status-finished',
  }[status.gamePhase] || 'room-status-setup';

  return (
    <div className={`room-status ${phaseClass}`}>
      <span className="status-dot"></span>
      <div>
        <strong>Room {status.roomCode}</strong> — {status.gamePhase}
        <br />
        <span className="status-detail">{status.studentCount} student(s) connected</span>
      </div>
    </div>
  );
}
