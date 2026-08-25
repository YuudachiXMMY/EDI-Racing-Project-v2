import express from 'express';
import cors from 'cors';
import { existsSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';
import { getDb, closeDb } from './db.js';
import authRoutes from './routes/auth.js';
import surveyRoutes from './routes/surveys.js';
import exportRoutes from './routes/export.js';
import templateRoutes from './routes/templates.js';
import responseRoutes from './routes/responses.js';
import gameStatusRoutes from './routes/game-status.js';
import resultsRoutes, { archiveSecretUsable } from './routes/results.js';
import { checkSecretConfig } from './hostToken.js';
import { mailConfigured } from './config.js';

const __dirname = dirname(fileURLToPath(import.meta.url));
const PORT = parseInt(process.env.API_PORT || '3001', 10);

// Boot guard — refuse to start (or warn) if the host-token secret is misconfigured.
// Runs before app.listen so a fatal config never binds the port. Mirrored in Server/server.js.
// gameAccessGate:true — this process serves /api/game/gate, an ALWAYS-on auth boundary nginx
// checks on every /game/ asset, so a default/public secret is never safe here (it would let
// anyone forge a game_access cookie). The guard is therefore fatal on the default regardless
// of REQUIRE_HOST_TOKEN.
const REQUIRE_HOST_TOKEN = (process.env.REQUIRE_HOST_TOKEN || 'false').toLowerCase() === 'true';
const secretCheck = checkSecretConfig({
  secret: process.env.INTERNAL_SECRET,
  requireHostToken: REQUIRE_HOST_TOKEN,
  gameAccessGate: true,
});
if (secretCheck.level === 'fatal') {
  console.error(`[Auth] FATAL: ${secretCheck.message}`);
  process.exit(1);
}
if (secretCheck.level === 'warn') {
  console.warn(`[Auth] WARNING: ${secretCheck.message}`);
}

// Archive endpoint is a separate auth boundary from the host-token gate above: it accepts
// writes against any professor account, so it fails closed unless a strong secret is set —
// independent of REQUIRE_HOST_TOKEN. Warn loudly when it is disabled so the operator knows.
if (!archiveSecretUsable(process.env.INTERNAL_SECRET)) {
  console.warn(
    '[Auth] WARNING: /api/sessions/archive is DISABLED — INTERNAL_SECRET is unset or the ' +
    'public default. Set a strong secret to enable session archiving.'
  );
}

// Mail boot guard — warn (not fatal) when SMTP is unconfigured so password-reset emails are
// silently disabled but the app still boots and serves login for existing users.
if (!mailConfigured()) {
  console.warn('[Mail] WARNING: SMTP is not fully configured — password-reset emails are DISABLED. ' +
    'Set SMTP_HOST/MAIL_FROM (+ SMTP_USER/SMTP_PASS if authenticating).');
}

const app = express();
app.use(cors());
app.use(express.json({ limit: '1mb' }));

// Initialize database on startup
getDb();

// API Routes
app.use('/api/auth', authRoutes);
app.use('/api/surveys', surveyRoutes);
app.use('/api/surveys', exportRoutes);
app.use('/api/templates', templateRoutes);
// Each router is mounted exactly once at /api; sub-paths are baked into the route
// definitions (e.g. '/surveys/:id/responses', '/sessions/archive'). Mounting under two
// prefixes would give every route a second, untested shadow URL — avoid that.
app.use('/api', responseRoutes);
app.use('/api', resultsRoutes);
app.use('/api/game', gameStatusRoutes);

// Health check
app.get('/api/health', (req, res) => {
  res.json({ success: true, data: { status: 'ok' } });
});

// Serve the client build at the site root (Vite base: '/'). The Unity game is served
// separately by nginx at /game/, so the SPA owns everything else. HashRouter means the
// server only ever sees '/', but the '*' fallback also covers any non-hash deep path;
// it skips /api so unmatched API routes still 404 as JSON instead of returning HTML.
const clientDist = join(__dirname, '..', 'client', 'dist');
if (existsSync(clientDist)) {
  app.use(express.static(clientDist));
  app.get('*', (req, res, next) => {
    if (req.path.startsWith('/api/')) return next();
    res.sendFile(join(clientDist, 'index.html'));
  });
}

// Global error handler
app.use((err, req, res, _next) => {
  console.error('[API] Error:', err.message);
  res.status(500).json({ success: false, error: 'Internal server error' });
});

app.listen(PORT, () => {
  console.log(`[API] EDI Survey Web App listening on port ${PORT}`);
});

process.on('SIGTERM', () => {
  closeDb();
  process.exit(0);
});
