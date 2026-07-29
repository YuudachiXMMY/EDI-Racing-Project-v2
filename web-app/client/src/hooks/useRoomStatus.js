import { useState, useEffect, useRef } from 'react';
import { getRoomStatus } from '../api.js';

export const ROOM_CODE_KEY = 'edi-last-room-code';
const DEBOUNCE_DELAY = 800;
const MIN_CODE_LENGTH = 4;
const POLL_INTERVAL = 5000;

/**
 * Room-code debounce + status polling shared by the Send-to-Game / Send-Config modals.
 * Seeds the room code from localStorage, debounces status lookups, and (when `poll` is
 * true) re-checks on an interval. Calls `onFinished(trimmedCode)` each time the room
 * reports `gamePhase === 'Finished'` — the caller owns any once-only guard.
 */
export default function useRoomStatus({ poll = false, onFinished } = {}) {
  const [roomCode, setRoomCode] = useState(() => localStorage.getItem(ROOM_CODE_KEY) || '');
  const [roomStatus, setRoomStatus] = useState(null);
  const [checking, setChecking] = useState(false);
  const debounceRef = useRef(null);
  const pollRef = useRef(null);
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
    const data = result.success ? result.data : { exists: false, error: 'Failed to check' };
    setRoomStatus(data);
    setChecking(false);

    if (onFinished && data && data.gamePhase === 'Finished') {
      await onFinished(trimmed);
    }
  }

  // Debounced check + optional polling when roomCode changes
  useEffect(() => {
    abortRef.current = false;
    clearTimeout(debounceRef.current);
    clearInterval(pollRef.current);
    setRoomStatus(null);

    const trimmed = roomCode.trim();
    if (trimmed.length < MIN_CODE_LENGTH) return;

    debounceRef.current = setTimeout(() => {
      fetchStatus(trimmed);
      if (poll) {
        pollRef.current = setInterval(() => fetchStatus(trimmed), POLL_INTERVAL);
      }
    }, DEBOUNCE_DELAY);

    return () => {
      abortRef.current = true;
      clearTimeout(debounceRef.current);
      clearInterval(pollRef.current);
    };
  }, [roomCode]);

  // Cleanup on unmount
  useEffect(() => {
    return () => { abortRef.current = true; };
  }, []);

  function pausePolling() {
    clearInterval(pollRef.current);
  }

  return { roomCode, setRoomCode, roomStatus, checking, pausePolling };
}
