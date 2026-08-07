import { useState, useEffect } from 'react';
import { getRaceResults } from '../api.js';
import { buildResultsCsv, downloadBlob, downloadJsonFile } from '../utils/csvExport.js';
import ResultsTable from './ResultsTable.jsx';
import EventLogTable from './EventLogTable.jsx';

export default function ResultsTab({ surveyId }) {
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedId, setExpandedId] = useState(null);

  useEffect(() => {
    loadResults();
  }, [surveyId]);

  async function loadResults() {
    setLoading(true);
    const result = await getRaceResults(surveyId);
    if (result.success) setSessions(result.data);
    setLoading(false);
  }

  function downloadCsv(session) {
    const csv = buildResultsCsv(session);
    if (!csv) return;
    downloadBlob(csv, `race-results-${session.id}.csv`, 'text/csv;charset=utf-8');
  }

  function downloadJson(session) {
    downloadJsonFile(session, `race-results-${session.id}.json`);
  }

  if (loading) return <p className="loading">Loading results...</p>;

  if (sessions.length === 0) {
    return <p className="empty">No race results yet. Run a race in Unity with this survey to see results here.</p>;
  }

  return (
    <div className="results-tab">
      {sessions.map(session => (
        <div key={session.id} className="result-session">
          <div className="result-session-header"
               onClick={() => setExpandedId(expandedId === session.id ? null : session.id)}>
            <span className="result-session-title">
              {session.configName || 'Race Session'} — {new Date(session.receivedAt).toLocaleString()}
            </span>
            <span className="result-session-meta">
              {(session.rankings || []).length} car(s) | {(session.totalRaceTime || 0).toFixed(1)}s
              {session.roomCode && ` | Room ${session.roomCode}`}
            </span>
            <span className="expand-icon">{expandedId === session.id ? '▼' : '▶'}</span>
          </div>

          {expandedId === session.id && (
            <div className="result-session-body">
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
            </div>
          )}
        </div>
      ))}

      <button className="btn-secondary" onClick={loadResults} style={{ marginTop: '12px' }}>
        Refresh
      </button>
    </div>
  );
}
