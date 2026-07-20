export default function LiveEventFeed({ events }) {
  if (!events || events.length === 0) {
    return <div className="live-events empty">No events yet</div>;
  }

  return (
    <div className="live-events">
      <h3>Event Feed</h3>
      <div className="event-list">
        {events.map((evt, i) => (
          <div key={i} className="event-item">
            <span className="event-name">{evt.name}</span>
            <span className="event-detail">{evt.affected}/{evt.total} cars</span>
          </div>
        ))}
      </div>
    </div>
  );
}
