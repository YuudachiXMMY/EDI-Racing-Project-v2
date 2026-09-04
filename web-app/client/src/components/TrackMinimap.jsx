import { useRef, useEffect } from 'react';
import { CAR_COLORS } from '../constants.js';
import { parseCarAttrs, buildTransform, normalize, fitEllipse } from '../lib/carStatus.js';

// Spawn-order palette used ONLY as a fallback when a position has no matching roster car (so no
// real colorIndex). Real cars are colored by CAR_COLORS[colorIndex] (the Unity mirror).
const FALLBACK_COLORS = [
  '#e04040', '#4a9eff', '#40a040', '#e0a020', '#a040e0',
  '#e07020', '#20c0c0', '#c060a0', '#80b040', '#4060e0',
  '#c0c040', '#6080b0', '#b04040', '#40b0a0', '#a0a0a0',
];
const LOGICAL_W = 400;
const LOGICAL_H = 300;
const PADDING = 24;
const DOT_RADIUS = 6;
const HIT_SLOP = 6;

export default function TrackMinimap({ positions, cars, selectedTeamName, onSelect, trackGeometry }) {
  const canvasRef = useRef(null);
  // Canvas has no DOM nodes — remember each dot's logical (x,y) + team so clicks can hit-test.
  const dotsRef = useRef([]);
  // Fallback map reference when Unity has not been rebuilt to emit track_geometry: accumulate
  // bounds across frames (NOT per-frame, so the frame does not jitter) and snapshot the first
  // frame's centroid as an approximate start.
  const accBounds = useRef({ minX: Infinity, maxX: -Infinity, minZ: Infinity, maxZ: -Infinity });
  const startFallback = useRef(null);

  // A new race clears trackGeometry (useRaceWebSocket sets it null on race_start). Reset the
  // accumulated fallback bounds + start snapshot on that transition so a second race in the same
  // session re-fits to the new track instead of inheriting the previous race's extent.
  useEffect(() => {
    if (trackGeometry === null) {
      accBounds.current = { minX: Infinity, maxX: -Infinity, minZ: Infinity, maxZ: -Infinity };
      startFallback.current = null;
    }
  }, [trackGeometry]);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const hasGeom = trackGeometry && Number.isFinite(trackGeometry.minX);
    const hasPositions = positions && positions.length > 0;
    if (!hasGeom && !hasPositions) return;

    // --- Resolve bounds + start (real geometry, or accumulated fallback) -----------------
    let bounds;
    let start = null;
    let approximate = false;
    if (hasGeom) {
      bounds = { minX: trackGeometry.minX, maxX: trackGeometry.maxX, minZ: trackGeometry.minZ, maxZ: trackGeometry.maxZ };
      start = { x: trackGeometry.startX, z: trackGeometry.startZ };
    } else {
      approximate = true;
      const acc = accBounds.current;
      for (const p of positions) {
        if (p.px < acc.minX) acc.minX = p.px;
        if (p.px > acc.maxX) acc.maxX = p.px;
        if (p.pz < acc.minZ) acc.minZ = p.pz;
        if (p.pz > acc.maxZ) acc.maxZ = p.pz;
      }
      bounds = { ...acc };
      if (!startFallback.current && hasPositions) {
        let sx = 0, sz = 0;
        for (const p of positions) { sx += p.px; sz += p.pz; }
        startFallback.current = { x: sx / positions.length, z: sz / positions.length };
      }
      start = startFallback.current;
    }

    const transform = buildTransform(bounds, LOGICAL_W, LOGICAL_H, PADDING);

    // --- Crisp rendering on HiDPI displays ----------------------------------------------
    const dpr = window.devicePixelRatio || 1;
    canvas.width = LOGICAL_W * dpr;
    canvas.height = LOGICAL_H * dpr;
    const ctx = canvas.getContext('2d');
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

    // Background
    ctx.fillStyle = '#14141f';
    ctx.fillRect(0, 0, LOGICAL_W, LOGICAL_H);

    // --- Track outline: stylized ellipse fitted to the bounds ----------------------------
    const el = fitEllipse(bounds);
    const center = normalize(el.cx, el.cz, transform);
    const rx = Math.max(el.rx * transform.scale, 6);
    const ry = Math.max(el.rz * transform.scale, 6);
    ctx.beginPath();
    ctx.ellipse(center.x, center.y, rx, ry, 0, 0, Math.PI * 2);
    ctx.strokeStyle = approximate ? '#3a3a4a' : '#50506a';
    ctx.lineWidth = 2;
    ctx.setLineDash(approximate ? [5, 4] : []);
    ctx.stroke();
    ctx.setLineDash([]);

    // Optional true course outline if the host shipped a waypoint polyline.
    if (hasGeom && Array.isArray(trackGeometry.wpx) && trackGeometry.wpx.length > 1) {
      ctx.beginPath();
      for (let k = 0; k < trackGeometry.wpx.length; k++) {
        const wp = normalize(trackGeometry.wpx[k], trackGeometry.wpz[k], transform);
        if (k === 0) ctx.moveTo(wp.x, wp.y);
        else ctx.lineTo(wp.x, wp.y);
      }
      ctx.closePath();
      ctx.strokeStyle = 'rgba(120,120,150,0.45)';
      ctx.lineWidth = 1;
      ctx.stroke();
    }

    // --- START marker --------------------------------------------------------------------
    if (start) {
      const s = normalize(start.x, start.z, transform);
      ctx.fillStyle = '#e0a020';
      ctx.beginPath();
      ctx.moveTo(s.x, s.y - 6);
      ctx.lineTo(s.x + 6, s.y);
      ctx.lineTo(s.x, s.y + 6);
      ctx.lineTo(s.x - 6, s.y);
      ctx.closePath();
      ctx.fill();
      ctx.font = 'bold 9px system-ui';
      ctx.textAlign = 'center';
      ctx.fillText('START', s.x, s.y - 9);
    }

    // --- Car dots (real colors, selection ring, click targets) ---------------------------
    const dots = [];
    if (hasPositions) {
      for (const p of positions) {
        const { x, y } = normalize(p.px, p.pz, transform);
        const car = cars && cars[p.i];
        let color;
        if (car) {
          const { colorIndex } = parseCarAttrs(car);
          // Out-of-range colorIndex -> neutral gray (matches CarDetailPanel's "Unknown" swatch).
          color = colorIndex >= 0 && colorIndex < CAR_COLORS.length ? CAR_COLORS[colorIndex] : '#888';
        } else {
          color = FALLBACK_COLORS[p.i % FALLBACK_COLORS.length];
        }
        const teamName = car ? car.teamName : `#${p.i + 1}`;
        const selected = teamName === selectedTeamName;

        if (selected) {
          ctx.beginPath();
          ctx.arc(x, y, DOT_RADIUS + 4, 0, Math.PI * 2);
          ctx.strokeStyle = '#ffffff';
          ctx.lineWidth = 2;
          ctx.stroke();
        }

        ctx.beginPath();
        ctx.arc(x, y, selected ? DOT_RADIUS + 2 : DOT_RADIUS, 0, Math.PI * 2);
        ctx.fillStyle = color;
        ctx.fill();

        ctx.fillStyle = selected ? '#ffffff' : '#e0e0e0';
        ctx.font = selected ? 'bold 10px system-ui' : '10px system-ui';
        ctx.textAlign = 'center';
        ctx.fillText(teamName, x, y - DOT_RADIUS - 4);

        // Only real (roster-resolved) cars are clickable. A dot whose cars[p.i] is absent is
        // drawn with its synthetic "#N" label for visibility but NOT added to the hit-test set,
        // so an unresolvable phantom cannot be selected into an empty detail panel.
        if (car) dots.push({ x, y, teamName });
      }
    }
    dotsRef.current = dots;

    // Approximate-map hint when running without real geometry.
    if (approximate) {
      ctx.fillStyle = '#777';
      ctx.font = '9px system-ui';
      ctx.textAlign = 'left';
      ctx.fillText('approx. layout', 6, LOGICAL_H - 6);
    }
  }, [positions, cars, selectedTeamName, trackGeometry]);

  const handleClick = (e) => {
    const canvas = canvasRef.current;
    if (!canvas || !onSelect) return;
    const rect = canvas.getBoundingClientRect();
    if (!rect.width || !rect.height) return;
    // Displayed canvas is scaled to the container width — map click px back to logical coords.
    const cx = (e.clientX - rect.left) * (LOGICAL_W / rect.width);
    const cy = (e.clientY - rect.top) * (LOGICAL_H / rect.height);
    let best = null;
    let bestDist = Infinity;
    for (const d of dotsRef.current) {
      const dist = Math.hypot(d.x - cx, d.y - cy);
      if (dist < bestDist) { bestDist = dist; best = d; }
    }
    if (best && bestDist <= DOT_RADIUS + HIT_SLOP) onSelect(best.teamName);
  };

  return (
    <div className="track-minimap">
      <h3>Track View</h3>
      <canvas
        ref={canvasRef}
        width={LOGICAL_W}
        height={LOGICAL_H}
        className="minimap-canvas"
        onClick={handleClick}
      />
    </div>
  );
}
