using backend.Services;
using backend.Hubs;
using backend.Messaging;
using backend.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Net;

using backend.Data;
using Microsoft.EntityFrameworkCore;
using backend.Agents.Tools.Monitoring;
using backend.Agents.Monitoring;
using backend.Agents.Abstractions;
using backend.Agents.Core;
using backend.Agents.Rca;
using backend.Agents.Rca.Tools;
using backend.Agents.Rca.Logging;
using backend.Agents.Rca.Storage;
using backend.Realtime;
using backend.Agents.Knowledge;
using backend.Agents.Remediation.Abstractions;
using backend.Agents.Remediation.Runbooks;
using backend.Agents.Remediation.Core;
using backend.Agents.Remediation.Policies;
using backend.Agents.Remediation.Services;
using backend.Agents.Remediation;

var builder = WebApplication.CreateBuilder(args);

var authority = builder.Configuration["Authentication:Authority"];
var audience = builder.Configuration["Authentication:Audience"];
var swaggerTokenUrl = builder.Configuration["Authentication:SwaggerTokenUrl"];

// In Development, fall back to a built-in dev token issuer (see DevTokenService/
// DevTokenController) so the API can be exercised end-to-end without a real
// external identity provider such as Azure AD. Production/other environments
// still require a properly configured external Authority/Audience.
var useDevAuth = builder.Environment.IsDevelopment() &&
    string.IsNullOrWhiteSpace(authority) &&
    string.IsNullOrWhiteSpace(audience) &&
    string.IsNullOrWhiteSpace(swaggerTokenUrl);

if (useDevAuth)
{
    audience = "network-monitoring-api";
    swaggerTokenUrl = builder.Configuration["Authentication:DevSwaggerTokenUrl"];
    if (string.IsNullOrWhiteSpace(swaggerTokenUrl))
    {
        swaggerTokenUrl = builder.Configuration["Swagger:TokenUrl"];
    }
}
else if (string.IsNullOrWhiteSpace(authority) ||
    string.IsNullOrWhiteSpace(audience) ||
    string.IsNullOrWhiteSpace(swaggerTokenUrl))
{
    throw new InvalidOperationException(
        "Authentication:Authority, Authentication:Audience, and Authentication:SwaggerTokenUrl must be configured.");
}

if (string.IsNullOrWhiteSpace(swaggerTokenUrl))
{
    throw new InvalidOperationException(
        "Authentication:SwaggerTokenUrl (or Swagger:TokenUrl in Development) must be configured.");
}

builder.Services.AddSingleton<DevTokenService>();

builder.Services.AddDbContext<MonitoringDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("MonitoringDatabase");

    options.UseNpgsql(connectionString);
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (useDevAuth)
        {
            // Dev-only: validate tokens issued by DevTokenService using a shared
            // symmetric signing key instead of discovering metadata from an Authority.
            var devSigningKeyValue = builder.Configuration["Authentication:DevSigningKey"]
                ?? "dev-only-signing-key-do-not-use-in-production-1234567890";
            var devIssuer = builder.Configuration["Authentication:DevIssuer"] ?? "https://localhost:7011/";
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = devIssuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(devSigningKeyValue))
            };
        }
        else
        {
            options.Authority = authority;
            options.Audience = audience;
            options.RequireHttpsMetadata = builder.Configuration.GetValue("Authentication:RequireHttpsMetadata", true);
        }

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var requestPath = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (requestPath.StartsWithSegments("/hubs/metrics") ||
                     requestPath.StartsWithSegments("/hubs/aiops")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddControllers(options =>
    options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Network Monitoring API",
        Version = "v1",
        Description = "Endpoint monitoring, KPI metrics, anomaly detection, and reporting API."
    });
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Description = "OAuth 2.0 client-credentials access token.",
        Flows = new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri(swaggerTokenUrl)
            }
        }
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});
builder.Services.AddSingleton<EndpointService>();
builder.Services.AddSingleton<AnomalyDetectionService>();
builder.Services.AddSingleton<MlAnomalyDetectionService>();
builder.Services.AddSingleton<ReportingService>();
builder.Services.AddScoped<IMetricsRepository, MetricsRepository>();
builder.Services.AddScoped<IAnomalyRepository, AnomalyRepository>();
builder.Services.AddScoped<IKpiSnapshotRepository, KpiSnapshotRepository>();
builder.Services.AddScoped<IEndpointRepository, EndpointRepository>();
builder.Services.AddHostedService<EndpointInitializationWorker>();
builder.Services.AddHttpClient();
foreach (var clientName in new[]
         {
             nameof(KubernetesOperations),
             nameof(LokiLogSearchService),
             nameof(WebhookNotificationService),
             nameof(WorkflowNotificationService),
             nameof(DockerMonitoringService)
         })
{
    builder.Services.AddHttpClient(clientName)
        .ConfigurePrimaryHttpMessageHandler(
            () => new HttpClientHandler { AllowAutoRedirect = false });
}
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var value in builder.Configuration
                 .GetSection("ForwardedHeaders:KnownProxies")
                 .Get<string[]>() ?? [])
    {
        if (!IPAddress.TryParse(value, out var address))
        {
            throw new InvalidOperationException(
                $"ForwardedHeaders:KnownProxies contains an invalid IP address: '{value}'.");
        }

        options.KnownProxies.Add(address);
    }
});
builder.Services.AddHealthChecks()
    .AddCheck("live", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseReadinessCheck>("database", tags: ["ready"]);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddSingleton<MetricsStore>();
builder.Services.AddSingleton<AnomalyStore>();
builder.Services.AddSingleton<KpiHistoryStore>();
builder.Services.AddHostedService<MonitoringService>();
builder.Services.AddHostedService<MetricsIngestionWorker>();
builder.Services.AddHostedService<AnomalyDetectionWorker>();
builder.Services.AddHostedService<AnomalyIngestionWorker>();
builder.Services.AddHostedService<KpiSnapshotWorker>();
builder.Services.AddHostedService<AlertRuleEvaluator>();

builder.Services.AddSingleton<Instrumentation>();
builder.Services.AddSingleton<IEndpointProbeService, EndpointProbeService>();

builder.Services.AddSingleton<MonitoringAgent>();

builder.Services.AddSingleton<IAiOpsAgent>(sp => sp.GetRequiredService<MonitoringAgent>());

builder.Services.AddSingleton<AgentRegistry>();

builder.Services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();

builder.Services.AddScoped<IncidentService>();
builder.Services.AddScoped<IncidentWorkflowService>();
builder.Services.AddScoped<WebhookNotificationService>();
builder.Services.AddScoped<WorkflowNotificationService>();
builder.Services.AddSingleton<DockerMonitoringService>();
builder.Services.AddSingleton<LogIpDiscoveryService>();

builder.Services.AddSingleton<RootCauseAnalysisService>();

builder.Services.AddSingleton<RcaAgent>();

builder.Services.AddSingleton<IAiOpsAgent>(sp => sp.GetRequiredService<RcaAgent>());

builder.Services.AddSingleton<EndpointInvestigationTool>();

builder.Services.AddSingleton<MetricsInvestigationTool>();

builder.Services.AddSingleton<LogInvestigationTool>();

builder.Services.AddSingleton<IInvestigationTool>(
    sp => sp.GetRequiredService<EndpointInvestigationTool>());

builder.Services.AddSingleton<IInvestigationTool>(
    sp => sp.GetRequiredService<MetricsInvestigationTool>());

builder.Services.AddSingleton<IInvestigationTool>(
    sp => sp.GetRequiredService<LogInvestigationTool>());

builder.Services.AddSingleton<InMemoryLogSearchService>();
builder.Services.AddSingleton<LokiLogSearchService>();
builder.Services.AddSingleton<ILogSearchService, CompositeLogSearchService>();

builder.Services.AddSingleton<IAiOpsEventPublisher, SignalRAiOpsEventPublisher>();

builder.Services.AddScoped<KnowledgeMatchingService>();

builder.Services.AddSingleton<KnowledgeAgent>();

builder.Services.AddSingleton<IAiOpsAgent>(sp => sp.GetRequiredService<KnowledgeAgent>());

builder.Services.AddScoped<KnowledgeBaseService>();

builder.Services.AddScoped<IncidentLearningService>();

builder.Services.AddScoped<IncidentKnowledgeMatchService>();

builder.Services.AddScoped<IncidentResolutionService>();

builder.Services.AddScoped<RemediationService>();

builder.Services.AddScoped<IRemediationRunbook, EndpointRecheckRunbook>();
builder.Services.AddSingleton<IKubernetesOperations, KubernetesOperations>();
builder.Services.AddScoped<IRemediationRunbook, RestartPodRunbook>();
builder.Services.AddScoped<IRemediationRunbook, RestartDeploymentRunbook>();
builder.Services.AddScoped<IRemediationRunbook, ScaleDeploymentRunbook>();

builder.Services.AddScoped<RemediationRunbookRegistry>();

builder.Services.AddScoped<RemediationRunbookExecutor>();

builder.Services.AddSingleton<IRcaResultStore, InMemoryRcaResultStore>();

builder.Services.AddSingleton<IRemediationPolicyEngine, RemediationPolicyEngine>();

builder.Services.AddScoped<RemediationRecommendationService>();

builder.Services.AddSingleton<RemediationAgent>();

builder.Services.AddScoped<RemediationLifecycleService>();

builder.Services.AddScoped<RemediationVerificationService>();

builder.Services.AddScoped<RemediationVerificationWorkflowService>();

builder.Services.AddSingleton<IAiOpsAgent>(sp => sp.GetRequiredService<RemediationAgent>());

var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(Instrumentation.ServiceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(Instrumentation.ServiceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(Instrumentation.ServiceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
        }
    });

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Network Monitoring API v1");
    options.RoutePrefix = "swagger";
    if (useDevAuth)
    {
        options.OAuthClientId(builder.Configuration["Authentication:DevClientId"] ?? "dev-client");
        options.OAuthClientSecret(builder.Configuration["Authentication:DevClientSecret"] ?? "dev-secret");
    }
    else
    {
        options.OAuthClientId(builder.Configuration["Authentication:SwaggerClientId"] ?? string.Empty);
    }
});

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHub<MetricsHub>("/hubs/metrics");
app.MapHub<AiOpsHub>("/hubs/aiops");
app.MapPrometheusScrapingEndpoint();

app.Run();

