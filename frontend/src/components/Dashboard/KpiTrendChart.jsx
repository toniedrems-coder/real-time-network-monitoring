import { useEffect, useRef } from "react";
import {
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

Chart.register(
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

export default function KpiTrendChart({ history }) {
  const canvasRef = useRef(null);
  const chartRef = useRef(null);

  useEffect(() => {
    if (!canvasRef.current || !history?.length) return undefined;

    chartRef.current?.destroy();
    chartRef.current = new Chart(canvasRef.current, {
      type: "line",
      data: {
        labels: history.map((point) =>
          new Date(point.timestamp).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }),
        ),
        datasets: [
          {
            label: "Healthy endpoints (%)",
            data: history.map((point) => point.healthyPercent),
            borderColor: "#16803c",
            backgroundColor: "#16803c",
            tension: 0.3,
            pointRadius: 1,
            yAxisID: "percentage",
          },
          {
            label: "p95 latency (ms)",
            data: history.map((point) => point.p95LatencyMs),
            borderColor: "#5b82e5",
            backgroundColor: "#5b82e5",
            tension: 0.3,
            pointRadius: 1,
            yAxisID: "latency",
          },
          {
            label: "Anomalies / hour",
            data: history.map((point) => point.anomalyRatePerHour),
            borderColor: "#b54708",
            backgroundColor: "#b54708",
            tension: 0.3,
            pointRadius: 1,
            yAxisID: "latency",
          },
        ],
      },
      options: {
        maintainAspectRatio: false,
        responsive: true,
        interaction: { mode: "index", intersect: false },
        plugins: {
          title: { display: true, text: "24-hour KPI trend" },
          legend: { position: "bottom" },
        },
        scales: {
          latency: { beginAtZero: true, title: { display: true, text: "Latency / anomaly rate" } },
          percentage: {
            beginAtZero: true,
            max: 100,
            position: "right",
            grid: { drawOnChartArea: false },
            title: { display: true, text: "Healthy %" },
          },
        },
      },
    });

    return () => {
      chartRef.current?.destroy();
      chartRef.current = null;
    };
  }, [history]);

  if (!history?.length) {
    return <p className="muted">KPI history will appear here once snapshots are recorded (every 5 minutes).</p>;
  }

  return (
    <div className="chart-container" aria-label="Historical healthy percentage, latency, and anomaly rate trend">
      <div className="chart-canvas">
        <canvas ref={canvasRef} />
      </div>
    </div>
  );
}
