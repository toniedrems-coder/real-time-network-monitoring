import { useNavigate } from "react-router-dom";

export default function EndpointCard({ endpoint }) {
  const navigate = useNavigate();
  const statusClass = endpoint.status.toLowerCase().split(" ")[0];
  const target = `/reports?endpointId=${endpoint.endpointId}`;

  return (
    <article
      className="endpoint-card clickable"
      role="link"
      tabIndex={0}
      aria-label={`${endpoint.name}: view detailed metrics`}
      onClick={() => navigate(target)}
      onKeyDown={(event) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          navigate(target);
        }
      }}
    >
      <div className="card-row"><strong>{endpoint.name}</strong><span className={`status ${statusClass}`}>{endpoint.status}</span></div>
      <p>{endpoint.url}</p>
      <div className="metric-row"><span>Latency</span><strong>{endpoint.latencyMs.toFixed(2)} ms</strong></div>
      <div className="metric-row"><span>Availability</span><strong>{endpoint.availabilityPercent.toFixed(2)}%</strong></div>
      <div className="metric-row"><span>Anomalies</span><strong>{endpoint.anomalyCount}</strong></div>
      {endpoint.lastAnomalyAt && (
        <div className="metric-row"><span>Last anomaly</span><strong>{new Date(endpoint.lastAnomalyAt).toLocaleString()}</strong></div>
      )}
      <span className="details-link">View detailed metrics →</span>
    </article>
  );
}
