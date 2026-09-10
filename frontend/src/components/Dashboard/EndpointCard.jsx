import { Link } from "react-router-dom";

export default function EndpointCard({ endpoint }) {
  const statusClass = endpoint.status.toLowerCase().split(" ")[0];

  return (
    <article className="endpoint-card">
      <div className="card-row"><strong>{endpoint.name}</strong><span className={`status ${statusClass}`}>{endpoint.status}</span></div>
      <p>{endpoint.url}</p>
      <div className="metric-row"><span>Latency</span><strong>{endpoint.latencyMs.toFixed(2)} ms</strong></div>
      <div className="metric-row"><span>Availability</span><strong>{endpoint.availabilityPercent.toFixed(2)}%</strong></div>
      <Link className="details-link" to={`/reports?endpointId=${endpoint.endpointId}`}>View detailed metrics</Link>
    </article>
  );
}