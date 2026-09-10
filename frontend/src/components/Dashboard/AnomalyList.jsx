export default function AnomalyList({ anomalies }) {
  return (
    <section className="panel">
      <div className="card-row"><h2>Recent anomalies</h2><span className="muted">{anomalies.length} detected</span></div>
      {anomalies.length ? <div className="anomaly-list">{anomalies.map((anomaly) => (
        <article className="anomaly" key={anomaly.id}>
          <div className="card-row"><strong>{anomaly.type}</strong><span className={`status ${anomaly.severity.toLowerCase()}`}>{anomaly.severity}</span></div>
          <p>{anomaly.description}</p>
          <time dateTime={anomaly.detectedAt}>{new Date(anomaly.detectedAt).toLocaleString()}</time>
        </article>
      ))}</div> : <p className="muted">No anomalies detected.</p>}
    </section>
  );
}