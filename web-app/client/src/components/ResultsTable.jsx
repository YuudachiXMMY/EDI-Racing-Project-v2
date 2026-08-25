// Presentational race-results table shared by ResultsTab and HistoryPage. Reads PascalCase
// fields (car.Rank, car.TeamName, ...) because the rows come from the Unity C# serializer.
export default function ResultsTable({ rankings }) {
  return (
    <table className="response-table">
      <thead>
        <tr>
          <th>Rank</th>
          <th>Team</th>
          <th>Laps</th>
          <th>Checkpoints</th>
          <th>Total</th>
          <th>Best Lap</th>
          <th>Avg Lap</th>
        </tr>
      </thead>
      <tbody>
        {(rankings || []).map((car, i) => (
          <tr key={i} className="response-row">
            <td className={car.Rank <= 3 ? `rank-${car.Rank}` : ''}>{car.Rank}</td>
            <td>{car.TeamName}</td>
            <td>{car.LapsCompleted}</td>
            <td>{car.CheckpointsPassed}</td>
            <td>{(car.ElapsedTime ?? car.TotalTime ?? 0).toFixed(3)}s</td>
            <td>{(car.BestLapTime || 0).toFixed(3)}s</td>
            <td>{(car.AverageLapTime || 0).toFixed(3)}s</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
