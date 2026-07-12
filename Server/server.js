const { WebSocketServer } = require('ws');

const PORT = parseInt(process.env.PORT || '8080', 10);
const HEARTBEAT_INTERVAL = 30000;

// Room: { professor: WebSocket, students: Set<WebSocket>, raceStarted: boolean, latestState: string|null }
const rooms = new Map();
const clientRooms = new Map(); // WebSocket -> { roomCode, role }

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

function cleanupClient(ws) {
  const info = clientRooms.get(ws);
  if (!info) return;
  clientRooms.delete(ws);

  const room = rooms.get(info.roomCode);
  if (!room) return;

  if (info.role === 'professor') {
    // Professor left — close room, notify students
    broadcastToStudents(info.roomCode, { type: 'room_closed' });
    for (const student of room.students) {
      clientRooms.delete(student);
    }
    rooms.delete(info.roomCode);
    console.log(`[Room ${info.roomCode}] Closed (professor disconnected)`);
  } else {
    room.students.delete(ws);
    // Notify professor of updated count
    if (room.professor && room.professor.readyState === 1) {
      sendJSON(room.professor, { type: 'student_count', count: room.students.size });
    }
    console.log(`[Room ${info.roomCode}] Student left (${room.students.size} remaining)`);
  }
}

const wss = new WebSocketServer({ port: PORT });

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
          raceStarted: false,
          latestState: null,
        });
        clientRooms.set(ws, { roomCode, role: 'professor' });
        sendJSON(ws, { type: 'room_created', roomCode });
        console.log(`[Room ${roomCode}] Created`);
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
        clientRooms.set(ws, { roomCode: code, role: 'student' });
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
            room.latestState = raw;
          } else if (msg.type === 'state_update') {
            room.latestState = raw;
          } else if (msg.type === 'survey_questions') {
            room.surveyData = raw; // Cache for late-joiners
          }

          broadcastToStudents(info.roomCode, raw);
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

console.log(`WebSocket server listening on port ${PORT}`);
