import { useState, useEffect, useRef } from 'react';
import { sendToGame, getRoomResults, saveRaceResults } from '../api.js';
import useRoomStatus, { ROOM_CODE_KEY } from '../hooks/useRoomStatus.js';
import RoomCodeModal from './RoomCodeModal.jsx';

export default function SendToGameModal({ surveyId, onClose }) {
  const [status, setStatus] = useState('idle'); // idle | sending | success | error
  const [message, setMessage] = useState('');
  const resultsSavedRef = useRef(false);

  // Auto-save race results once, when the room reports the race finished
  async function handleFinished(trimmed) {
    if (resultsSavedRef.current) return;
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
        resultsSavedRef.current = true;
      }
    } catch {
      // Results fetch failed — professor can still view via Results tab later
    }
  }

  const { roomCode, setRoomCode, roomStatus, checking, pausePolling } = useRoomStatus({
    poll: true,
    onFinished: handleFinished,
  });

  // Pause polling while sending. `pausePolling` only clears the hook's ref-held interval,
  // so it is ref-stable across renders and safe to omit from the dependency array.
  useEffect(() => {
    if (status === 'sending') pausePolling();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [status]);

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
    <RoomCodeModal
      title="Send to Game"
      hint="Host a room in the Unity game first, then enter the room code below."
      sendLabel="Send"
      roomCode={roomCode}
      setRoomCode={setRoomCode}
      roomStatus={roomStatus}
      checking={checking}
      status={status}
      message={message}
      canSend={canSend}
      onSend={handleSend}
      onClose={onClose}
    >
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
    </RoomCodeModal>
  );
}
