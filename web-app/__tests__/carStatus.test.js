import { describe, it, expect } from 'vitest';
import {
  parseCarAttrs,
  resolveSelectedCar,
  deriveSpeed,
  buildTransform,
  normalize,
  fitEllipse,
} from '../client/src/lib/carStatus.js';

// --- Fixtures -------------------------------------------------------------

// A roster car as it arrives on the wire: teamName + attrs array of {k,v}.
function rosterCar(teamName, colorIndex, functions) {
  const attrs = [];
  if (colorIndex !== undefined) attrs.push({ k: 'colorIndex', v: String(colorIndex) });
  if (functions !== undefined) attrs.push({ k: 'functions', v: functions });
  return { teamName, attrs };
}

// --- parseCarAttrs --------------------------------------------------------

describe('carStatus — parseCarAttrs', () => {
  it('test_parse_attrs_reads_colorIndex_and_functions', () => {
    // Arrange: the exact shape from the wire (attrs is an ARRAY of {k,v}, not an object)
    const car = rosterCar('Red', 2, 'facerecog/password');
    // Act
    const out = parseCarAttrs(car);
    // Assert: colorIndex parsed to int; functions split on '/', trimmed, lowercased
    expect(out).toEqual({ colorIndex: 2, functions: ['facerecog', 'password'] });
  });

  it('test_parse_attrs_missing_attrs_defaults_to_green_and_empty', () => {
    // Arrange: a survey config that omits colorIndex and functions
    const car = { teamName: 'Blank', attrs: [] };
    // Act
    const out = parseCarAttrs(car);
    // Assert: default color index 0 (green) and no functions — no crash
    expect(out).toEqual({ colorIndex: 0, functions: [] });
  });

  it('test_parse_attrs_null_car_defaults', () => {
    // Arrange/Act: a position with no matching roster car
    const out = parseCarAttrs(undefined);
    // Assert: graceful defaults
    expect(out).toEqual({ colorIndex: 0, functions: [] });
  });

  it('test_parse_attrs_normalizes_case_and_whitespace', () => {
    // Arrange: mixed case + spaces around the slash-separated tags
    const car = rosterCar('Msg', 4, ' Glasses / LANGUAGE ');
    // Act
    const out = parseCarAttrs(car);
    // Assert
    expect(out.functions).toEqual(['glasses', 'language']);
  });
});

// --- resolveSelectedCar ---------------------------------------------------

describe('carStatus — resolveSelectedCar', () => {
  const cars = [
    rosterCar('Red', 2, 'facerecog'),
    rosterCar('Blue', 3, 'password'),
  ];
  const positions = [
    { i: 0, px: 5, py: 0, pz: 7, ry: 90, l: 3, c: 14, s: 12.5 },
    { i: 1, px: -2, py: 0, pz: 1, ry: 0, l: 3, c: 13 },
  ];
  const leaderboard = [
    { rank: 1, name: 'Red', lap: 3, cp: 14 },
    { rank: 2, name: 'Blue', lap: 3, cp: 13 },
  ];

  it('test_resolve_joins_roster_positions_and_leaderboard_by_teamName', () => {
    // Act
    const vm = resolveSelectedCar('Red', cars, positions, leaderboard);
    // Assert: color/functions from roster, rank from leaderboard, pos + speed from state_update
    expect(vm.index).toBe(0);
    expect(vm.colorIndex).toBe(2);
    expect(vm.functions).toEqual(['facerecog']);
    expect(vm.rank).toBe(1);
    expect(vm.lap).toBe(3);
    expect(vm.cp).toBe(14);
    expect(vm.px).toBe(5);
    expect(vm.pz).toBe(7);
    expect(vm.speed).toBe(12.5);
    expect(vm.speedApprox).toBe(false);
  });

  it('test_resolve_null_selection_returns_null', () => {
    // Assert: no selection -> no view-model
    expect(resolveSelectedCar(null, cars, positions, leaderboard)).toBeNull();
  });

  it('test_resolve_tail_car_without_leaderboard_row_returns_null_rank', () => {
    // Arrange: a 16th car present in the roster + positions but NOT in the top-15 leaderboard
    const manyCars = [...cars, rosterCar('Tail', 1, 'distance')];
    const manyPositions = [...positions, { i: 2, px: 9, py: 0, pz: 9, ry: 45, l: 1, c: 2 }];
    // Act
    const vm = resolveSelectedCar('Tail', manyCars, manyPositions, leaderboard);
    // Assert: rank is null (no row) but lap/cp/pos still resolve from state_update
    expect(vm.rank).toBeNull();
    expect(vm.lap).toBe(1);
    expect(vm.cp).toBe(2);
    expect(vm.px).toBe(9);
    expect(vm.colorIndex).toBe(1);
  });

  it('test_resolve_marks_speed_approx_when_derived_flag_set', () => {
    // Arrange: a position whose speed was client-derived (Unity not rebuilt)
    const approxPositions = [{ i: 0, px: 5, py: 0, pz: 7, ry: 90, l: 3, c: 14, s: 8.2, sApprox: true }];
    // Act
    const vm = resolveSelectedCar('Red', cars, approxPositions, leaderboard);
    // Assert
    expect(vm.speed).toBe(8.2);
    expect(vm.speedApprox).toBe(true);
  });

  it('test_resolve_missing_position_leaves_speed_undefined', () => {
    // Arrange: leaderboard has the car but no state_update frame yet
    const vm = resolveSelectedCar('Blue', cars, [], leaderboard);
    // Assert: lap/cp fall back to the leaderboard; speed/pos undefined/null
    expect(vm.lap).toBe(3);
    expect(vm.cp).toBe(13);
    expect(vm.speed).toBeUndefined();
    expect(vm.px).toBeNull();
  });
});

// --- deriveSpeed ----------------------------------------------------------

describe('carStatus — deriveSpeed', () => {
  it('test_derive_speed_is_distance_over_time', () => {
    // Arrange: moved 3 in x and 4 in z over 0.5s -> hypot(3,4)=5, /0.5 = 10
    const prev = { i: 0, px: 0, pz: 0 };
    const cur = { i: 0, px: 3, pz: 4 };
    // Act
    const s = deriveSpeed(prev, cur, 0.5);
    // Assert
    expect(s).toBeCloseTo(10, 5);
  });

  it('test_derive_speed_frame1_returns_undefined', () => {
    // Arrange/Act: no previous frame
    expect(deriveSpeed(undefined, { px: 1, pz: 1 }, 0.1)).toBeUndefined();
    // Assert: non-positive dt is also undefined (no divide-by-zero)
    expect(deriveSpeed({ px: 0, pz: 0 }, { px: 1, pz: 1 }, 0)).toBeUndefined();
  });
});

// --- minimap geometry helpers --------------------------------------------

describe('carStatus — minimap transform', () => {
  it('test_normalize_applies_pz_to_y_flip', () => {
    // Arrange: a 100x100 canvas with no padding over a 10x10 world
    const t = buildTransform({ minX: 0, maxX: 10, minZ: 0, maxZ: 10 }, 100, 100, 0);
    // Act
    const bottomLeft = normalize(0, 0, t); // world min -> screen bottom
    const topRight = normalize(10, 10, t); // world max -> screen top
    // Assert: higher world Z maps to a SMALLER canvas y (drawn higher)
    expect(bottomLeft).toEqual({ x: 0, y: 100 });
    expect(topRight).toEqual({ x: 100, y: 0 });
  });

  it('test_fitEllipse_returns_center_and_radii', () => {
    // Act
    const el = fitEllipse({ minX: 0, maxX: 10, minZ: -20, maxZ: 20 });
    // Assert
    expect(el).toEqual({ cx: 5, cz: 0, rx: 5, rz: 20 });
  });
});
