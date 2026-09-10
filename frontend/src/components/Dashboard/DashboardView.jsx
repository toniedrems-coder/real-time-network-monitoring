import { useEffect, useRef, useState } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import AnomalyList from "./AnomalyList";
import EndpointCard from "./EndpointCard";
import MetricsChart from "./MetricsChart";
import ReportExport from "./ReportExport";
import { apiBase, apiFetch, signalRAccessTokenFactory } from "../../services/api";

const hubBase = import.meta.env.VITE_SIGNALR_URL || `${apiBase}/hubs/metrics`;

export default function DashboardView() {
  const [dashboard, setDashboard] = useState(null);
  const [anomalies, setAnomalies] = useState([]);
  const [selectedEndpointId, setSelectedEndpointId] = useState("");
  const [error, setError] = useState("");
  const connectionRef = useRef(null);
  const subscribedIds = useRef(new Set());

  useEffect(() => {
    let cancelled = false;
    let refreshTimer;

    const loadDashboard = () => Promise.all([
      apiFetch("/dashboard").then(assertResponse).then((response) => response.json()),
      apiFetch("/anomalies").then(assertResponse).then((response) => response.json()),
    ]).then(([dashboardData, anomalyData]) => {
      if (cancelled) return;
      setDashboard(dashboardData);
      setAnomalies(anomalyData);
      setSelectedEndpointId((current) =>
        dashboardData.endpoints?.some((endpoint) => endpoint.endpointId === current)
          ? current
          : dashboardData.endpoints?.[0]?.endpointId ?? "");

      const connection = connectionRef.current;
      if (connection && connection.state === "Connected") {
        dashboardData.endpoints?.forEach((endpoint) => {
          if (!subscribedIds.current.has(endpoint.endpointId)) {
            subscribedIds.current.add(endpoint.endpointId);
            connection.invoke("SubscribeToEndpoint", endpoint.endpointId).catch(() => {});
          }
        });
      }
    }).catch(() => {
      if (!cancelled) setError("Unable to load dashboard data.");
    });

    loadDashboard().finally(() => {
      if (!cancelled) refreshTimer = setInterval(loadDashboard, 30_000);
    });

    return () => {
      cancelled = true;
      clearInterval(refreshTimer);
    };
  }, []);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(hubBase, { accessTokenFactory: signalRAccessTokenFactory })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("AnomalyDetected", (anomaly) => {
      setAnomalies((current) => [anomaly, ...current].slice(0, 100));
    });

    connectionRef.current = connection;
    connection.start()
      .then(() => {
        dashboard?.endpoints?.forEach((endpoint) => {
          subscribedIds.current.add(endpoint.endpointId);
          connection.invoke("SubscribeToEndpoint", endpoint.endpointId).catch(() => {});
        });
      })
      .catch(() => {});

    return () => {
      connectionRef.current = null;
      connection.stop();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (error) return <main className="page"><p className="error">{error}</p></main>;
  if (!dashboard) return <main className="page"><p>Loading dashboard...</p></main>;
  return (
    <main className="page">
      <div className="page-heading"><div><p className="eyebrow">Live overview</p><h1>Network dashboard</h1></div></div>
      <section className="kpi-grid" aria-label="Key performance indicators">
        <Kpi label="Monitored endpoints" value={dashboard.totalEndpoints} />
        <Kpi label="Healthy endpoints" value={dashboard.healthyEndpoints} tone="good" />
        <Kpi label="Active anomalies" value={dashboard.activeAnomalies} tone="warning" />
      </section>
      <ReportExport />
      <section className="content-grid">
        <article className="panel"><h2>Endpoint health</h2>
          {dashboard.endpoints?.length ? <div className="endpoint-grid">{dashboard.endpoints.map((endpoint) => <EndpointCard key={endpoint.endpointId} endpoint={endpoint} />)}</div> : <p className="muted">No endpoints are being monitored yet.</p>}
        </article>
        <AnomalyList anomalies={anomalies} />
      </section>
      {selectedEndpointId && <section className="panel trend-panel">
        <div className="card-row"><h2>Endpoint trends</h2><select value={selectedEndpointId} onChange={(event) => setSelectedEndpointId(event.target.value)}>
          {dashboard.endpoints.map((endpoint) => <option key={endpoint.endpointId} value={endpoint.endpointId}>{endpoint.name}</option>)}
        </select></div>
        <MetricsChart endpointId={selectedEndpointId} />
      </section>}
    </main>
  );
}

function Kpi({ label, value, tone = "" }) {
  return <div className={`kpi-card ${tone}`}><span>{label}</span><strong>{value}</strong></div>;
}

function assertResponse(response) {
  if (!response.ok) throw new Error();
  return response;
}
