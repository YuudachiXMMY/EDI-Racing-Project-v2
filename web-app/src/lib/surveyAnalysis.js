/**
 * Survey response analysis.
 *
 * Pure, dependency-free descriptive statistics over a survey's collected
 * responses. Kept side-effect free (no DB, no I/O) so it is deterministic and
 * unit-testable in isolation — the route layer supplies parsed questions and
 * answers.
 *
 * Question types mirror the Unity `QuestionType` enum (see client constants.js):
 *   0 = Text          -> response count only (no numeric/choice summary)
 *   1 = MultipleChoice-> option counts (single pick per response)
 *   2 = Numeric       -> mean/std/median/Q1/Q3/min/max
 *   3 = MultiSelect   -> option counts (e.g. "car features", up to N picks)
 */

const QuestionType = { Text: 0, MultipleChoice: 1, Numeric: 2, MultiSelect: 3 };

/**
 * Linear-interpolation quantile over an ascending-sorted numeric array.
 * Matches NumPy's default ('linear') / Excel PERCENTILE.INC behaviour so the
 * reported Q1/median/Q3 line up with what a professor would get in a spreadsheet.
 *
 * @param {number[]} sorted - ascending, length >= 1
 * @param {number} q - quantile in [0, 1]
 * @returns {number}
 */
function quantile(sorted, q) {
  const n = sorted.length;
  if (n === 1) return sorted[0];
  const pos = q * (n - 1);
  const lower = Math.floor(pos);
  const frac = pos - lower;
  if (lower + 1 >= n) return sorted[lower];
  return sorted[lower] + frac * (sorted[lower + 1] - sorted[lower]);
}

/**
 * Descriptive statistics for a numeric sample.
 * Standard deviation is the sample std (dividing by n-1, ddof=1) to match
 * Excel STDEV / pandas .std() defaults; it is null for n < 2 where it is undefined.
 *
 * @param {number[]} values
 * @returns {{count, mean, std, median, q1, q3, min, max} | null} null if no values
 */
function numericStats(values) {
  const n = values.length;
  if (n === 0) return null;

  const sorted = [...values].sort((a, b) => a - b);
  const sum = sorted.reduce((a, b) => a + b, 0);
  const mean = sum / n;

  let std = null;
  if (n >= 2) {
    const variance = sorted.reduce((acc, v) => acc + (v - mean) ** 2, 0) / (n - 1);
    std = Math.sqrt(variance);
  }

  return {
    count: n,
    mean,
    std,
    median: quantile(sorted, 0.5),
    q1: quantile(sorted, 0.25),
    q3: quantile(sorted, 0.75),
    min: sorted[0],
    max: sorted[n - 1],
  };
}

/**
 * Read a question's answer out of a response's answers object, case-insensitively
 * on the question id (responses may key by id with differing case — mirrors the
 * lookup export.js performs).
 */
function readAnswer(answers, questionId) {
  if (!answers || !questionId) return undefined;
  if (Object.prototype.hasOwnProperty.call(answers, questionId)) return answers[questionId];
  const target = String(questionId).toLowerCase();
  for (const key of Object.keys(answers)) {
    if (key.toLowerCase() === target) return answers[key];
  }
  return undefined;
}

/**
 * Tally option counts for a choice question. Handles both single answers
 * (MultipleChoice) and array answers (MultiSelect / car features). Every option
 * declared on the question is seeded to 0 so unpicked options still appear; any
 * answer value not in the declared options is surfaced under `other`.
 *
 * @returns {{ counts: Array<{option, count}>, respondents: number, other: Array<{value, count}> }}
 */
function choiceStats(question, answersList) {
  const declared = Array.isArray(question.Options) ? question.Options : [];
  const counts = new Map(declared.map(opt => [opt, 0]));
  const other = new Map();
  let respondents = 0;

  for (const raw of answersList) {
    if (raw === undefined || raw === null || raw === '') continue;
    const picks = Array.isArray(raw) ? raw : [raw];
    const cleaned = picks
      .map(p => (typeof p === 'string' ? p : String(p)))
      .filter(p => p !== '');
    if (cleaned.length === 0) continue;
    respondents++;
    for (const pick of cleaned) {
      if (counts.has(pick)) {
        counts.set(pick, counts.get(pick) + 1);
      } else {
        other.set(pick, (other.get(pick) || 0) + 1);
      }
    }
  }

  return {
    respondents,
    counts: [...counts.entries()].map(([option, count]) => ({ option, count })),
    other: [...other.entries()].map(([value, count]) => ({ value, count })),
  };
}

/**
 * Compute per-question analysis for a survey.
 *
 * @param {Array} questions - parsed questions_json (each: { Id, Text, Type, Options, ... })
 * @param {Array<{answers: Object}>} responses - parsed responses, each with an `answers` object
 * @returns {{ responseCount: number, questions: Array<Object> }}
 */
export function computeSurveyAnalysis(questions, responses) {
  const safeQuestions = Array.isArray(questions) ? questions : [];
  const safeResponses = Array.isArray(responses) ? responses : [];

  const analyzed = safeQuestions.map(q => {
    const answersList = safeResponses.map(r => readAnswer(r.answers, q.Id));
    const base = { id: q.Id, text: q.Text, type: q.Type };

    if (q.Type === QuestionType.Numeric) {
      const numeric = answersList
        .map(v => (typeof v === 'number' ? v : parseFloat(v)))
        .filter(v => typeof v === 'number' && !Number.isNaN(v));
      return { ...base, kind: 'numeric', answered: numeric.length, stats: numericStats(numeric) };
    }

    if (q.Type === QuestionType.MultipleChoice || q.Type === QuestionType.MultiSelect) {
      const { respondents, counts, other } = choiceStats(q, answersList);
      return { ...base, kind: 'choice', answered: respondents, counts, other };
    }

    // Text (or unknown) — no meaningful aggregate beyond how many answered.
    const answered = answersList.filter(v => v !== undefined && v !== null && String(v).trim() !== '').length;
    return { ...base, kind: 'text', answered };
  });

  return { responseCount: safeResponses.length, questions: analyzed };
}

/**
 * Quote a CSV field when it contains a comma, double-quote, or newline. Embedded
 * double-quotes are doubled per RFC 4180. Mirrors the client escapeCsv helper.
 */
function escapeCsv(value) {
  if (value === null || value === undefined) return '';
  const str = String(value);
  if (str.includes(',') || str.includes('"') || str.includes('\n') || str.includes('\r'))
    return '"' + str.replace(/"/g, '""') + '"';
  return str;
}

/** Round a stat to at most 4 decimals, dropping trailing zeros; '' for null/undefined. */
function fmtNum(n) {
  if (n === null || n === undefined || Number.isNaN(n)) return '';
  return Number.isInteger(n) ? String(n) : String(Number(n.toFixed(4)));
}

/**
 * Build a tidy (long-format) CSV from a survey-analysis object as returned by
 * computeSurveyAnalysis. One row per metric so numeric and choice questions share
 * the same columns: Question, Type, Answered, Metric, Value. Numeric questions
 * emit Mean/Std/Median/Q1/Q3/Min/Max rows; choice questions emit one row per
 * option (declared options plus any "(other)" values); free-text questions emit a
 * single placeholder row. Returns '' when there are no questions.
 *
 * Kept in lockstep with the client buildAnalysisCsv (utils/csvExport.js) so the
 * bundled analysis file matches what the standalone client export produced.
 *
 * @param {{ questions: Array<Object> }} analysis
 * @returns {string}
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
