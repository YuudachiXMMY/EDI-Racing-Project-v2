import WebSocket from 'ws';
import { WS_GAME_URL } from '../config.js';

/**
 * Connect to the game relay, join a room, and drive the request/response handshake.
 * The shared skeleton owns: connection, 5000ms timeout -> 504, sending `web_join_room`,
 * JSON.parse guard, `error` message -> 400, socket error -> 502, and the `responded`
 * idempotency flag. Endpoint-specific behavior is injected via callbacks.
 *
 * @param {import('express').Response} res - Express response
 * @param {Object} opts
 * @param {string} opts.code - normalized room code
 * @param {(ws: WebSocket) => void} opts.onRoomJoined - send the import message once the room is joined
 * @param {(msg: any, res: import('express').Response, ws: WebSocket, done: () => void) => void} opts.handleAck
 *        - handle the ack message; call `done()` before responding to settle the request
 */
export function sendToGameRoom(res, { code, onRoomJoined, handleAck }) {
  const ws = new WebSocket(WS_GAME_URL);
  let responded = false;

  const timeout = setTimeout(() => {
    if (!responded) {
      responded = true;
      ws.close();
      res.status(504).json({ success: false, error: 'Game server did not respond in time' });
    }
  }, 5000);

  // Settle the pending request exactly once: mark responded, cancel the timeout, close the socket.
  const done = () => {
    responded = true;
    clearTimeout(timeout);
    ws.close();
  };

  ws.on('open', () => {
    ws.send(JSON.stringify({ type: 'web_join_room', roomCode: code }));
  });

  ws.on('message', (data) => {
    if (responded) return;
    let msg;
    try {
      msg = JSON.parse(data.toString());
    } catch {
      return;
    }

    if (msg.type === 'error') {
      done();
      return res.status(400).json({ success: false, error: msg.message || 'Room not found' });
    }

    if (msg.type === 'room_joined') {
      onRoomJoined(ws);
      return;
    }

    handleAck(msg, res, ws, done);
  });

  ws.on('error', () => {
    if (!responded) {
      responded = true;
      clearTimeout(timeout);
      res.status(502).json({ success: false, error: 'Cannot connect to game server' });
    }
  });
}
