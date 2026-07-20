import { useState, useEffect, useRef } from 'react';
import { sendToGame, getRoomStatus, getRoomResults, saveRaceResults } from '../api.js';
import RoomStatusBadge from './RoomStatusBadge.jsx';

const ROOM_CODE_KEY = 'edi-last-room-code';
const POLL_INTERVAL = 5000;
const DEBOUNCE_DELAY = 800;
const MIN_CODE_LENGTH = 4;

export default function SendToGameModal({ surveyId, onClose }) {
  const [roomCode, setRoomCode] = useState(() => localStorage.getItem(ROOM_CODE_KEY) || '');
  const [status, setStatus] = useState('idle'); // idle | sending | success | error
  const [message, setMessage] = useState('');
  const [roomStatus, setRoomStatus] = useState(null);
  const [checking, setChecking] = useState(false);
  const [resultsSaved, setResultsSaved] = useState(false);
  const debounceRef = useRef(null);
  const pollRef = useRef(null);
  const abortRef = useRef(false);

  // Fetch room status once
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
    const data = result.success ? result.data : { exists: false, error: 'Failed to check' };
    setRoomStatus(data);
    setChecking(false);

    // Auto-save race results when race finishes
    if (data && data.gamePhase === 'Finished' && !resultsSaved) {
      try {
        const roomRes = await getRoomResults(trimmed);
        if (roomRes.success && roomRes.data && roomRes.data.type === 'race_results') {
          const parsed = JSON.parse(roomRes.data.resultsJson);
          await saveRaceResults(surveyId, {
            roomCode: trimmed,
            configName: roomRes.data.configName || '',
            rankings: parsed.Rankings || [],
            eventLog: parsed.EventLog || [],
            totalRaceTime: parsed.TotalRaceTime || 0,
          });
          setResultsSaved(true);
        }
      } catch {
        // Results fetch failed — professor can still view via Results tab later
      }
    }
  }

  // Debounced check + polling setup when roomCode changes
  useEffect(() => {
    abortRef.current = false;
    clearTimeout(debounceRef.current);
    clearInterval(pollRef.current);
    setRoomStatus(null);

    const trimmed = roomCode.trim();
    if (trimmed.length < MIN_CODE_LENGTH) return;

    debounceRef.current = setTimeout(() => {
      fetchStatus(trimmed);
      pollRef.current = setInterval(() => fetchStatus(trimmed), POLL_INTERVAL);
    }, DEBOUNCE_DELAY);

    return () => {
      abortRef.current = true;
      clearTimeout(debounceRef.current);
      clearInterval(pollRef.current);
    };
  }, [roomCode]);

  // Pause polling while sending
  useEffect(() => {
    if (status === 'sending') {
      clearInterval(pollRef.current);
    }
  }, [status]);

  // Cleanup on unmount
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

    const result = await sendToGame(surveyId, code);

    if (result.success) {
      setStatus('success');
      setMessage(`Sent! ${result.data.carsCount} car(s), ${result.data.rulesCount} rule(s) loaded in game.`);
    } else {
      setStatus('error');
      setMessage(result.error || 'Failed to send data to game.');
    }
  }

  const canSend = status !== 'sending' && !checking && (!roomStatus || roomStatus.exists !== false);

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

        <RoomStatusBadge status={roomStatus} checking={checking} />

        {roomStatus && roomStatus.exists && (
          <a
            href={`#/live/${roomCode.trim().toUpperCase()}`}
            target="_blank"
            rel="noopener"
            className="live-link"
          >
            Watch Live Race
          </a>
        )}

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
