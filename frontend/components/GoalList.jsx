export default function GoalList({ items }) {
  if (!items || items.length === 0) {
    return (
      <div className="empty-state">
        <p>📋 No saved goals yet. Create one by refining a goal above!</p>
      </div>
    )
  }

  return (
    <div className="list">
      {items.map((item) => (
        <div key={item.id} className="list-item">
          <h3>{item.refined_goal}</h3>
          <div>
            <h4 style={{ fontSize: '0.95rem', marginBottom: '0.5rem' }}>Key Results:</h4>
            <ul style={{ marginLeft: 0 }}>
              {item.key_results?.map((k, i) => (
                <li key={i}>{k}</li>
              ))}
            </ul>
          </div>
          <div className="meta">
            <span className="meta-badge">Confidence: {item.confidence_score}/10</span>
            <span>{new Date(item.saved_at).toLocaleDateString()} {new Date(item.saved_at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
          </div>
        </div>
      ))}
    </div>
  )
}
