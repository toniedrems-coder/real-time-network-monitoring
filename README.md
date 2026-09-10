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

Swagger UI is available at `http://localhost:5000/swagger` when the backend is running locally.

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

## GitHub Actions

Separate workflows build and validate each project when its files change:

- `.github/workflows/backend.yml`
- `.github/workflows/frontend.yml`
