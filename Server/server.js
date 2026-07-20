const http = require('http');
const { WebSocketServer } = require('ws');

const PORT = parseInt(process.env.PORT || '8080', 10);
const HEARTBEAT_INTERVAL = 30000;
const PROFESSOR_GRACE_PERIOD = 60000; // 60s before room deletion after professor disconnect

// Room: { professor: WebSocket|null, students: Set<WebSocket>, webapps: Set<WebSocket>, raceStarted: boolean, latestState: string|null, gamePhase: string, raceResults: string|null, surveyData: string|null, professorSessionId: string|null, graceTimer: NodeJS.Timeout|null }
const rooms = new Map();
const clientRooms = new Map(); // WebSocket -> { roomCode, role, sessionId }
const sessions = new Map();   // sessionId -> { roomCode, role }

function generateRoomCode() {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // no I/O/0/1 to avoid confusion
  let code;
  do {
    code = '';
    for (let i = 0; i < 6; i++) {
      code += chars[Math.floor(Math.random() * chars.length)];
    }
  } while (rooms.has(code));
  return code;
}

function sendJSON(ws, obj) {
  if (ws.readyState === 1) {
    ws.send(JSON.stringify(obj));
  }
}

function broadcastToStudents(roomCode, message) {
  const room = rooms.get(roomCode);
  if (!room) return;
  const data = typeof message === 'string' ? message : JSON.stringify(message);
  for (const student of room.students) {
    if (student.readyState === 1) {
      student.send(data);
    }
  }
}

function destroyRoom(roomCode) {
  const room = rooms.get(roomCode);
  if (!room) return;
  if (room.graceTimer) clearTimeout(room.graceTimer);
  broadcastToStudents(roomCode, { type: 'room_closed' });
  for (const student of room.students) {
    clientRooms.delete(student);
  }
  // Clean up session references for this room
  for (const [sid, info] of sessions) {
    if (info.roomCode === roomCode) sessions.delete(sid);
  }
  rooms.delete(roomCode);
  console.log(`[Room ${roomCode}] Destroyed`);
}

function cleanupClient(ws) {
  const info = clientRooms.get(ws);
  if (!info) return;
  clientRooms.delete(ws);

  const room = rooms.get(info.roomCode);
  if (!room) return;

  if (info.role === 'professor') {
    // Professor left — suspend room with grace period instead of immediate deletion
    room.professor = null;
    broadcastToStudents(info.roomCode, { type: 'host_reconnecting' });
    console.log(`[Room ${info.roomCode}] Professor disconnected — grace period ${PROFESSOR_GRACE_PERIOD / 1000}s`);

    room.graceTimer = setTimeout(() => {
      room.graceTimer = null;
      if (!room.professor) {
        console.log(`[Room ${info.roomCode}] Grace period expired`);
        destroyRoom(info.roomCode);
      }
    }, PROFESSOR_GRACE_PERIOD);
  } else if (info.role === 'webapp') {
    room.webapps.delete(ws);
    console.log(`[Room ${info.roomCode}] Web-app client disconnected`);
  } else {
    room.students.delete(ws);
    // Notify professor of updated count
    if (room.professor && room.professor.readyState === 1) {
      sendJSON(room.professor, { type: 'student_count', count: room.students.size });
    }
    console.log(`[Room ${info.roomCode}] Student left (${room.students.size} remaining)`);
  }
}

// --- HTTP server for room-status API ---
const server = http.createServer((req, res) => {
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET');
  res.setHeader('Content-Type', 'application/json');

  const match = req.url.match(/^\/api\/room-status\/([A-Za-z0-9]+)$/);
  if (req.method === 'GET' && match) {
    const code = match[1].toUpperCase();
    const room = rooms.get(code);
    if (!room) {
      res.writeHead(200);
      res.end(JSON.stringify({ exists: false }));
      return;
    }
    res.writeHead(200);
    res.end(JSON.stringify({
      exists: true,
      roomCode: code,
      studentCount: room.students.size,
      gamePhase: room.gamePhase || 'Setup',
      raceStarted: room.raceStarted,
    }));
    return;
  }

  const resultsMatch = req.url.match(/^\/api\/room-results\/([A-Za-z0-9]+)$/);
  if (req.method === 'GET' && resultsMatch) {
    const code = resultsMatch[1].toUpperCase();
    const room = rooms.get(code);
    if (!room || !room.raceResults) {
      res.writeHead(200);
      res.end(JSON.stringify({ exists: false }));
      return;
    }
    res.writeHead(200);
    res.end(room.raceResults);
    return;
  }

  res.writeHead(404);
  res.end(JSON.stringify({ error: 'Not found' }));
});

// --- WebSocket server attached to HTTP server ---
const wss = new WebSocketServer({ server });

// Heartbeat to detect dead connections
const heartbeat = setInterval(() => {
  for (const ws of wss.clients) {
    if (ws.isAlive === false) {
      cleanupClient(ws);
      ws.terminate();
      continue;
    }
    ws.isAlive = false;
    ws.ping();
  }
}, HEARTBEAT_INTERVAL);

wss.on('close', () => clearInterval(heartbeat));

wss.on('connection', (ws) => {
  ws.isAlive = true;
  ws.on('pong', () => { ws.isAlive = true; });

  ws.on('message', (data) => {
    let msg;
    try {
      msg = JSON.parse(data.toString());
    } catch {
      return;
    }

    switch (msg.type) {
      case 'create_room': {
        const roomCode = generateRoomCode();
        rooms.set(roomCode, {
          professor: ws,
          students: new Set(),
          webapps: new Set(),
          raceStarted: false,
          latestState: null,
          gamePhase: 'Setup',
          raceResults: null,
          surveyData: null,
          professorSessionId: msg.sessionId || null,
          graceTimer: null,
        });
        const clientInfo = { roomCode, role: 'professor', sessionId: msg.sessionId || null };
        clientRooms.set(ws, clientInfo);
        if (msg.sessionId) {
          sessions.set(msg.sessionId, { roomCode, role: 'professor' });
        }
        sendJSON(ws, { type: 'room_created', roomCode });
        console.log(`[Room ${roomCode}] Created${msg.sessionId ? ` (session: ${msg.sessionId})` : ''}`);
        break;
      }

      case 'join_room': {
        const code = (msg.roomCode || '').toUpperCase();
        const room = rooms.get(code);
        if (!room) {
          sendJSON(ws, { type: 'error', message: 'Room not found' });
          return;
        }
        room.students.add(ws);
        const clientInfo = { roomCode: code, role: 'student', sessionId: msg.sessionId || null };
        clientRooms.set(ws, clientInfo);
        if (msg.sessionId) {
          sessions.set(msg.sessionId, { roomCode: code, role: 'student' });
        }
        sendJSON(ws, { type: 'room_joined', roomCode: code });
        // Notify professor
        if (room.professor && room.professor.readyState === 1) {
          sendJSON(room.professor, { type: 'student_count', count: room.students.size });
        }
        // Send cached survey to late-joiner (if survey distributed but race not started)
        if (room.surveyData && !room.raceStarted) {
          ws.send(room.surveyData);
        }
        // If race already started, send latest state to late-joiner
        if (room.latestState) {
          ws.send(room.latestState);
        }
        console.log(`[Room ${code}] Student joined (${room.students.size} total)`);
        break;
      }

      case 'rejoin_room': {
        const code = (msg.roomCode || '').toUpperCase();
        const sid = msg.sessionId || '';
        const room = rooms.get(code);

        if (!room || !sid) {
          sendJSON(ws, { type: 'error', message: 'Room not found or invalid session' });
          return;
        }

        const sessionInfo = sessions.get(sid);
        if (!sessionInfo || sessionInfo.roomCode !== code) {
          sendJSON(ws, { type: 'error', message: 'Session not recognized for this room' });
          return;
        }

        if (sessionInfo.role === 'professor') {
          if (room.graceTimer) {
            clearTimeout(room.graceTimer);
            room.graceTimer = null;
          }
          room.professor = ws;
          clientRooms.set(ws, { roomCode: code, role: 'professor', sessionId: sid });
          broadcastToStudents(code, { type: 'host_reconnected' });
          sendJSON(ws, {
            type: 'reconnect_state',
            gamePhase: room.gamePhase || 'Setup',
            studentCount: room.students.size,
            raceStarted: room.raceStarted,
          });
          if (room.latestState) {
            ws.send(room.latestState);
          }
          console.log(`[Room ${code}] Professor reconnected (session: ${sid})`);
        } else {
          room.students.add(ws);
          clientRooms.set(ws, { roomCode: code, role: 'student', sessionId: sid });
          sendJSON(ws, {
            type: 'reconnect_state',
            gamePhase: room.gamePhase || 'Setup',
            studentCount: room.students.size,
            raceStarted: room.raceStarted,
          });
          if (room.surveyData && !room.raceStarted) {
            ws.send(room.surveyData);
          }
          if (room.latestState) {
            ws.send(room.latestState);
          }
          if (room.professor && room.professor.readyState === 1) {
            sendJSON(room.professor, { type: 'student_count', count: room.students.size });
          }
          console.log(`[Room ${code}] Student reconnected (${room.students.size} total)`);
        }
        break;
      }

      case 'web_join_room': {
        const webCode = (msg.roomCode || '').toUpperCase();
        const webRoom = rooms.get(webCode);
        if (!webRoom) {
          sendJSON(ws, { type: 'error', message: 'Room not found' });
          return;
        }
        webRoom.webapps.add(ws);
        clientRooms.set(ws, { roomCode: webCode, role: 'webapp' });
        sendJSON(ws, { type: 'room_joined', roomCode: webCode });
        console.log(`[Room ${webCode}] Web-app client joined`);
        break;
      }

      case 'survey_import': {
        const webInfo = clientRooms.get(ws);
        if (!webInfo || webInfo.role !== 'webapp') {
          sendJSON(ws, { type: 'error', message: 'Not authorized' });
          return;
        }
        const importRoom = rooms.get(webInfo.roomCode);
        if (!importRoom || !importRoom.professor || importRoom.professor.readyState !== 1) {
          sendJSON(ws, { type: 'survey_import_ack', success: false, error: 'Professor not connected' });
          return;
        }
        importRoom.professor.send(data.toString());
        sendJSON(ws, { type: 'survey_import_ack', success: true });
        console.log(`[Room ${webInfo.roomCode}] Survey data sent from web-app to professor`);
        break;
      }

      default: {
        const info = clientRooms.get(ws);
        if (!info) return;
        const room = rooms.get(info.roomCode);
        if (!room) return;

        const raw = data.toString();

        if (info.role === 'professor') {
          // Professor → Students relay
          if (msg.type === 'race_start') {
            room.raceStarted = true;
            room.gamePhase = 'Racing';
            room.latestState = raw;
          } else if (msg.type === 'state_update') {
            room.latestState = raw;
          } else if (msg.type === 'survey_questions') {
            room.surveyData = raw;
          } else if (msg.type === 'game_state') {
            room.gamePhase = msg.state || 'Setup';
          } else if (msg.type === 'race_results') {
            room.raceResults = raw;
            room.gamePhase = 'Finished';
          } else if (msg.type === 'race_end') {
            room.gamePhase = 'Finished';
          }

          broadcastToStudents(info.roomCode, raw);

          // Also relay race_results to web-app clients
          if (msg.type === 'race_results') {
            for (const webapp of room.webapps) {
              if (webapp.readyState === 1) webapp.send(raw);
            }
          }
        } else if (info.role === 'student') {
          // Student → Professor relay
          if (room.professor && room.professor.readyState === 1) {
            room.professor.send(raw);
          }
        }
        break;
      }
    }
  });

  ws.on('close', () => cleanupClient(ws));
  ws.on('error', () => cleanupClient(ws));
});

server.listen(PORT, () => {
  console.log(`WebSocket + HTTP server listening on port ${PORT}`);
});
