import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { getSessionHistory } from '../api.js';

export default function HistoryPage() {
  const navigate = useNavigate();
  const [sessions, setSessions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedId, setExpandedId] = useState(null);

  useEffect(() => { loadSessions(); }, []);

  async function loadSessions() {
    setLoading(true);
    const result = await getSessionHistory();
    if (result.success) setSessions(result.data);
    setLoading(false);
  }

  function formatDuration(seconds) {
    if (!seconds) return '--';
    const m = Math.floor(seconds / 60);
    const s = Math.floor(seconds % 60);
    return `${m}m ${s}s`;
  }

  function downloadCsv(session) {
    const rankings = session.rankings || [];
    if (rankings.length === 0) return;

    const allKeys = [];
    for (const car of rankings) {
      for (const attr of (car.Attributes || [])) {
        if (attr.Key && !allKeys.includes(attr.Key)) allKeys.push(attr.Key);
      }
    }

    let csv = 'Rank,TeamName';
    for (const key of allKeys) csv += `,${escapeCsv(key)}`;
    csv += ',LapsCompleted,CheckpointsPassed,Time\n';

    for (const car of rankings) {
      csv += `${car.Rank},${escapeCsv(car.TeamName)}`;
      for (const key of allKeys) {
        const attr = (car.Attributes || []).find(a => a.Key === key);
        csv += `,${escapeCsv(attr ? attr.Value : '')}`;
      }
      csv += `,${car.LapsCompleted},${car.CheckpointsPassed},${(car.TotalTime || 0).toFixed(2)}\n`;
    }

    downloadBlob(csv, `session-${session.roomCode}-${session.id}.csv`, 'text/csv;charset=utf-8');
  }

  function downloadJson(session) {
    downloadBlob(JSON.stringify(session, null, 2), `session-${session.roomCode}-${session.id}.json`, 'application/json');
  }

  return (
    <div className="dashboard-page">
      <header className="app-header">
        <h1>Session History</h1>
        <button onClick={() => navigate('/dashboard')} className="btn-secondary">← Dashboard</button>
      </header>

      {loading ? (
        <p className="loading">Loading sessions...</p>
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
                <span className="expand-icon">{expandedId === session.id ? '\u25BC' : '\u25B6'}</span>
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
                      <table className="response-table">
                        <thead>
                          <tr>
                            <th>Rank</th>
                            <th>Team</th>
                            <th>Laps</th>
                            <th>Checkpoints</th>
                            <th>Time</th>
                          </tr>
                        </thead>
                        <tbody>
                          {session.rankings.map((car, i) => (
                            <tr key={i} className="response-row">
                              <td className={car.Rank <= 3 ? `rank-${car.Rank}` : ''}>{car.Rank}</td>
                              <td>{car.TeamName}</td>
                              <td>{car.LapsCompleted}</td>
                              <td>{car.CheckpointsPassed}</td>
                              <td>{(car.TotalTime || 0).toFixed(2)}s</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>

                      {(session.eventLog || []).length > 0 && (
                        <div className="event-log">
                          <h4>Event Log</h4>
                          <table className="response-table">
                            <thead>
                              <tr><th>Time</th><th>Event</th><th>Affected</th></tr>
                            </thead>
                            <tbody>
                              {session.eventLog.map((e, i) => (
                                <tr key={i} className="response-row">
                                  <td>{(e.Timestamp || 0).toFixed(1)}s</td>
                                  <td>{e.EventName}</td>
                                  <td>{e.AffectedCount}/{e.TotalCars}</td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      )}

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

function escapeCsv(value) {
  if (!value) return '';
  const str = String(value);
  if (str.includes(',') || str.includes('"')) return '"' + str.replace(/"/g, '""') + '"';
  return str;
}

function downloadBlob(content, filename, mimeType) {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
