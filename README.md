# Real-time Network Monitoring and Anomaly Detection

This repository contains a .NET Web API backend and a React frontend.

## Projects

- `backend/` - ASP.NET Core Web API
- `frontend/` - React application powered by Vite

## Local development

### Backend

```powershell
dotnet run --project backend
```

Swagger UI is available at `http://localhost:5011/swagger` when the backend is running locally.

### API authentication

The API validates OAuth 2.0 access tokens issued by an external identity provider.
Configure the API with a client-credentials application registration; never put the
client secret in this repository or in the React application.

Set these backend configuration valuecdcds using environment variables, user secrets, or
your deployment secret store:

```powershell
$env:Authentication__Authority = "https://login.microsoftonline.com/<tenant-id>/v2.0"
$env:Authentication__Audience = "api://<api-application-id>"
$env:Authentication__SwaggerTokenUrl = "https://login.microsoftonline.com/<tenant-id>/oauth2/v2.0/token"
$env:Authentication__SwaggerClientId = "<swagger-client-id>"
```

The API and SignalR hub require a bearer token. Swagger's **Authorize** dialog uses
the configured OAuth2 client-credentials token endpoint. For local frontend testing,
copy `frontend/.env.example` to `frontend/.env.local` and set
`VITE_API_ACCESS_TOKEN` to a short-lived token. A browser app must not perform the
client-credentials exchange because that would expose the client secret; use a
backend-for-frontend or a managed identity in production.

### Frontend

Install a current Node.js LTS release, then run:

```powershell
cd frontend
npm install
npm run dev
```

### Event-driven architecture (Kafka)

`docker-compose.kafka.yml` starts a single-broker Kafka (KRaft mode) plus Kafka UI:

```powershell
docker compose -f docker-compose.kafka.yml up -d
```

The backend publishes probed endpoint metrics to the `endpoint.metrics` topic and detected
anomalies to `endpoint.anomalies`. Separate hosted workers (`MetricsIngestionWorker`,
`AnomalyDetectionWorker`, `AnomalyIngestionWorker`) consume these topics independently, so
probing, rule/ML anomaly detection, and live dashboard updates are fully decoupled.

### Observability (OpenTelemetry + Prometheus + Grafana)

The backend is instrumented with OpenTelemetry:

- **Traces**: ASP.NET Core + HttpClient auto-instrumentation, plus a custom `ActivitySource`
  (`network-monitor-backend`), exported via OTLP when `OpenTelemetry:OtlpEndpoint` is configured.
- **Metrics**: ASP.NET Core, HttpClient, and .NET runtime instrumentation, plus custom
  instruments in `backend/Observability/Instrumentation.cs`:
  - `network_monitor.probe.duration` (histogram, ms) — endpoint probe latency
  - `network_monitor.probe.result` (counter) — probe outcomes tagged by `reachable`
  - `network_monitor.anomaly.detected` (counter) — anomalies tagged by `type` and `detectionMethod`
  - `network_monitor.kafka.produced` / `network_monitor.kafka.consumed` (counters) — Kafka
    throughput tagged by `topic`
- Metrics are exposed directly for Prometheus scraping at `/metrics` (via
  `OpenTelemetry.Exporter.Prometheus.AspNetCore`), and optionally also exported over OTLP to an
  OpenTelemetry Collector if `OpenTelemetry:OtlpEndpoint` (e.g. `http://localhost:4317`) is set in
  configuration/environment.

Start the observability stack (OTel Collector, Prometheus, Grafana):

```powershell
docker compose -f docker-compose.observability.yml up -d
```

- Prometheus: http://localhost:9090 (scrapes the backend's `/metrics` endpoint and the collector)
- Grafana: http://localhost:3001 (admin/admin, or anonymous viewer access) — a starter
  "Network Monitor - Overview" dashboard is auto-provisioned with panels for probe latency,
  probe results, anomaly rate, and Kafka produce/consume throughput.

## GitHub Actions

Separate workflows build and validate each project when its files change:

- `.github/workflows/backend.yml`
- `.github/workflows/frontend.yml`
