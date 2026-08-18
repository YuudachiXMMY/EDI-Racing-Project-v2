// Presentational event-log table shared by ResultsTab and HistoryPage. Renders nothing when
// the log is empty. Reads PascalCase fields (e.Timestamp, e.EventName, ...) from the Unity
// C# serializer.
export default function EventLogTable({ eventLog }) {
  if (!((eventLog || []).length > 0)) return null;

  return (
    <div className="event-log">
      <h4>Event Log</h4>
      <table className="response-table">
        <thead>
          <tr>
            <th>Time</th>
            <th>Event</th>
            <th>Affected</th>
          </tr>
        </thead>
        <tbody>
          {eventLog.map((e, i) => (
            <tr key={i} className="response-row">
              <td>{(e.Timestamp || 0).toFixed(1)}s</td>
              <td>{e.EventName}</td>
              <td>{e.AffectedCount}/{e.TotalCars}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
