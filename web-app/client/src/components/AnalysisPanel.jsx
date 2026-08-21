import { useState, useEffect } from 'react';
import { getSurveyAnalysis } from '../api.js';

// Format a numeric stat for display: round to 2 decimals, drop trailing zeros,
// and render an em-dash for undefined values (e.g. std of a single response).
function fmt(n) {
  if (n === null || n === undefined || Number.isNaN(n)) return '—';
  return Number.isInteger(n) ? String(n) : Number(n.toFixed(2)).toString();
}

const STAT_COLUMNS = [
  ['mean', 'Mean'],
  ['std', 'Std'],
  ['median', 'Median'],
  ['q1', 'Q1'],
  ['q3', 'Q3'],
  ['min', 'Min'],
  ['max', 'Max'],
];

function NumericQuestion({ q }) {
  if (!q.stats) {
    return <p className="analysis-empty">No numeric answers yet.</p>;
  }
  return (
    <table className="analysis-stats-table">
      <thead>
        <tr>{STAT_COLUMNS.map(([, label]) => <th key={label}>{label}</th>)}</tr>
      </thead>
      <tbody>
        <tr>{STAT_COLUMNS.map(([key]) => <td key={key}>{fmt(q.stats[key])}</td>)}</tr>
      </tbody>
    </table>
  );
}

function ChoiceQuestion({ q }) {
  const rows = [...q.counts, ...q.other.map(o => ({ option: `${o.value} (other)`, count: o.count }))];
  const max = rows.reduce((m, r) => Math.max(m, r.count), 0);
  if (rows.length === 0) {
    return <p className="analysis-empty">No choices recorded yet.</p>;
  }
  return (
    <table className="analysis-choice-table">
      <tbody>
        {rows.map(r => (
          <tr key={r.option}>
            <td className="analysis-choice-label">{r.option}</td>
            <td className="analysis-choice-bar-cell">
              <span
                className="analysis-choice-bar"
                style={{ width: max > 0 ? `${(r.count / max) * 100}%` : '0%' }}
              />
            </td>
            <td className="analysis-choice-count">{r.count}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export default function AnalysisPanel({ surveyId }) {
  const [analysis, setAnalysis] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  async function loadAnalysis() {
    setLoading(true);
    setError('');
    const result = await getSurveyAnalysis(surveyId);
    if (result.success) setAnalysis(result.data);
    else setError(result.error || 'Failed to load analysis');
    setLoading(false);
  }

  // Load once when the panel mounts; the professor refreshes manually thereafter.
  useEffect(() => {
    loadAnalysis();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [surveyId]);

  return (
    <section className="analysis-panel">
      <div className="analysis-header">
        <h3>Survey Analysis</h3>
        <button className="btn-secondary btn-small" onClick={loadAnalysis} disabled={loading}>
          {loading ? 'Refreshing…' : 'Refresh Analysis'}
        </button>
      </div>

      {error && <p className="error">{error}</p>}

      {analysis && (
        <>
          <p className="tab-summary">Based on {analysis.responseCount} response(s)</p>
          {analysis.questions.map(q => (
            <div key={q.id} className="analysis-question">
              <div className="analysis-question-head">
                <span className="analysis-question-text">{q.text}</span>
                <span className="analysis-question-meta">{q.answered} answered</span>
              </div>
              {q.kind === 'numeric' && <NumericQuestion q={q} />}
              {q.kind === 'choice' && <ChoiceQuestion q={q} />}
              {q.kind === 'text' && (
                <p className="analysis-empty">Free-text question — no aggregate statistics.</p>
              )}
            </div>
          ))}
        </>
      )}
    </section>
  );
}
