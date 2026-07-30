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
