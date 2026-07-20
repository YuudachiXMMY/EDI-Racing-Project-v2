import { useState, useEffect } from 'react';
import { getRaceResults } from '../api.js';

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

    downloadBlob(csv, `race-results-${session.id}.csv`, 'text/csv;charset=utf-8');
  }

  function downloadJson(session) {
    downloadBlob(JSON.stringify(session, null, 2), `race-results-${session.id}.json`, 'application/json');
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
            <span className="expand-icon">{expandedId === session.id ? '\u25BC' : '\u25B6'}</span>
          </div>

          {expandedId === session.id && (
            <div className="result-session-body">
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
                  {(session.rankings || []).map((car, i) => (
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
                      <tr>
                        <th>Time</th>
                        <th>Event</th>
                        <th>Affected</th>
                      </tr>
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
