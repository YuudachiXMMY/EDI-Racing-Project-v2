import { useState } from 'react';
import { sendConfigToGame } from '../api.js';
import useRoomStatus, { ROOM_CODE_KEY } from '../hooks/useRoomStatus.js';
import RoomCodeModal from './RoomCodeModal.jsx';

export default function SendConfigModal({ surveyId, onClose }) {
  const [status, setStatus] = useState('idle');
  const [message, setMessage] = useState('');
  const { roomCode, setRoomCode, roomStatus, checking } = useRoomStatus({ poll: false });

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
    <RoomCodeModal
      title="Send Config to Game"
      hint="Send the raw survey config (questions, mappings, rules) to the Unity game. The professor can then use it as the active config without re-creating it."
      sendLabel="Send Config"
      roomCode={roomCode}
      setRoomCode={setRoomCode}
      roomStatus={roomStatus}
      checking={checking}
      status={status}
      message={message}
      canSend={canSend}
      onSend={handleSend}
      onClose={onClose}
    />
  );
}
