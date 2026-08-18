import { useState, useEffect, useRef, useCallback } from 'react';

const RECONNECT_DELAY = 3000;
const MAX_RECONNECT = 5;

export default function useRaceWebSocket(roomCode) {
  const [connected, setConnected] = useState(false);
  const [gamePhase, setGamePhase] = useState('Connecting');
  const [cars, setCars] = useState([]);
  const [positions, setPositions] = useState([]);
  const [leaderboard, setLeaderboard] = useState([]);
  const [events, setEvents] = useState([]);
  const [raceTime, setRaceTime] = useState(0);
  const wsRef = useRef(null);
  const reconnectCount = useRef(0);
  const reconnectTimer = useRef(null);

  const connect = useCallback(() => {
    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${protocol}//${location.host}/ws`;
    const ws = new WebSocket(wsUrl);
    wsRef.current = ws;

    ws.onopen = () => {
      setConnected(true);
      reconnectCount.current = 0;
      ws.send(JSON.stringify({ type: 'web_join_room', roomCode: roomCode.toUpperCase() }));
    };

    ws.onmessage = (event) => {
      let msg;
      try {
        msg = JSON.parse(event.data);
      } catch {
        return;
      }

      switch (msg.type) {
        case 'room_joined':
          setGamePhase('Setup');
          break;
        case 'error':
          setGamePhase('Error');
          break;
        case 'race_start':
          setGamePhase('Racing');
          setCars(msg.cars || []);
          break;
        case 'state_update':
          setPositions(msg.cars || []);
          setRaceTime(msg.t || 0);
          break;
        case 'leaderboard':
          setLeaderboard(msg.rankings || []);
          break;
        case 'game_state':
          setGamePhase(msg.state || 'Setup');
          break;
        case 'event_triggered':
          setEvents(prev => [{ ...msg, timestamp: Date.now() }, ...prev].slice(0, 50));
          break;
        case 'race_end':
        case 'race_results':
          setGamePhase('Finished');
          break;
        case 'room_closed':
          setGamePhase('Closed');
          break;
      }
    };

    ws.onclose = () => {
      setConnected(false);
      wsRef.current = null;
      if (reconnectCount.current < MAX_RECONNECT) {
        reconnectCount.current++;
        reconnectTimer.current = setTimeout(connect, RECONNECT_DELAY);
      }
    };

    ws.onerror = () => ws.close();
  }, [roomCode]);

  useEffect(() => {
    if (!roomCode) return;
    connect();
    return () => {
      reconnectCount.current = MAX_RECONNECT;
      clearTimeout(reconnectTimer.current);
      if (wsRef.current) wsRef.current.close();
    };
  }, [roomCode, connect]);

  return { connected, gamePhase, cars, positions, leaderboard, events, raceTime };
}
