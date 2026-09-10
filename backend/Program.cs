using backend.Services;
using backend.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var authority = builder.Configuration["Authentication:Authority"];
var audience = builder.Configuration["Authentication:Audience"];
var swaggerTokenUrl = builder.Configuration["Authentication:SwaggerTokenUrl"];
if (string.IsNullOrWhiteSpace(authority) ||
    string.IsNullOrWhiteSpace(audience) ||
    string.IsNullOrWhiteSpace(swaggerTokenUrl))
{
    throw new InvalidOperationException(
        "Authentication:Authority, Authentication:Audience, and Authentication:SwaggerTokenUrl must be configured.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = builder.Configuration.GetValue("Authentication:RequireHttpsMetadata", true);
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var requestPath = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    requestPath.StartsWithSegments("/hubs/metrics"))
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
            .AllowAnyMethod());
});
builder.Services.AddSingleton<EndpointService>();
builder.Services.AddSingleton<AnomalyDetectionService>();
builder.Services.AddSingleton<ReportingService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MonitoringService>();
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<MonitoringService>());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Network Monitoring API v1");
    options.RoutePrefix = "swagger";
    options.OAuthClientId(builder.Configuration["Authentication:SwaggerClientId"] ?? string.Empty);
});

app.UseHttpsRedirection();
app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<MetricsHub>("/hubs/metrics");

app.Run();
