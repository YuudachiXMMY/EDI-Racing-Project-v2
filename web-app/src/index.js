import express from 'express';
import cors from 'cors';
import { getDb, closeDb } from './db.js';
import authRoutes from './routes/auth.js';
import surveyRoutes from './routes/surveys.js';
import exportRoutes from './routes/export.js';

const PORT = parseInt(process.env.API_PORT || '3001', 10);

const app = express();
app.use(cors());
app.use(express.json({ limit: '1mb' }));

// Initialize database on startup
getDb();

// Routes
app.use('/api/auth', authRoutes);
app.use('/api/surveys', surveyRoutes);
app.use('/api/surveys', exportRoutes);

// Health check
app.get('/api/health', (req, res) => {
  res.json({ success: true, data: { status: 'ok' } });
});

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
