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
Use `dotnet run --project backend --launch-profile http` only with the Development
authentication fallback. Do not use that profile for production.

### API authentication

The API validates OAuth 2.0 access tokens issued by an external identity provider.
Configure the API with a client-credentials application registration; never put the
client secret in this repository or in the React application.

Set these backend configuration values using environment variables, user secrets, or
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

For a host-run backend using the infrastructure stack, Kafka's host listener is
`localhost:29092`; containers use `kafka:9092`.

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

## Agent lifecycle and operations

Detected anomalies are persisted before incident automation starts. The workflow
creates or updates a deduplicated incident, runs RCA followed by the Knowledge
Agent, creates a policy-governed remediation recommendation, and records every
stage in PostgreSQL. Remediation execution is **not** automatic for approval-
required or escalated actions. Failed workflow runs can be reviewed and retried
from **Operations** (`/operations`); successful retries are idempotent per
anomaly. The authenticated workflow API is available at `/api/workflows`.

Apply database migrations before starting the API:

```powershell
dotnet ef database update --project backend --startup-project backend
```

When the API runs outside the repository directory, provide the
`ConnectionStrings__MonitoringDatabase` environment variable and use the
existing `dotnet-ef` tool installation for deployment migrations. Do not enable
automatic migration at API startup in production.

## Production integration settings

All integration credentials are server-only settings. Use a secret manager or
workload identity in production; do not put bearer tokens, webhook keys, or
database credentials in `appsettings*.json`, frontend variables, or source
control.

### Kubernetes remediation

Set `Kubernetes__ApiServer` to the cluster API's HTTPS URL and
`Kubernetes__BearerToken` to a least-privilege service-account token. Set
`Kubernetes__MaximumReplicas` to the approved upper bound for scale-up actions.
In **Operations**, operators can propose a pod restart, deployment restart, or
scale-up for an incident and supply the exact `namespace/resource-name` target.
Kubernetes targets use the exact `namespace/resource-name` form. Pod restart,
deployment restart, and scale-up runbooks use the Kubernetes API directly.
Only loopback HTTP development clusters may use
`Kubernetes__AllowInsecureHttp=true`. Grant only the required `get`, `patch`,
and `delete` permissions to the service account. Medium-risk pod restarts
require approval; high-risk deployment restart/scale actions remain escalated.

### Log search

Set `Logging__LokiBaseUrl` to a Loki base URL to add incident-window log search
to the in-memory probe log search. Optional Loki credentials use
`Logging__LokiBearerToken`. HTTPS is required outside loopback development
(`Logging__AllowInsecureHttp=true`). Queries are bounded to 1,000 results and
the incident time range; Loki query errors are surfaced to RCA callers.

### Notifications and ServiceNow

Set `Notifications__WebhookUrl` and `Notifications__WebhookSigningSecret` to
enable completed-workflow notifications. HTTPS is required except for an
explicit loopback development endpoint. Requests include an
`Idempotency-Key` (the workflow ID) and an HMAC-SHA256
`X-Network-Monitor-Signature` header. Notification delivery failure is retained
on the completed workflow so operators can see it without incorrectly
re-running a completed investigation.

Optional email alerts use `Notifications__Email__SmtpHost`, `SmtpPort`,
`UseTls`, `UserName`, `Password`, `From`, and `To`. Optional SMS alerts use
`Notifications__Sms__AccountSid`, `AuthToken`, `From`, and `To`; the SMS
adapter sends through Twilio and accepts E.164 phone numbers only. Configure
these settings through secrets/environment variables. The authenticated
`/api/notifications/status` endpoint reports masked destinations, and
`/api/notifications/test/email` or `/api/notifications/test/sms` sends only to
the configured recipient. Workflow execution summaries retain per-channel
delivery status and stage timestamps.

### Docker/container monitoring

The navigation includes **Notifications** and **Containers** pages; each workflow row links to its execution summary. The
Endpoints, Containers and Operations each show an execution flow for their task
built from live data; stages without evidence are shown as idle rather than inferred.

Container inventory and bounded log-tail access are available through the
read-only `/api/containers` endpoints when `Docker__ApiBaseUrl` points to an
authenticated, hardened Docker API proxy. Supply `Docker__BearerToken` when
required. HTTPS is mandatory except for an explicitly enabled loopback
development endpoint (`Docker__AllowInsecureHttp=true`). Do not mount the
unrestricted Docker socket into the application container. For local
development, `infrastructure/docker-compose.docker-proxy.yml` starts a
read-only, GET-only proxy on `127.0.0.1:23750`, and
`appsettings.Development.json` points `Docker__ApiBaseUrl` at it (restart the
API after changing configuration). Container IDs are
validated, list responses and log output are size-bounded, log tails are capped
at 1,000 lines, and API redirects are disabled.

ServiceNow and other ticketing systems should be connected through this
signed-webhook contract or a separately authenticated `IIncidentNotifier`
adapter; map the stable incident number, severity, target, RCA, remediation
approval/status, and workflow ID, and use the workflow ID for deduplication.
No ServiceNow credentials or tenant-specific behavior are assumed by this
repository.

## Readiness and deployment checklist

- A containerized development/staging stack (PostgreSQL, Kafka, API, and React
  behind Nginx with SignalR WebSocket proxying) is available through
  `infrastructure/docker-compose.yml`. The API/frontend images use .NET 11
  preview and Node 22 base images; replace/pin supported production base-image
  digests and move the API target framework to a released .NET LTS before
  production rollout.
- Set the required `POSTGRES_PASSWORD`, `AUTHENTICATION_AUTHORITY`,
  `AUTHENTICATION_AUDIENCE`, `AUTHENTICATION_SWAGGER_TOKEN_URL`, and
  `PUBLIC_ORIGIN` environment variables through your secret/configuration
  manager, then start the stack:

  ```powershell
  docker compose -f infrastructure\docker-compose.yml config --quiet
  docker compose -f infrastructure\docker-compose.yml up --build -d
  ```

  Apply schema migrations before enabling workflow processing:

  ```powershell
  dotnet ef database update --project backend --startup-project backend
  ```

  In container deployments, run this one-off migration from a release job
  configured with the same database connection string; never run migrations
  concurrently from API replicas.
- The bundled single-broker Kafka/PostgreSQL stack is for development and
  staging only. Production requires durable managed/multi-node databases and
  Kafka, TLS, backups, retention and recovery policies.
- `/health/live` reports process liveness; `/health/ready` checks PostgreSQL
  reachability and pending database migrations. Both endpoints intentionally
  return no credentials or connection details.
- If the API is deployed behind a different trusted TLS-terminating proxy,
  configure `ForwardedHeaders__KnownProxies__0` with that proxy's actual IP; the
  API intentionally does not trust arbitrary forwarded headers.
- Provide external OAuth authority, audience, Swagger token URL, and a production
  CORS allow-list. The built-in token issuer and its development signing key are
  Development-only; do not deploy with the Development environment.
- Remediation approval/rejection audit identity is taken from authenticated
  `name`/`preferred_username` claims, never from the request body. Use delegated
  operator tokens with one of these claims for the Operations approval controls;
  client-credentials tokens without a human identity are intentionally denied.
- Apply migrations, then verify PostgreSQL, Kafka, authentication, CORS,
  Kubernetes/Loki/webhook settings, and Prometheus/OTLP connectivity in the
  deployment environment.
- Protect Grafana with production authentication and credentials; the bundled
  local observability defaults are for development only.
- CI runs backend integration-adapter unit tests and builds the frontend. End-to-end
  tests against disposable PostgreSQL/Kafka plus a real staging Kubernetes,
  Loki, and notification environment are still required before production sign-off.



### Log-based endpoint discovery

The Endpoints page can scan recent logs of running Docker containers (requires Docker monitoring) for IPv4 addresses via `GET /api/log-discovery/scan` and add selected ones as endpoints via `POST /api/log-discovery/add`. Loopback, link-local and reserved addresses are excluded, already-monitored hosts are flagged, and nothing is added without explicit selection (max 50 per request).

### Threshold alerting (email / SMS to support contacts)

The **Notifications** page has four tabs: **Rules**, **Contacts**, **History** and **Channels**.

- **Contacts** are support people (name, email, E.164 phone).
- **Rules** target an endpoint (latency above, error rate above, availability below, unreachable) or a Docker container (not running, which covers databases and services running in containers). A rule has a duration (breach must hold for N minutes), a severity, channels (email and/or SMS), the contacts to alert, and a reminder interval (0 = notify once).
- `AlertRuleEvaluator` checks enabled rules every 30 seconds, sends a **Triggered** alert once the breach has lasted the configured duration, optional **Reminder**s, and a **Resolved** message when the metric recovers. Every alert and its per-recipient delivery result is stored in **History**.
- Delivery uses the server-level `Notifications:Email:*` (SMTP) and `Notifications:Sms:*` (Twilio) settings; the `To` values there are only used for incident-workflow summaries. If a transport is not configured, alerts are recorded as `NotConfigured` rather than silently dropped.
- API: `/api/alerts/contacts`, `/api/alerts/rules`, `/api/alerts/events`, `/api/alerts/transport`.
