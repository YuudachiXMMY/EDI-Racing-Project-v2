import { useState, useEffect, useRef } from 'react';
import { sendConfigToGame, getRoomStatus } from '../api.js';
import RoomStatusBadge from './RoomStatusBadge.jsx';

const ROOM_CODE_KEY = 'edi-last-room-code';
const DEBOUNCE_DELAY = 800;
const MIN_CODE_LENGTH = 4;

export default function SendConfigModal({ surveyId, onClose }) {
  const [roomCode, setRoomCode] = useState(() => localStorage.getItem(ROOM_CODE_KEY) || '');
  const [status, setStatus] = useState('idle');
  const [message, setMessage] = useState('');
  const [roomStatus, setRoomStatus] = useState(null);
  const [checking, setChecking] = useState(false);
  const debounceRef = useRef(null);
  const abortRef = useRef(false);

  async function fetchStatus(code) {
    if (abortRef.current) return;
    const trimmed = code.trim().toUpperCase();
    if (trimmed.length < MIN_CODE_LENGTH) {
      setRoomStatus(null);
      setChecking(false);
      return;
    }
    setChecking(true);
    const result = await getRoomStatus(trimmed);
    if (abortRef.current) return;
    setRoomStatus(result.success ? result.data : { exists: false, error: 'Failed to check' });
    setChecking(false);
  }

  useEffect(() => {
    abortRef.current = false;
    clearTimeout(debounceRef.current);
    setRoomStatus(null);

    const trimmed = roomCode.trim();
    if (trimmed.length < MIN_CODE_LENGTH) return;

    debounceRef.current = setTimeout(() => fetchStatus(trimmed), DEBOUNCE_DELAY);

    return () => {
      abortRef.current = true;
      clearTimeout(debounceRef.current);
    };
  }, [roomCode]);

  useEffect(() => {
    return () => { abortRef.current = true; };
  }, []);

  async function handleSend() {
    const code = roomCode.trim().toUpperCase();
    if (!code) {
      setMessage('Enter the room code shown in the Unity game.');
      setStatus('error');
      return;
    }

    setStatus('sending');
    setMessage('');
    localStorage.setItem(ROOM_CODE_KEY, code);

    const result = await sendConfigToGame(surveyId, code);

    if (result.success) {
      setStatus('success');
      setMessage(`Config "${result.data.configName}" sent to Unity. It is now the active config in the game.`);
    } else {
      setStatus('error');
      setMessage(result.error || 'Failed to send config to game.');
    }
  }

  const canSend = status !== 'sending' && !checking && (!roomStatus || roomStatus.exists !== false);

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content send-to-game-modal" onClick={e => e.stopPropagation()}>
        <h3>Send Config to Game</h3>
        <p className="modal-hint">
          Send the raw survey config (questions, mappings, rules) to the Unity game.
          The professor can then use it as the active config without re-creating it.
        </p>

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

        {message && (
          <p className={`modal-message ${status}`}>{message}</p>
        )}

        <div className="modal-actions">
          {status !== 'success' && (
            <button
              onClick={handleSend}
              className="btn-primary"
              disabled={!canSend}
            >
              {status === 'sending' ? 'Sending...' : 'Send Config'}
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
