import { describe, it, expect } from 'vitest';
import { computeSurveyAnalysis } from '../src/lib/surveyAnalysis.js';

// Question type constants mirror the Unity enum (see client constants.js).
const TEXT = 0;
const CHOICE = 1;
const NUMERIC = 2;
const MULTISELECT = 3;

const QUESTIONS = [
  { Id: 'member_names', Text: 'Member names', Type: TEXT, Options: [] },
  { Id: 'color', Text: 'Car colour', Type: CHOICE, Options: ['Blue', 'Red', 'Green'] },
  { Id: 'member_count', Text: 'Members', Type: NUMERIC, Options: [] },
  {
    Id: 'car_features',
    Text: 'Rank in-car features',
    Type: MULTISELECT,
    Options: ['Heads-up display', 'Camera Vision', 'Lane-Keep Assist'],
  },
];

function res(answers) {
  return { answers };
}

describe('computeSurveyAnalysis', () => {
  it('returns responseCount and one entry per question', () => {
    const out = computeSurveyAnalysis(QUESTIONS, [res({}), res({})]);
    expect(out.responseCount).toBe(2);
    expect(out.questions).toHaveLength(4);
    expect(out.questions.map(q => q.kind)).toEqual(['text', 'choice', 'numeric', 'choice']);
  });

  it('computes numeric stats matching a known sample', () => {
    // Sample: 2, 4, 4, 4, 5, 5, 7, 9  (the classic textbook set)
    const responses = [2, 4, 4, 4, 5, 5, 7, 9].map(n => res({ member_count: n }));
    const stat = computeSurveyAnalysis(QUESTIONS, responses).questions[2].stats;

    expect(stat.count).toBe(8);
    expect(stat.mean).toBeCloseTo(5, 10);
    // Sample std (ddof=1) of this set is exactly sqrt(32/7) ≈ 2.13809.
    expect(stat.std).toBeCloseTo(Math.sqrt(32 / 7), 10);
    expect(stat.median).toBeCloseTo(4.5, 10);
    expect(stat.min).toBe(2);
    expect(stat.max).toBe(9);
  });

  it('computes quartiles with linear interpolation (NumPy/Excel-inclusive)', () => {
    // 1..10 -> Q1=3.25, median=5.5, Q3=7.75 under linear interpolation.
    const responses = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map(n => res({ member_count: n }));
    const stat = computeSurveyAnalysis(QUESTIONS, responses).questions[2].stats;
    expect(stat.q1).toBeCloseTo(3.25, 10);
    expect(stat.median).toBeCloseTo(5.5, 10);
    expect(stat.q3).toBeCloseTo(7.75, 10);
  });

  it('parses numeric answers stored as strings and ignores non-numeric ones', () => {
    const responses = [res({ member_count: '3' }), res({ member_count: 'abc' }), res({ member_count: 5 })];
    const q = computeSurveyAnalysis(QUESTIONS, responses).questions[2];
    expect(q.answered).toBe(2);
    expect(q.stats.mean).toBeCloseTo(4, 10);
    expect(q.stats.count).toBe(2);
  });

  it('reports std as null for a single numeric response', () => {
    const q = computeSurveyAnalysis(QUESTIONS, [res({ member_count: 7 })]).questions[2];
    expect(q.stats.count).toBe(1);
    expect(q.stats.std).toBeNull();
    expect(q.stats.median).toBe(7);
    expect(q.stats.q1).toBe(7);
    expect(q.stats.q3).toBe(7);
  });

  it('returns null stats when a numeric question has no answers', () => {
    const q = computeSurveyAnalysis(QUESTIONS, [res({}), res({})]).questions[2];
    expect(q.answered).toBe(0);
    expect(q.stats).toBeNull();
  });

  it('counts single-choice options and seeds unpicked options to zero', () => {
    const responses = [
      res({ color: 'Blue' }),
      res({ color: 'Blue' }),
      res({ color: 'Red' }),
    ];
    const q = computeSurveyAnalysis(QUESTIONS, responses).questions[1];
    expect(q.answered).toBe(3);
    expect(q.counts).toEqual([
      { option: 'Blue', count: 2 },
      { option: 'Red', count: 1 },
      { option: 'Green', count: 0 },
    ]);
    expect(q.other).toEqual([]);
  });

  it('counts each pick for multi-select car features', () => {
    const responses = [
      res({ car_features: ['Heads-up display', 'Camera Vision'] }),
      res({ car_features: ['Camera Vision'] }),
      res({ car_features: ['Camera Vision', 'Lane-Keep Assist'] }),
      res({ car_features: [] }),
    ];
    const q = computeSurveyAnalysis(QUESTIONS, responses).questions[3];
    // 3 respondents actually picked something (empty array does not count).
    expect(q.answered).toBe(3);
    expect(q.counts).toEqual([
      { option: 'Heads-up display', count: 1 },
      { option: 'Camera Vision', count: 3 },
      { option: 'Lane-Keep Assist', count: 1 },
    ]);
  });

  it('surfaces choice values outside the declared options under `other`', () => {
    const q = computeSurveyAnalysis(QUESTIONS, [res({ color: 'Purple' })]).questions[1];
    expect(q.counts.find(c => c.option === 'Blue').count).toBe(0);
    expect(q.other).toEqual([{ value: 'Purple', count: 1 }]);
  });

  it('matches answer keys case-insensitively', () => {
    const q = computeSurveyAnalysis(QUESTIONS, [res({ Member_Count: 4 })]).questions[2];
    expect(q.stats.count).toBe(1);
    expect(q.stats.mean).toBe(4);
  });

  it('counts answered free-text responses without producing stats', () => {
    const responses = [res({ member_names: 'Steve, Al' }), res({ member_names: '' }), res({})];
    const q = computeSurveyAnalysis(QUESTIONS, responses).questions[0];
    expect(q.kind).toBe('text');
    expect(q.answered).toBe(1);
    expect(q.stats).toBeUndefined();
  });

  it('handles empty inputs gracefully', () => {
    expect(computeSurveyAnalysis([], [])).toEqual({ responseCount: 0, questions: [] });
    expect(computeSurveyAnalysis(null, null)).toEqual({ responseCount: 0, questions: [] });
  });
});
