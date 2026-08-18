import { useRef, useEffect } from 'react';

const COLORS = [
  '#e04040', '#4a9eff', '#40a040', '#e0a020', '#a040e0',
  '#e07020', '#20c0c0', '#c060a0', '#80b040', '#4060e0',
  '#c0c040', '#6080b0', '#b04040', '#40b0a0', '#a0a0a0',
];
const PADDING = 20;
const DOT_RADIUS = 6;

export default function TrackMinimap({ positions, cars }) {
  const canvasRef = useRef(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || !positions || positions.length === 0) return;

    const ctx = canvas.getContext('2d');
    const w = canvas.width;
    const h = canvas.height;

    // Compute bounding box from positions
    let minX = Infinity, maxX = -Infinity, minZ = Infinity, maxZ = -Infinity;
    for (const p of positions) {
      if (p.px < minX) minX = p.px;
      if (p.px > maxX) maxX = p.px;
      if (p.pz < minZ) minZ = p.pz;
      if (p.pz > maxZ) maxZ = p.pz;
    }

    // Handle degenerate case (all cars at same spot)
    const rangeX = Math.max(maxX - minX, 1);
    const rangeZ = Math.max(maxZ - minZ, 1);
    const scale = Math.min((w - PADDING * 2) / rangeX, (h - PADDING * 2) / rangeZ);
    const offsetX = (w - rangeX * scale) / 2;
    const offsetZ = (h - rangeZ * scale) / 2;

    // Clear canvas
    ctx.fillStyle = '#14141f';
    ctx.fillRect(0, 0, w, h);

    // Draw car dots
    for (const p of positions) {
      const x = offsetX + (p.px - minX) * scale;
      const y = offsetZ + (p.pz - minZ) * scale;
      const color = COLORS[p.i % COLORS.length];

      ctx.beginPath();
      ctx.arc(x, y, DOT_RADIUS, 0, Math.PI * 2);
      ctx.fillStyle = color;
      ctx.fill();

      // Label
      const carName = cars && cars[p.i] ? cars[p.i].teamName : `#${p.i + 1}`;
      ctx.fillStyle = '#e0e0e0';
      ctx.font = '10px system-ui';
      ctx.textAlign = 'center';
      ctx.fillText(carName, x, y - DOT_RADIUS - 3);
    }
  }, [positions, cars]);

  return (
    <div className="track-minimap">
      <h3>Track View</h3>
      <canvas ref={canvasRef} width={400} height={300} className="minimap-canvas" />
    </div>
  );
}
