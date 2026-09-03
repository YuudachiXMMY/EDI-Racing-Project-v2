export default function LiveLeaderboard({ rankings, selectedTeamName, onSelect }) {
  if (!rankings || rankings.length === 0) {
    return <div className="live-leaderboard empty">Waiting for race data...</div>;
  }

  return (
    <div className="live-leaderboard">
      <h3>Leaderboard</h3>
      <table className="response-table">
        <thead>
          <tr><th>#</th><th>Team</th><th>Lap</th><th>CP</th></tr>
        </thead>
        <tbody>
          {rankings.map((entry) => (
            <tr
              key={entry.name}
              className={`response-row${entry.name === selectedTeamName ? ' selected' : ''}`}
              tabIndex={0}
              role="button"
              aria-pressed={entry.name === selectedTeamName}
              onClick={() => onSelect && onSelect(entry.name)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  if (e.key === ' ') e.preventDefault();
                  if (onSelect) onSelect(entry.name);
                }
              }}
            >
              <td className={entry.rank <= 3 ? `rank-${entry.rank}` : ''}>{entry.rank}</td>
              <td>{entry.name}</td>
              <td>{entry.lap}</td>
              <td>{entry.cp}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
