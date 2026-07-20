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
import resultsRoutes from './routes/results.js';

const __dirname = dirname(fileURLToPath(import.meta.url));
const PORT = parseInt(process.env.API_PORT || '3001', 10);

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
app.use('/api', responseRoutes);
app.use('/api/surveys', responseRoutes);
app.use('/api/surveys', resultsRoutes);
app.use('/api/game', gameStatusRoutes);

// Health check
app.get('/api/health', (req, res) => {
  res.json({ success: true, data: { status: 'ok' } });
});

// Serve client build in production (base path matches Vite config base: '/survey/')
const clientDist = join(__dirname, '..', 'client', 'dist');
if (existsSync(clientDist)) {
  app.use('/survey', express.static(clientDist));
  app.get('/', (req, res) => res.redirect('/survey/'));
  app.get('/survey/*', (req, res) => {
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
