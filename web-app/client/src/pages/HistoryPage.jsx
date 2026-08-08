import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { getSessionHistory } from '../api.js';
import { buildResultsCsv, downloadBlob, downloadJsonFile } from '../utils/csvExport.js';
import ResultsTable from '../components/ResultsTable.jsx';
import EventLogTable from '../components/EventLogTable.jsx';

export default function HistoryPage() {
  const navigate = useNavigate();
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedId, setExpandedId] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => { loadSessions(); }, []);

  async function loadSessions() {
    setLoading(true);
    setError(null);
    const result = await getSessionHistory();
    if (result.success) {
      setSessions(result.data);
    } else {
      setError(result.error || 'Failed to load session history');
    }
    setLoading(false);
  }

  function formatDuration(seconds) {
    if (!seconds) return '--';
    const m = Math.floor(seconds / 60);
    const s = Math.floor(seconds % 60);
    return `${m}m ${s}s`;
  }

  function downloadCsv(session) {
    const csv = buildResultsCsv(session);
    if (!csv) return;
    downloadBlob(csv, `session-${session.roomCode}-${session.id}.csv`, 'text/csv;charset=utf-8');
  }

  function downloadJson(session) {
    downloadJsonFile(session, `session-${session.roomCode}-${session.id}.json`);
  }

  return (
    <div className="dashboard-page">
      <header className="app-header">
        <h1>Session History</h1>
        <button onClick={() => navigate('/dashboard')} className="btn-secondary">← Back to Dashboard</button>
      </header>

      {loading ? (
        <p className="loading">Loading sessions...</p>
      ) : error ? (
        <p className="error">{error}</p>
      ) : sessions.length === 0 ? (
        <p className="empty">No game sessions recorded yet. Sessions are archived automatically when a game room closes.</p>
      ) : (
        <div className="results-tab">
          {sessions.map(session => (
            <div key={session.id} className="result-session">
              <div className="result-session-header"
                   onClick={() => setExpandedId(expandedId === session.id ? null : session.id)}>
                <span className="result-session-title">
                  {session.configName || 'Game Session'} — Room {session.roomCode}
                </span>
                <span className="result-session-meta">
                  {session.studentCount} participant(s)
                  {session.raceStarted ? ` | ${formatDuration(session.totalRaceTime)}` : ' | No race'}
                  {` | ${session.gamePhase}`}
                  {` | ${new Date(session.endedAt).toLocaleString()}`}
                </span>
                <span className="expand-icon">{expandedId === session.id ? '▼' : '▶'}</span>
              </div>

              {expandedId === session.id && (
                <div className="result-session-body">
                  {session.studentNames && session.studentNames.length > 0 && (
                    <p><strong>Participants:</strong> {session.studentNames.join(', ')}</p>
                  )}

                  {session.surveyId && (
                    <p>
                      <strong>Survey:</strong>{' '}
                      <a href={`#/surveys/${session.surveyId}`}>{session.configName || `#${session.surveyId}`}</a>
                    </p>
                  )}

                  {(!session.rankings || session.rankings.length === 0) ? (
                    <p className="empty">No race completed in this session.</p>
                  ) : (
                    <>
                      <ResultsTable rankings={session.rankings} />

                      <EventLogTable eventLog={session.eventLog} />

                      <div className="result-actions">
                        <button className="btn-primary btn-small" onClick={() => downloadCsv(session)}>
                          Download CSV
                        </button>
                        <button className="btn-secondary btn-small" onClick={() => downloadJson(session)}>
                          Download JSON
                        </button>
                      </div>
                    </>
                  )}
                </div>
              )}
            </div>
          ))}

          <button className="btn-secondary" onClick={loadSessions} style={{ marginTop: '12px' }}>
            Refresh
          </button>
        </div>
      )}
    </div>
  );
}
