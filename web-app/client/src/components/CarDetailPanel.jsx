import { CAR_COLORS, CAR_COLOR_NAMES, FUNCTION_LABELS } from '../constants.js';
import { resolveSelectedCar } from '../lib/carStatus.js';

// Read-only status panel for the currently selected car. All data comes from the pure
// resolveSelectedCar selector (roster + live positions + leaderboard, joined by teamName).
export default function CarDetailPanel({ selectedTeamName, cars, positions, leaderboard }) {
  const vm = resolveSelectedCar(selectedTeamName, cars, positions, leaderboard);

  if (!vm) {
    return (
      <div className="car-detail-panel empty">
        <h3>Car Status</h3>
        <p className="car-detail-hint">Click a car in the leaderboard or on the map to see its status.</p>
      </div>
    );
  }

  const color = CAR_COLORS[vm.colorIndex] || CAR_COLORS[0];
  const colorName = CAR_COLOR_NAMES[vm.colorIndex] || 'Default';
  const rankClass = vm.rank && vm.rank <= 3 ? `rank-${vm.rank}` : '';
  const speedDisplay =
    vm.speed === undefined
      ? 'n/a'
      : `${vm.speed.toFixed(1)} u/s${vm.speedApprox ? ' (approx)' : ''}`;

  return (
    <div className="car-detail-panel">
      <h3>Car Status</h3>
      <div className="car-detail-head">
        <span className="color-swatch" style={{ background: color }} aria-hidden="true"></span>
        <span className="car-detail-name">{vm.teamName}</span>
        <span className="car-detail-color">{colorName}</span>
      </div>

      <div className="car-detail-stats">
        <div className="car-stat">
          <span className="car-stat-label">Rank</span>
          <span className={`car-stat-value ${rankClass}`}>{vm.rank ?? '—'}</span>
        </div>
        <div className="car-stat">
          <span className="car-stat-label">Laps</span>
          <span className="car-stat-value">{vm.lap ?? '—'}</span>
        </div>
        <div className="car-stat">
          <span className="car-stat-label">CP</span>
          <span className="car-stat-value">{vm.cp ?? '—'}</span>
        </div>
        <div className="car-stat">
          <span className="car-stat-label">Speed</span>
          <span className="car-stat-value">{speedDisplay}</span>
        </div>
      </div>

      <div className="car-detail-functions">
        <span className="car-stat-label">Equipped (initial loadout)</span>
        <div className="function-chips">
          {vm.functions.length === 0 ? (
            <span className="function-chip empty">No functions</span>
          ) : (
            vm.functions.map((f, i) => (
              <span key={i} className="function-chip">{FUNCTION_LABELS[f] || f}</span>
            ))
          )}
        </div>
      </div>
    </div>
  );
}
