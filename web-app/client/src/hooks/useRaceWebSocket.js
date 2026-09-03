import { useState, useEffect, useRef, useCallback } from 'react';
import { deriveSpeed } from '../lib/carStatus.js';

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
  const [trackGeometry, setTrackGeometry] = useState(null);
  const wsRef = useRef(null);
  const reconnectCount = useRef(0);
  const reconnectTimer = useRef(null);
  // Previous position frame + its timestamp, for the client-side speed fallback used when the
  // deployed Unity build does not yet emit an authoritative CarNetState.s.
  const prevPositions = useRef([]);
  const prevTime = useRef(0);

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
          // New race -> drop any stale frame so the first speed delta is not a teleport spike.
          prevPositions.current = [];
          prevTime.current = 0;
          // Drop the previous race's map so it is not shown before this race's track_geometry
          // arrives (the minimap re-fits/re-accumulates from scratch — see TrackMinimap).
          setTrackGeometry(null);
          break;
        case 'state_update': {
          const frame = msg.cars || [];
          const t = msg.t || 0;
          const dt = t - prevTime.current;
          // If Unity already sends `s`, pass through untouched; otherwise derive an approximate
          // speed from the previous frame and flag it so the UI can label it "approx".
          const augmented = frame.map((c) => {
            if (typeof c.s === 'number') return c;
            const prev = prevPositions.current.find((p) => p.i === c.i);
            const s = deriveSpeed(prev, c, dt);
            return s === undefined ? c : { ...c, s, sApprox: true };
          });
          prevPositions.current = frame;
          prevTime.current = t;
          setPositions(augmented);
          setRaceTime(t);
          break;
        }
        case 'leaderboard':
          setLeaderboard(msg.rankings || []);
          break;
        case 'track_geometry':
          setTrackGeometry(msg);
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

  return { connected, gamePhase, cars, positions, leaderboard, events, raceTime, trackGeometry };
}
