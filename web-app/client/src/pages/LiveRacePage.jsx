import { useParams } from 'react-router-dom';
import useRaceWebSocket from '../hooks/useRaceWebSocket.js';
import LiveLeaderboard from '../components/LiveLeaderboard.jsx';
import LiveEventFeed from '../components/LiveEventFeed.jsx';
import TrackMinimap from '../components/TrackMinimap.jsx';

const PHASE_LABELS = {
  Connecting: 'Connecting...',
  Setup: 'Waiting for Race',
  Racing: 'Race In Progress',
  Paused: 'Paused',
  Finished: 'Race Finished',
  Closed: 'Room Closed',
  Error: 'Room Not Found',
};

export default function LiveRacePage() {
  const { roomCode } = useParams();
  const { connected, gamePhase, cars, positions, leaderboard, events, raceTime } = useRaceWebSocket(roomCode);

  const phaseClass = {
    Setup: 'room-status-setup',
    Racing: 'room-status-racing',
    Paused: 'room-status-racing',
    Finished: 'room-status-finished',
    Error: 'room-status-error',
    Closed: 'room-status-error',
  }[gamePhase] || 'room-status-setup';

  return (
    <div className="live-race-page">
      <header className="live-header">
        <h1>Live Race</h1>
        <div className="live-room-info">
          <span className="live-room-code">Room {roomCode?.toUpperCase()}</span>
          <div className={`room-status ${phaseClass}`}>
            <span className="status-dot"></span>
            {PHASE_LABELS[gamePhase] || gamePhase}
          </div>
        </div>
        {gamePhase === 'Racing' && (
          <span className="live-timer">{raceTime.toFixed(1)}s</span>
        )}
        <span className={`live-connection ${connected ? 'connected' : 'disconnected'}`}>
          {connected ? 'Connected' : 'Disconnected'}
        </span>
      </header>

      {(gamePhase === 'Error' || gamePhase === 'Closed') && (
        <div className="live-message">
          <p>{gamePhase === 'Error' ? 'Room not found. Check the room code and try again.' : 'The room has been closed by the host.'}</p>
        </div>
      )}

      {gamePhase === 'Setup' && (
        <div className="live-message">
          <p>Connected to room. Waiting for the host to start the race...</p>
        </div>
      )}

      {(gamePhase === 'Racing' || gamePhase === 'Paused' || gamePhase === 'Finished') && (
        <div className="live-grid">
          <LiveLeaderboard rankings={leaderboard} />
          <TrackMinimap positions={positions} cars={cars} />
          <LiveEventFeed events={events} />
        </div>
      )}
    </div>
  );
}
