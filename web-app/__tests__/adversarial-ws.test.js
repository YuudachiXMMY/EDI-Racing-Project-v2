import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { WebSocket } from 'ws';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// Phase 7 adversarial QA — proves the host-token security boundary against a REAL
// Server/server.js process driven by REAL ws sockets, with enforcement ON. This is
// the automated half of "verify a student link cannot trigger events even via a
// crafted URL" (PRD role-bound-game-links, Phase 7).
//
// Security model under test (Server/server.js):
//   • create_room is token-gated when REQUIRE_HOST_TOKEN=true (lines 332-340).
//   • 'professor' is the ONLY role whose messages broadcast to students/web viewers;
//     that role is granted ONLY on a successful create_room. So gating create_room
//     transitively gates event triggering — a student socket's event_triggered relays
//     ONLY to the professor, never to other students or web viewers (lines 603-608).
//   • Host-only messages (survey_import / config_import / config_export) are role-gated.
//
// Determinism note: negative WS assertions ("socket X must NOT receive Y") use a bounded
// collect(ms) window rather than an unbounded wait. The OUTCOME is deterministic (the
// server never sends the frame); only the wall-clock window is fixed. Every negative
// window is paired with a positive assertion (the professor DID receive the relay) so a
// silently-dropped connection cannot masquerade as "correctly isolated".

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const SERVER_PATH = path.resolve(__dirname, '../../Server/server.js');

// A strong, NON-default secret. Enforcement + the default secret would trip the Server's
// boot guard (checkSecretConfig → fatal → exit 1), so the secret must be non-default AND
// shared by both the token minter (this process) and the verifier (the spawned Server).
const TEST_SECRET = 'phase7-adversarial-qa-secret-0123456789abcdef';
const PORT = 18080;
const WS_URL = `ws://127.0.0.1:${PORT}`;

let serverProc;
let mintHostToken; // bound to TEST_SECRET via a dynamic import (see beforeAll)
const openClients = [];

// --- test WebSocket client with an inbox + message awaiters ------------------------
function makeClient() {
  const ws = new WebSocket(WS_URL);
  openClients.push(ws);
  const inbox = [];
  const waiters = [];
  ws.on('message', (data) => {
    let msg;
    try { msg = JSON.parse(data.toString()); } catch { msg = { _raw: data.toString() }; }
    inbox.push(msg);
    for (let i = waiters.length - 1; i >= 0; i--) {
      if (waiters[i].pred(msg)) {
        waiters[i].resolve(msg);
        waiters.splice(i, 1);
      }
    }
  });
  return {
    ws,
    ready: () => (ws.readyState === WebSocket.OPEN ? Promise.resolve() : once(ws, 'open')),
    send: (obj) => ws.send(JSON.stringify(obj)),
    // Resolve with the first message (past or future) matching pred; reject on timeout.
    next(pred, timeoutMs = 1500) {
      const existing = inbox.find(pred);
      if (existing) return Promise.resolve(existing);
      return new Promise((resolve, reject) => {
        const w = { pred, resolve };
        waiters.push(w);
        setTimeout(() => {
          const idx = waiters.indexOf(w);
          if (idx >= 0) {
            waiters.splice(idx, 1);
            reject(new Error(`timeout after ${timeoutMs}ms waiting for message`));
          }
        }, timeoutMs);
      });
    },
    // Capture every message that arrives during the next `ms` (for negative assertions).
    // Records the inbox high-water mark synchronously, so call it BEFORE the triggering send.
    collect(ms) {
      const start = inbox.length;
      return new Promise((resolve) => setTimeout(() => resolve(inbox.slice(start)), ms));
    },
    close: () => ws.close(),
  };
}

// Open a client, create a room with a valid token, return { prof, roomCode }.
async function hostARoom(surveyId = 1) {
  const prof = makeClient();
  await prof.ready();
  prof.send({ type: 'create_room', hostToken: mintHostToken(surveyId).token });
  const created = await prof.next((m) => m.type === 'room_created');
  return { prof, roomCode: created.roomCode };
}

async function joinAsStudent(roomCode, teamName = '') {
  const s = makeClient();
  await s.ready();
  s.send({ type: 'join_room', roomCode, teamName });
  await s.next((m) => m.type === 'room_joined');
  return s;
}

beforeAll(async () => {
  // Set the secret BEFORE importing hostToken.js — it captures INTERNAL_SECRET at module
  // load. A dynamic import (no static import above) guarantees the module evaluates with
  // the env already in place, so mintHostToken here signs with TEST_SECRET.
  process.env.INTERNAL_SECRET = TEST_SECRET;
  ({ mintHostToken } = await import('../src/hostToken.js'));

  serverProc = spawn('node', [SERVER_PATH], {
    env: {
      ...process.env,
      PORT: String(PORT),
      REQUIRE_HOST_TOKEN: 'true',
      INTERNAL_SECRET: TEST_SECRET,
      API_URL: 'http://127.0.0.1:1', // unreachable; archive fetch is fire-and-forget
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  });

  let stderr = '';
  serverProc.stderr.on('data', (d) => { stderr += d.toString(); });

  // Resolve when the server logs its listen line; reject on early exit / missing deps.
  await new Promise((resolve, reject) => {
    const timer = setTimeout(
      () => reject(new Error(`server did not start in 10s. stderr:\n${stderr}`)),
      10000
    );
    serverProc.stdout.on('data', (d) => {
      if (/listening on port/i.test(d.toString())) {
        clearTimeout(timer);
        resolve();
      }
    });
    serverProc.on('exit', (code) => {
      clearTimeout(timer);
      const hint = /MODULE_NOT_FOUND/.test(stderr)
        ? ' (Server deps missing — run `npm install` in Server/)'
        : '';
      reject(new Error(`server exited early (code ${code})${hint}. stderr:\n${stderr}`));
    });
  });
}, 15000);

afterAll(async () => {
  for (const ws of openClients) {
    try { ws.close(); } catch { /* ignore */ }
  }
  if (serverProc && !serverProc.killed) {
    serverProc.kill();
    await once(serverProc, 'exit').catch(() => {});
  }
});

describe('create_room token gate (REQUIRE_HOST_TOKEN=true)', () => {
  it('rejects create_room with no host token', async () => {
    const c = makeClient();
    await c.ready();
    c.send({ type: 'create_room' });
    const msg = await c.next((m) => m.type === 'error' || m.type === 'room_created');
    expect(msg.type).toBe('error');
    expect(msg.message).toBe('Host authorization required');
  });

  it('rejects a tampered token', async () => {
    const c = makeClient();
    await c.ready();
    const { token } = mintHostToken(1);
    // Flip a signature char so the HMAC no longer matches.
    const tampered = token.slice(0, -1) + (token.endsWith('A') ? 'B' : 'A');
    c.send({ type: 'create_room', hostToken: tampered });
    const msg = await c.next((m) => m.type === 'error' || m.type === 'room_created');
    expect(msg.type).toBe('error');
  });

  it('rejects an expired token', async () => {
    const c = makeClient();
    await c.ready();
    // Mint 50 minutes in the past (TTL is 5 min) so exp < now on the server.
    const { token } = mintHostToken(1, Date.now() - 50 * 60 * 1000);
    c.send({ type: 'create_room', hostToken: token });
    const msg = await c.next((m) => m.type === 'error' || m.type === 'room_created');
    expect(msg.type).toBe('error');
  });

  it('accepts a valid token (positive control — the gate is not blanket-deny)', async () => {
    const c = makeClient();
    await c.ready();
    c.send({ type: 'create_room', hostToken: mintHostToken(42).token });
    const msg = await c.next((m) => m.type === 'error' || m.type === 'room_created');
    expect(msg.type).toBe('room_created');
    expect(typeof msg.roomCode).toBe('string');
    expect(msg.roomCode).toHaveLength(6);
  });

  it('rejects create_room from an already-joined student socket (no token)', async () => {
    const { roomCode } = await hostARoom();
    const student = await joinAsStudent(roomCode, 'red');
    // An established student socket tries to seize host — still gated, no second room.
    student.send({ type: 'create_room' });
    const msg = await student.next((m) => m.type === 'error' || m.type === 'room_created');
    expect(msg.type).toBe('error');
    expect(msg.message).toBe('Host authorization required');
  });
});

describe('student cannot inject events (relay isolation)', () => {
  it('a student event_triggered reaches ONLY the professor, not other students or web viewers', async () => {
    const { prof, roomCode } = await hostARoom();
    const studentA = await joinAsStudent(roomCode, 'alpha');
    const studentB = await joinAsStudent(roomCode, 'beta');

    const webapp = makeClient();
    await webapp.ready();
    webapp.send({ type: 'web_join_room', roomCode });
    await webapp.next((m) => m.type === 'room_joined');

    // Start the negative windows BEFORE the adversarial send.
    const aWindow = studentA.collect(300);
    const webWindow = webapp.collect(300);

    // Student B forges an event — the headline attack.
    studentB.send({ type: 'event_triggered', eventType: 'oil_spill', carIndex: 0 });

    // Positive anchor: the professor DOES receive the relayed frame (proves the socket is
    // live and the timing window is long enough — so an empty negative window is meaningful).
    const relayed = await prof.next((m) => m.type === 'event_triggered');
    expect(relayed.eventType).toBe('oil_spill');

    // Negative: no other student and no web viewer sees the forged event.
    const aGot = await aWindow;
    const webGot = await webWindow;
    expect(aGot.filter((m) => m.type === 'event_triggered')).toHaveLength(0);
    expect(webGot.filter((m) => m.type === 'event_triggered')).toHaveLength(0);
  });
});

describe('survey -> room mapping (Host Game student-link discovery)', () => {
  const roomStatusUrl = (surveyId) => `http://127.0.0.1:${PORT}/api/survey-room/${surveyId}`;
  // The endpoint is internal-only: it requires the shared secret the web-app backend proves it
  // with. The spawned Server runs under TEST_SECRET (see beforeAll), so callers must present it.
  const internalHeaders = { 'x-internal-secret': TEST_SECRET };
  const fetchRoom = (surveyId) => fetch(roomStatusUrl(surveyId), { headers: internalHeaders });

  it('resolves the live room a survey is hosting after create_room', async () => {
    // Unique surveyId so no other test's create_room can clobber this survey's mapping.
    const { roomCode } = await hostARoom(777);
    const res = await fetchRoom(777);
    const data = await res.json();
    expect(data.exists).toBe(true);
    expect(data.roomCode).toBe(roomCode);
  });

  it('returns exists:false for a survey with no live room', async () => {
    const res = await fetchRoom(999888);
    const data = await res.json();
    expect(data.exists).toBe(false);
    expect(data.roomCode).toBeUndefined();
  });

  it('points at the newest room when a survey is re-hosted (latest wins)', async () => {
    const first = await hostARoom(778);
    const second = await hostARoom(778);
    expect(second.roomCode).not.toBe(first.roomCode);
    const data = await (await fetchRoom(778)).json();
    expect(data.roomCode).toBe(second.roomCode);
  });

  it('rejects a survey-room lookup with no / wrong internal secret (403, no mapping leaked)', async () => {
    // A caller on the game server's network that is NOT the trusted web-app backend must not be
    // able to enumerate survey→room mappings, even for a survey that IS live.
    await hostARoom(779);
    const noSecret = await fetch(roomStatusUrl(779));
    expect(noSecret.status).toBe(403);
    const wrongSecret = await fetch(roomStatusUrl(779), {
      headers: { 'x-internal-secret': 'not-the-real-secret' },
    });
    expect(wrongSecret.status).toBe(403);
    const body = await wrongSecret.json();
    expect(body.roomCode).toBeUndefined();
    expect(body.exists).toBeUndefined();
  });
});

describe('host-only messages are role-gated against a student', () => {
  it('rejects survey_import from a student', async () => {
    const { roomCode } = await hostARoom();
    const student = await joinAsStudent(roomCode);
    student.send({ type: 'survey_import', payload: {} });
    const msg = await student.next((m) => m.type === 'error' || m.type === 'survey_import_ack');
    expect(msg.type).toBe('error');
    expect(msg.message).toBe('Not authorized');
  });

  it('rejects config_import from a student', async () => {
    const { roomCode } = await hostARoom();
    const student = await joinAsStudent(roomCode);
    student.send({ type: 'config_import', configName: 'x' });
    const msg = await student.next((m) => m.type === 'config_sync_ack');
    expect(msg.success).toBe(false);
    expect(msg.direction).toBe('import');
  });

  it('rejects config_export from a student', async () => {
    const { roomCode } = await hostARoom();
    const student = await joinAsStudent(roomCode);
    student.send({ type: 'config_export', configName: 'x' });
    const msg = await student.next((m) => m.type === 'config_sync_ack');
    expect(msg.success).toBe(false);
    expect(msg.direction).toBe('export');
  });
});

describe('late-join roster replay (students who join after race_start still spawn cars)', () => {
  // The roster (race_start) is a one-time message; latestState is overwritten by the first
  // state_update ~100ms later. Without caching the roster, a late joiner receives only
  // positions for cars it never created and its spawn path never runs (no cars). These tests
  // prove the relay caches race_start and replays it (personalized) before latestState.
  const roster = [
    { teamName: 'Red', colorIndex: 2 },
    { teamName: 'Blue', colorIndex: 3 },
  ];

  it('replays race_start with the full roster and correct yourCarIndex to a late joiner', async () => {
    const { prof, roomCode } = await hostARoom();
    prof.send({ type: 'race_start', cars: roster });

    // Join AFTER the race started. joinAsStudent resolves on room_joined; the roster replay
    // is emitted synchronously right after, so it is already in the inbox for next().
    const student = await joinAsStudent(roomCode, 'Blue');
    const start = await student.next((m) => m.type === 'race_start');
    expect(start.cars).toHaveLength(2);
    expect(start.cars.map((c) => c.teamName)).toEqual(['Red', 'Blue']);
    expect(start.yourCarIndex).toBe(1); // Blue is index 1
  });

  it('sends the roster BEFORE the latest position frame (spawn-then-position ordering)', async () => {
    const { prof, roomCode } = await hostARoom();
    prof.send({ type: 'race_start', cars: roster });
    // A position frame overwrites latestState — the exact condition that used to drop the roster.
    prof.send({ type: 'state_update', t: 1.0, cars: [{ i: 0, px: 5, py: 0, pz: 5, ry: 90, l: 0, c: 1 }] });

    // Drive the join manually so we can capture arrival ORDER from a single collect window.
    const s = makeClient();
    await s.ready();
    const window = s.collect(400);
    s.send({ type: 'join_room', roomCode, teamName: 'Red' });
    const got = await window;

    const startIdx = got.findIndex((m) => m.type === 'race_start');
    const stateIdx = got.findIndex((m) => m.type === 'state_update');
    expect(startIdx).toBeGreaterThanOrEqual(0); // roster replayed at all
    expect(stateIdx).toBeGreaterThanOrEqual(0); // positions also replayed
    expect(startIdx).toBeLessThan(stateIdx);    // roster first, so cars spawn before snapping
  });

  it('replays the full roster with yourCarIndex -1 for an unknown/anonymous team', async () => {
    const { prof, roomCode } = await hostARoom();
    prof.send({ type: 'race_start', cars: roster });

    const unknown = await joinAsStudent(roomCode, 'Zebra');
    const s1 = await unknown.next((m) => m.type === 'race_start');
    expect(s1.cars).toHaveLength(2);
    expect(s1.yourCarIndex).toBe(-1);

    const anon = await joinAsStudent(roomCode, '');
    const s2 = await anon.next((m) => m.type === 'race_start');
    expect(s2.cars).toHaveLength(2);
    expect(s2.yourCarIndex).toBe(-1);
  });

  it('does NOT duplicate race_start for a student who joined before the race started', async () => {
    const { prof, roomCode } = await hostARoom();
    // Join BEFORE race_start — the pre-race path must not replay a cached roster (there is none).
    const student = await joinAsStudent(roomCode, 'Red');

    const window = student.collect(400);
    prof.send({ type: 'race_start', cars: roster });
    const got = await window;

    // Exactly one race_start (the live personalized broadcast), not a live + a replayed copy.
    const starts = got.filter((m) => m.type === 'race_start');
    expect(starts).toHaveLength(1);
    expect(starts[0].yourCarIndex).toBe(0); // Red is index 0
  });

  it('replays the roster to a late-joining web (2D) viewer with yourCarIndex -1', async () => {
    const { prof, roomCode } = await hostARoom();
    prof.send({ type: 'race_start', cars: roster });

    const webapp = makeClient();
    await webapp.ready();
    webapp.send({ type: 'web_join_room', roomCode });
    await webapp.next((m) => m.type === 'room_joined');
    const start = await webapp.next((m) => m.type === 'race_start');
    expect(start.cars).toHaveLength(2);
    expect(start.yourCarIndex).toBe(-1);
  });
});
