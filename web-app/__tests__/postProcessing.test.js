import { describe, it, expect } from 'vitest';
import { applyPostProcessing } from '../src/routes/export.js';

// --- Fixtures -------------------------------------------------------------

// Build a car from a plain attribute map for concise fixtures.
function car(teamName, attrs) {
  return {
    teamName,
    attributes: Object.entries(attrs).map(([key, value]) => ({ key, value: String(value) })),
  };
}

// Extract the slash-separated function tags a car ended up with.
function tags(c) {
  const f = c.attributes.find(a => a.key === 'functions');
  return f ? f.value.split('/').filter(Boolean) : [];
}

const gt = (src, tag) => ({
  type: 'average_threshold', sourceAttribute: src, direction: 'gt', tagName: tag, targetAttribute: 'functions',
});
const maleRule = {
  type: 'difference_threshold', sourceMinuend: 'member_count', sourceSubtrahend: 'male_count',
  threshold: 2, direction: 'lt', tagName: 'male', targetAttribute: 'functions',
};

// --- Strict greater-than-average ------------------------------------------

describe('applyPostProcessing — strict greater-than average', () => {
  it('test_average_gt_only_tags_strictly_above_mean', () => {
    // Arrange: facial counts [3, 4, 5] → average 4
    const cars = [car('A', { facial_count: 3 }), car('B', { facial_count: 4 }), car('C', { facial_count: 5 })];
    // Act
    const out = applyPostProcessing(cars, [gt('facial_count', 'facerecog')]);
    // Assert: only C (5 > 4) is tagged; B (4, equal to mean) is excluded — proves '>' not '>='
    expect(tags(out[0])).toEqual([]);
    expect(tags(out[1])).toEqual([]);
    expect(tags(out[2])).toEqual(['facerecog']);
  });

  it('test_average_gt_single_response_tags_nobody', () => {
    // Arrange: a single team → average equals its own value
    const cars = [car('A', { facial_count: 5 })];
    // Act
    const out = applyPostProcessing(cars, [gt('facial_count', 'facerecog')]);
    // Assert: 5 > 5 is false → no tag
    expect(tags(out[0])).toEqual([]);
  });

  it('test_password_uses_gt_not_lte', () => {
    // Arrange: pwd_count [2, 4, 6] → average 4 (old logic used '<= average')
    const cars = [car('A', { pwd_count: 2 }), car('B', { pwd_count: 4 }), car('C', { pwd_count: 6 })];
    // Act
    const out = applyPostProcessing(cars, [gt('pwd_count', 'password')]);
    // Assert: only C (6 > 4) tagged; old '<=' would have tagged A and B
    expect(tags(out[0])).toEqual([]);
    expect(tags(out[1])).toEqual([]);
    expect(tags(out[2])).toEqual(['password']);
  });
});

// --- Male difference_threshold --------------------------------------------

describe('applyPostProcessing — male difference_threshold', () => {
  it('test_male_tagged_when_nonmale_below_two', () => {
    // Arrange: (member, male) → non-male = member - male
    //   (5,4)→1 <2 male | (5,3)→2 not <2 | (3,3)→0 <2 male
    const cars = [
      car('A', { member_count: 5, male_count: 4 }),
      car('B', { member_count: 5, male_count: 3 }),
      car('C', { member_count: 3, male_count: 3 }),
    ];
    // Act
    const out = applyPostProcessing(cars, [maleRule]);
    // Assert
    expect(tags(out[0])).toEqual(['male']);
    expect(tags(out[1])).toEqual([]);
    expect(tags(out[2])).toEqual(['male']);
  });
});

// --- Combination + edge cases ---------------------------------------------

describe('applyPostProcessing — combined tags and edges', () => {
  it('test_multiple_matching_rules_join_with_slash', () => {
    // Arrange: facial [9,1] → avg 5; A also has non-male 1 (<2)
    const cars = [
      car('A', { facial_count: 9, member_count: 5, male_count: 4 }),
      car('B', { facial_count: 1, member_count: 5, male_count: 0 }),
    ];
    // Act
    const out = applyPostProcessing(cars, [gt('facial_count', 'facerecog'), maleRule]);
    // Assert: A gets both (9>5, non-male 1<2); B gets neither (1<5, non-male 5 not <2)
    expect(tags(out[0])).toEqual(['facerecog', 'male']);
    expect(tags(out[1])).toEqual([]);
  });

  it('test_empty_postprocessing_returns_unchanged', () => {
    // Arrange
    const cars = [car('A', { facial_count: 5 })];
    // Act
    const out = applyPostProcessing(cars, []);
    // Assert
    expect(out).toEqual(cars);
  });
});
