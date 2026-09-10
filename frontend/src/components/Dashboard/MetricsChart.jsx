import { useEffect, useRef, useState } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import {
  BarController,
  BarElement,
  CategoryScale,
  Chart,
  Filler,
  Legend,
  LinearScale,
  LineController,
  LineElement,
  PointElement,
  Title,
  Tooltip,
} from "chart.js";
import { apiBase, apiFetch, signalRAccessTokenFactory } from "../../services/api";

Chart.register(
  BarController,
  BarElement,
  CategoryScale,
  Filler,
  Legend,
  LinearScale,
  LineController,
  LineElement,
  PointElement,
  Title,
  Tooltip,
);

const hubBase = import.meta.env.VITE_SIGNALR_URL || `${apiBase}/hubs/metrics`;

export default function MetricsChart({ endpointId }) {
  const canvasRef = useRef(null);
  const chartRef = useRef(null);
  const [trends, setTrends] = useState([]);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!endpointId) return undefined;

    let cancelled = false;
    let connection;
    setError("");
    apiFetch(`/metrics/trends/${endpointId}`)
      .then((response) => {
        if (!response.ok) throw new Error("Trend request failed.");
        return response.json();
      })
      .then((data) => {
        if (!cancelled) setTrends(data);
      })
      .catch(() => {
        if (!cancelled) {
          setTrends([]);
          setError("Unable to load trend data.");
        }
      });

    connection = new HubConnectionBuilder()
      .withUrl(hubBase, { accessTokenFactory: signalRAccessTokenFactory })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.on("MetricUpdated", (metric) => {
      if (metric.endpointId !== endpointId || cancelled) return;
      setTrends((current) => {
        const next = [...current, metric].slice(-100);
        return next;
      });
    });
    connection.start()
      .then(() => connection.invoke("SubscribeToEndpoint", endpointId))
      .catch(() => {
        if (!cancelled) setError("Live updates unavailable; showing REST data.");
      });

    return () => {
      cancelled = true;
      connection?.invoke("UnsubscribeFromEndpoint", endpointId).catch(() => {});
      connection?.stop();
    };
  }, [endpointId]);

  useEffect(() => {
    if (!canvasRef.current || !trends.length) return undefined;

    chartRef.current?.destroy();
    chartRef.current = new Chart(canvasRef.current, {
      type: "bar",
      data: {
        labels: trends.map((point) =>
          new Date(point.timestamp).toLocaleTimeString([], {
            hour: "2-digit",
            minute: "2-digit",
          }),
        ),
        datasets: [
          {
            type: "bar",
            label: "Latency (ms)",
            data: trends.map((point) => point.latencyMs),
            backgroundColor: "#5b82e5",
            borderRadius: 3,
            yAxisID: "y",
          },
          {
            type: "line",
            label: "Availability (%)",
            data: trends.map((point) => point.availabilityPercent),
            borderColor: "#16803c",
            backgroundColor: "#16803c",
            tension: 0.3,
            pointRadius: 2,
            yAxisID: "percentage",
          },
          {
            type: "line",
            label: "Error rate (%)",
            data: trends.map((point) => point.errorRatePercent),
            borderColor: "#b54708",
            backgroundColor: "#b54708",
            tension: 0.3,
            pointRadius: 2,
            yAxisID: "percentage",
          },
          {
            type: "line",
            label: "Throughput (Mbps)",
            data: trends.map((point) => point.throughputMbps),
            borderColor: "#8b5cf6",
            backgroundColor: "#8b5cf6",
            tension: 0.3,
            pointRadius: 2,
            yAxisID: "y",
          },
        ],
      },
      options: {
        maintainAspectRatio: false,
        responsive: true,
        interaction: { mode: "index", intersect: false },
        plugins: {
          title: { display: true, text: "Endpoint KPI trends" },
          legend: { position: "bottom" },
        },
        scales: {
          y: { beginAtZero: true, title: { display: true, text: "Latency / throughput" } },
          percentage: {
            beginAtZero: true,
            max: 100,
            position: "right",
            grid: { drawOnChartArea: false },
            title: { display: true, text: "Percentage" },
          },
        },
      },
    });

    return () => {
      chartRef.current?.destroy();
      chartRef.current = null;
    };
  }, [trends]);

  if (error) return <p className="error">{error}</p>;
  if (!trends.length) return <p className="muted">No trend data is available yet.</p>;

  return (
    <div className="chart-container" aria-label="Latency, availability, error rate, and throughput trends">
      <div className="chart-canvas">
        <canvas ref={canvasRef} />
      </div>
    </div>
  );
}
