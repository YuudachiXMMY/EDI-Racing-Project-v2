// Shared CSV/JSON export helpers for race-results tables (ResultsTab, HistoryPage).

/**
 * Quote a CSV field when it contains a comma, double-quote, or newline/carriage-return.
 * Embedded double-quotes are doubled per RFC 4180. Fields with `\n`/`\r` MUST be quoted,
 * otherwise the newline breaks row boundaries and corrupts the file.
 */
export function escapeCsv(value) {
  if (!value) return '';
  const str = String(value);
  if (str.includes(',') || str.includes('"') || str.includes('\n') || str.includes('\r'))
    return '"' + str.replace(/"/g, '""') + '"';
  return str;
}

/** Trigger a browser download of `content` as `filename` with the given MIME type. */
export function downloadBlob(content, filename, mimeType) {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

/** Trigger a browser download of an already-constructed Blob. */
export function downloadBlobObject(blob, filename) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

/**
 * Build a CSV string from a race session's `rankings`. Columns: Rank, TeamName, one per
 * distinct car attribute key, then LapsCompleted, CheckpointsPassed, Time.
 * Returns '' when there are no rankings (caller should skip the download).
 */
export function buildResultsCsv(session) {
  const rankings = session.rankings || [];
  if (rankings.length === 0) return '';

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

  return csv;
}

/** Download a session object as pretty-printed JSON under `filename`. */
export function downloadJsonFile(session, filename) {
  downloadBlob(JSON.stringify(session, null, 2), filename, 'application/json');
}

/** Round a stat to at most 4 decimals, dropping trailing zeros; '' for null/undefined. */
function fmtNum(n) {
  if (n === null || n === undefined || Number.isNaN(n)) return '';
  return Number.isInteger(n) ? String(n) : String(Number(n.toFixed(4)));
}

/**
 * Build a tidy (long-format) CSV from a survey-analysis object as returned by
 * GET /api/surveys/:id/analysis. One row per metric so numeric and choice
 * questions share the same columns:
 *   Question, Type, Answered, Metric, Value
 * Numeric questions emit Mean/Std/Median/Q1/Q3/Min/Max rows; choice questions
 * emit one row per option (declared options plus any "(other)" values); free-text
 * questions emit a single placeholder row. Returns '' when there are no questions.
 */
export function buildAnalysisCsv(analysis) {
  const questions = analysis?.questions || [];
  if (questions.length === 0) return '';

  let csv = 'Question,Type,Answered,Metric,Value\n';
  const row = (q, type, metric, value) =>
    `${escapeCsv(q.text)},${type},${q.answered},${escapeCsv(metric)},${escapeCsv(value)}\n`;

  for (const q of questions) {
    if (q.kind === 'numeric') {
      if (!q.stats) {
        csv += row(q, 'Numeric', '(no numeric answers)', '');
        continue;
      }
      csv += row(q, 'Numeric', 'Mean', fmtNum(q.stats.mean));
      csv += row(q, 'Numeric', 'Std', fmtNum(q.stats.std));
      csv += row(q, 'Numeric', 'Median', fmtNum(q.stats.median));
      csv += row(q, 'Numeric', 'Q1', fmtNum(q.stats.q1));
      csv += row(q, 'Numeric', 'Q3', fmtNum(q.stats.q3));
      csv += row(q, 'Numeric', 'Min', fmtNum(q.stats.min));
      csv += row(q, 'Numeric', 'Max', fmtNum(q.stats.max));
    } else if (q.kind === 'choice') {
      for (const c of q.counts) csv += row(q, 'Choice', c.option, c.count);
      for (const o of (q.other || [])) csv += row(q, 'Choice', `${o.value} (other)`, o.count);
    } else {
      csv += row(q, 'Text', '(free text)', '');
    }
  }

  return csv;
}
