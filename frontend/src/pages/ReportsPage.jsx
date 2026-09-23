import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import MetricsChart from "../components/Dashboard/MetricsChart";
import ReportExport from "../components/Dashboard/ReportExport";
import { apiFetch } from "../services/api";

export default function ReportsPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const requestedEndpointId =
    searchParams.get("endpointId") || "";

  const [endpoints, setEndpoints] = useState([]);
  const [selectedEndpointId, setSelectedEndpointId] =
    useState(requestedEndpointId);

  const [metrics, setMetrics] = useState(null);
  const [error, setError] = useState("");
  const [loadingMetrics, setLoadingMetrics] =
    useState(false);

  // --------------------------------------------------
  // Load monitored endpoints
  // --------------------------------------------------

  useEffect(() => {
    let cancelled = false;

    async function loadEndpoints() {
      try {
        const response =
          await apiFetch("/endpoints");

        assertResponse(response);

        const data = await response.json();

        if (cancelled) return;

        setEndpoints(data);
        setError("");

        setSelectedEndpointId((current) =>
          data.some(
            (endpoint) =>
              endpoint.id === current
          )
            ? current
            : data[0]?.id ?? ""
        );
      } catch {
        if (!cancelled) {
          setError(
            "Unable to load monitored endpoints."
          );
        }
      }
    }

    loadEndpoints();

    const timer =
      setInterval(loadEndpoints, 30_000);

    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, []);

  // --------------------------------------------------
  // Load latest metrics for selected endpoint
  // --------------------------------------------------

  useEffect(() => {
    if (!selectedEndpointId) {
      setMetrics(null);
      return undefined;
    }

    setSearchParams(
      {
        endpointId: selectedEndpointId,
      },
      {
        replace: true,
      }
    );

    let cancelled = false;

    async function loadMetrics() {
      try {
        setLoadingMetrics(true);

        const response =
          await apiFetch(
            `/metrics/${selectedEndpointId}`
          );

        assertResponse(response);

        const data = await response.json();

        if (!cancelled) {
          setMetrics(data);
        }
      } catch {
        if (!cancelled) {
          setMetrics(null);
        }
      } finally {
        if (!cancelled) {
          setLoadingMetrics(false);
        }
      }
    }

    loadMetrics();

    const timer =
      setInterval(loadMetrics, 30_000);

    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, [
    selectedEndpointId,
    setSearchParams,
  ]);

  const selectedEndpoint =
    endpoints.find(
      (endpoint) =>
        endpoint.id === selectedEndpointId
    );

  const statusClass =
    selectedEndpoint?.status
      ?.toLowerCase()
      .split(" ")[0] || "";

  return (
    <main className="page">

      {/* Page heading */}

      <div className="page-heading">
        <div>
          <p className="eyebrow">
            Endpoint investigation
          </p>

          <h1>Detailed Metrics</h1>

          <p className="muted">
            Review endpoint health,
            performance trends and monitoring
            history.
          </p>
        </div>

        <span className="live-indicator">
          Refreshes every 30 seconds
        </span>
      </div>

      {/* Endpoint selector */}

      <section className="panel">
        <div className="card-row">
          <div>
            <h2>Endpoint</h2>

            <p className="muted">
              Select an endpoint to investigate.
            </p>
          </div>

          <label className="inline-field">
            Endpoint

            <select
              value={selectedEndpointId}
              onChange={(event) =>
                setSelectedEndpointId(
                  event.target.value
                )
              }
            >
              {!endpoints.length && (
                <option value="">
                  No endpoints available
                </option>
              )}

              {endpoints.map(
                (endpoint) => (
                  <option
                    key={endpoint.id}
                    value={endpoint.id}
                  >
                    {endpoint.name}
                  </option>
                )
              )}
            </select>
          </label>
        </div>

        {error && (
          <p className="error">
            {error}
          </p>
        )}

        {selectedEndpoint && (
          <div className="report-endpoint-summary">
            <div>
              <strong>
                {selectedEndpoint.name}
              </strong>

              <p className="muted">
                {selectedEndpoint.url}
              </p>
            </div>

            <span
              className={`status ${statusClass}`}
            >
              {selectedEndpoint.status}
            </span>
          </div>
        )}
      </section>

      {/* Latest KPI cards */}

      {selectedEndpoint && (
        <section>
          <div className="card-row">
            <div>
              <h2>Current performance</h2>

              <p className="muted">
                Latest monitoring observation
                for {selectedEndpoint.name}.
              </p>
            </div>

            {loadingMetrics && (
              <span className="muted">
                Refreshing...
              </span>
            )}
          </div>

          {metrics ? (
            <div className="report-kpi-grid">

              <MetricCard
                title="Latency"
                value={`${formatNumber(
                  metrics.latencyMs
                )} ms`}
              />

              <MetricCard
                title="Availability"
                value={`${formatNumber(
                  metrics.availabilityPercent
                )}%`}
              />

              <MetricCard
                title="Error rate"
                value={`${formatNumber(
                  metrics.errorRatePercent
                )}%`}
              />

              <MetricCard
                title="Packet loss"
                value={`${formatNumber(
                  metrics.packetLossPercent
                )}%`}
              />

              <MetricCard
                title="Throughput"
                value={`${formatNumber(
                  metrics.throughputMbps,
                  4
                )} Mbps`}
              />

              <MetricCard
                title="Reachability"
                value={
                  metrics.isReachable
                    ? "Reachable"
                    : "Unreachable"
                }
              />

            </div>
          ) : (
            !loadingMetrics && (
              <p className="muted">
                No current metric data is
                available for this endpoint.
              </p>
            )
          )}
        </section>
      )}

      {/* Historical trends */}

      <section className="panel">
        <div className="card-row">
          <div>
            <h2>
              Historical trend analysis
            </h2>

            <p className="muted">
              Review changes in latency,
              availability, error rate and
              throughput over time.
            </p>
          </div>
        </div>

        {selectedEndpointId ? (
          <MetricsChart
            endpointId={selectedEndpointId}
          />
        ) : (
          <p className="muted">
            No endpoint data is available yet.
          </p>
        )}
      </section>

      {/* Report export */}

      <ReportExport />

    </main>
  );
}

function MetricCard({
  title,
  value,
}) {
  return (
    <article className="panel report-kpi-card">
      <span className="muted">
        {title}
      </span>

      <strong className="report-kpi-value">
        {value}
      </strong>
    </article>
  );
}

function formatNumber(
  value,
  decimals = 2
) {
  if (
    value === null ||
    value === undefined ||
    Number.isNaN(Number(value))
  ) {
    return "--";
  }

  return Number(value).toFixed(decimals);
}

function assertResponse(response) {
  if (!response.ok) {
    throw new Error("Request failed.");
  }

  return response;
}