// Pure, DOM-free selectors + minimap geometry helpers for the student 2D live view.
// Kept free of React/Canvas so it runs in the existing Node Vitest env (no jsdom) and is the
// single testable home for the color/functions/rank/speed logic the UI merely renders.

// Mirror of Unity CarData.ColorIndex / CarData.Functions (see CardData accessor contract):
// attrs is an ARRAY of { k, v } pairs (NOT an object). colorIndex defaults to 0 and functions
// to [] when the survey config omits them.
export function parseCarAttrs(car) {
  const attrs = car && Array.isArray(car.attrs) ? car.attrs : [];
  const find = (k) => {
    const a = attrs.find((x) => x && x.k === k);
    return a ? a.v : undefined;
  };
  const parsedColor = parseInt(find('colorIndex'), 10);
  const colorIndex = Number.isFinite(parsedColor) ? parsedColor : 0;
  const functions = (find('functions') || '')
    .split('/')
    .map((f) => f.trim().toLowerCase())
    .filter(Boolean);
  return { colorIndex, functions };
}

// Merge roster (cars, carries attrs), live positions (state_update, all cars) and leaderboard
// (top-15, name/rank/lap/cp) into one view-model for the selected team. Selection joins by
// teamName end-to-end: the leaderboard has no car index, positions have no name, the roster
// links them. Returns null when nothing is selected.
export function resolveSelectedCar(selectedTeamName, cars = [], positions = [], leaderboard = []) {
  if (!selectedTeamName) return null;

  const index = cars.findIndex((c) => c && c.teamName === selectedTeamName);
  const car = index >= 0 ? cars[index] : null;
  const { colorIndex, functions } = parseCarAttrs(car);

  // state_update carries every car (not just the top 15), keyed by spawn index i.
  const pos = index >= 0 ? positions.find((p) => p && p.i === index) || null : null;
  // Leaderboard is capped at 15 — a tail car resolves to rank null (panel shows "—").
  const lbEntry = leaderboard.find((e) => e && e.name === selectedTeamName) || null;

  return {
    index,
    teamName: selectedTeamName,
    colorIndex,
    functions,
    rank: lbEntry ? lbEntry.rank : null,
    lap: pos ? pos.l : lbEntry ? lbEntry.lap : null,
    cp: pos ? pos.c : lbEntry ? lbEntry.cp : null,
    px: pos ? pos.px : null,
    pz: pos ? pos.pz : null,
    ry: pos ? pos.ry : null,
    // Authoritative `s` (Unity rebuilt) or a client-derived value merged upstream; undefined
    // on the very first frame before a delta exists.
    speed: pos && typeof pos.s === 'number' ? pos.s : undefined,
    speedApprox: pos ? !!pos.sApprox : false,
  };
}

// Fallback live-speed when Unity has not been rebuilt to emit CarNetState.s: ground distance
// travelled between two frames over the elapsed time. Undefined when a delta cannot be formed
// (frame 1, missing frame, or non-positive dt).
export function deriveSpeed(prevPos, curPos, dt) {
  if (!prevPos || !curPos || !dt || dt <= 0) return undefined;
  const dpx = curPos.px - prevPos.px;
  const dpz = curPos.pz - prevPos.pz;
  return Math.hypot(dpx, dpz) / dt;
}

// Build a FIXED world->canvas transform from track bounds (no per-frame bbox, so the map does
// not jitter). Preserves aspect ratio and centers within the padded canvas.
export function buildTransform(bounds, w, h, padding = 20) {
  const rangeX = Math.max(bounds.maxX - bounds.minX, 1);
  const rangeZ = Math.max(bounds.maxZ - bounds.minZ, 1);
  const scale = Math.min((w - padding * 2) / rangeX, (h - padding * 2) / rangeZ);
  const offsetX = (w - rangeX * scale) / 2;
  const offsetY = (h - rangeZ * scale) / 2;
  return { minX: bounds.minX, minZ: bounds.minZ, scale, offsetX, offsetY, height: h };
}

// World (px, pz) -> canvas (x, y). Applies the pz->y flip so higher world Z draws higher on
// screen (canvas y grows downward), matching the Unity top-down orientation.
export function normalize(px, pz, transform) {
  const x = transform.offsetX + (px - transform.minX) * transform.scale;
  const yNoFlip = transform.offsetY + (pz - transform.minZ) * transform.scale;
  return { x, y: transform.height - yNoFlip };
}

// Ellipse fitted to the track bounds (center + world-space radii). The caller normalizes the
// center and multiplies the radii by transform.scale to draw the stylized elliptical outline.
export function fitEllipse(bounds) {
  return {
    cx: (bounds.minX + bounds.maxX) / 2,
    cz: (bounds.minZ + bounds.maxZ) / 2,
    rx: (bounds.maxX - bounds.minX) / 2,
    rz: (bounds.maxZ - bounds.minZ) / 2,
  };
}
