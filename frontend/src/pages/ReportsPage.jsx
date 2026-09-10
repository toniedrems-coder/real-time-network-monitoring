import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import MetricsChart from "../components/Dashboard/MetricsChart";
import ReportExport from "../components/Dashboard/ReportExport";
import { apiFetch } from "../services/api";

export default function ReportsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedEndpointId = searchParams.get("endpointId") || "";
  const [endpoints, setEndpoints] = useState([]);
  const [selectedEndpointId, setSelectedEndpointId] = useState(requestedEndpointId);
  const [metrics, setMetrics] = useState(null);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    const loadEndpoints = () => apiFetch("/endpoints")
      .then(assertResponse)
      .then((response) => response.json())
      .then((data) => {
        if (cancelled) return;
        setEndpoints(data);
        setSelectedEndpointId((current) =>
          data.some((endpoint) => endpoint.id === current)
            ? current
            : data[0]?.id ?? "");
      })
      .catch(() => {
        if (!cancelled) setError("Unable to load monitored endpoints.");
      });

    loadEndpoints();
    const timer = setInterval(loadEndpoints, 30_000);
    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, []);

  useEffect(() => {
    if (!selectedEndpointId) {
      setMetrics(null);
      return undefined;
    }

    setSearchParams({ endpointId: selectedEndpointId }, { replace: true });
    let cancelled = false;
    const loadMetrics = () => apiFetch(`/metrics/${selectedEndpointId}`)
      .then(assertResponse)
      .then((response) => response.json())
      .then((data) => {
        if (!cancelled) setMetrics(data);
      })
      .catch(() => {
        if (!cancelled) setMetrics(null);
      });

    loadMetrics();
    const timer = setInterval(loadMetrics, 30_000);
    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, [selectedEndpointId, setSearchParams]);

  const selectedEndpoint = endpoints.find((endpoint) => endpoint.id === selectedEndpointId);

  return (
    <main className="page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">Exports & trends</p>
          <h1>Reports</h1>
          <p className="muted">Review seeded endpoint status and export live monitoring data.</p>
        </div>
        <span className="live-indicator">Refreshes every 30 seconds</span>
      </div>
      <ReportExport />
      <section className="panel">
        <div className="card-row">
          <h2>Historical trend analysis</h2>
          <label className="inline-field">
            Endpoint
            <select value={selectedEndpointId} onChange={(event) => setSelectedEndpointId(event.target.value)}>
              {!endpoints.length && <option value="">No endpoints available</option>}
              {endpoints.map((endpoint) => <option key={endpoint.id} value={endpoint.id}>{endpoint.name}</option>)}
            </select>
          </label>
        </div>
        {error && <p className="error">{error}</p>}
        {selectedEndpoint && (
          <div className="report-endpoint-summary">
            <strong>{selectedEndpoint.url}</strong>
            <span className={`status ${selectedEndpoint.status.toLowerCase().split(" ")[0]}`}>{selectedEndpoint.status}</span>
            {metrics && <span className="muted">{metrics.latencyMs.toFixed(2)} ms latency · {metrics.availabilityPercent.toFixed(2)}% availability</span>}
          </div>
        )}
        {selectedEndpointId ? <MetricsChart endpointId={selectedEndpointId} /> : <p className="muted">No endpoint data is available yet.</p>}
      </section>
    </main>
  );
}

function assertResponse(response) {
  if (!response.ok) throw new Error("Request failed.");
  return response;
}