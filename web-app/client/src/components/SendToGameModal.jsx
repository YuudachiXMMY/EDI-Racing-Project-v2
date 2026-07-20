import { useState } from 'react';
import { sendToGame } from '../api.js';

const ROOM_CODE_KEY = 'edi-last-room-code';

export default function SendToGameModal({ surveyId, onClose }) {
  const [roomCode, setRoomCode] = useState(() => localStorage.getItem(ROOM_CODE_KEY) || '');
  const [status, setStatus] = useState('idle'); // idle | sending | success | error
  const [message, setMessage] = useState('');

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

    const result = await sendToGame(surveyId, code);

    if (result.success) {
      setStatus('success');
      setMessage(`Sent! ${result.data.carsCount} car(s), ${result.data.rulesCount} rule(s) loaded in game.`);
    } else {
      setStatus('error');
      setMessage(result.error || 'Failed to send data to game.');
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content send-to-game-modal" onClick={e => e.stopPropagation()}>
        <h3>Send to Game</h3>
        <p className="modal-hint">
          Host a room in the Unity game first, then enter the room code below.
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

        {message && (
          <p className={`modal-message ${status}`}>{message}</p>
        )}

        <div className="modal-actions">
          {status !== 'success' && (
            <button
              onClick={handleSend}
              className="btn-primary"
              disabled={status === 'sending'}
            >
              {status === 'sending' ? 'Sending...' : 'Send'}
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
