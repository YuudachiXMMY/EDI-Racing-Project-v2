import { useState, useEffect } from 'react';
import { getResponses } from '../api.js';

export default function ResponsesTab({ surveyId }) {
  const [responses, setResponses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedId, setExpandedId] = useState(null);

  useEffect(() => {
    loadResponses();
  }, [surveyId]);

  async function loadResponses() {
    setLoading(true);
    const result = await getResponses(surveyId);
    if (result.success) setResponses(result.data);
    setLoading(false);
  }

  if (loading) return <p className="loading">Loading responses...</p>;

  if (responses.length === 0) {
    return <p className="empty">No responses yet. Share the survey link with students to start collecting data.</p>;
  }

  return (
    <div className="responses-tab">
      <p className="tab-summary">{responses.length} response(s)</p>
      <table className="response-table">
        <thead>
          <tr>
            <th>Email</th>
            <th>Team Name</th>
            <th>Submitted</th>
            <th>Answers</th>
          </tr>
        </thead>
        <tbody>
          {responses.map(r => (
            <tr key={r.id} className="response-row">
              <td>{r.email}</td>
              <td>{r.team_name}</td>
              <td>{new Date(r.submitted_at).toLocaleString()}</td>
              <td>
                <button
                  className="btn-secondary btn-small"
                  onClick={() => setExpandedId(expandedId === r.id ? null : r.id)}
                >
                  {expandedId === r.id ? '隐藏' : '查看'}
                </button>
                {expandedId === r.id && (
                  <div className="answers-detail">
                    {Object.entries(r.answers || {}).map(([key, val]) => (
                      <div key={key} className="answer-row">
                        <span className="answer-key">{key}:</span>
                        <span className="answer-value">{String(val)}</span>
                      </div>
                    ))}
                  </div>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
