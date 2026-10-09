using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public sealed class MonitoringDbContext : DbContext
{
    public MonitoringDbContext(
        DbContextOptions<MonitoringDbContext> options)
        : base(options)
    {
    }

    public DbSet<MonitoredEndpoint> MonitoredEndpoints =>
        Set<MonitoredEndpoint>();

    public DbSet<EndpointMetrics> EndpointMetrics =>
        Set<EndpointMetrics>();

    public DbSet<DetectedAnomaly> DetectedAnomalies =>
        Set<DetectedAnomaly>();

    public DbSet<KpiSnapshot> KpiSnapshots => Set<KpiSnapshot>();

    public DbSet<Incident> Incidents => Set<Incident>();

    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();

    public DbSet<RemediationAction> RemediationActions => Set<RemediationAction>();

    public DbSet<IncidentKnowledgeMatch> IncidentKnowledgeMatches => Set<IncidentKnowledgeMatch>();

    public DbSet<WorkflowExecution> WorkflowExecutions => Set<WorkflowExecution>();

    public DbSet<SupportContact> SupportContacts => Set<SupportContact>();

    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureMonitoredEndpoint(modelBuilder);
        ConfigureEndpointMetrics(modelBuilder);
        ConfigureDetectedAnomaly(modelBuilder);
        ConfigureKpiSnapshot(modelBuilder);
        ConfigureIncident(modelBuilder);
        ConfigureKnowledgeArticle(modelBuilder);
        ConfigureIncidentKnowledgeMatch(modelBuilder);
        ConfigureRemediationAction(modelBuilder);
        ConfigureWorkflowExecution(modelBuilder);
        ConfigureAlerting(modelBuilder);


    }

    private void ConfigureRemediationAction(ModelBuilder modelBuilder)
    {
        var remediation = modelBuilder.Entity<RemediationAction>();

        remediation.ToTable("remediation_actions");

        remediation.HasKey(x => x.Id);

        remediation.Property(x => x.Id)
            .HasColumnName("id");

        remediation.Property(x => x.RemediationNumber)
            .HasColumnName("remediation_number")
            .HasMaxLength(50)
            .IsRequired();

        remediation.Property(x => x.IncidentId)
            .HasColumnName("incident_id")
            .IsRequired();

        remediation.Property(x => x.Target)
            .HasColumnName("target")
            .HasMaxLength(1000)
            .IsRequired();

        remediation.Property(x => x.TargetType)
            .HasColumnName("target_type")
            .HasMaxLength(100)
            .IsRequired();

        remediation.Property(x => x.ActionType)
            .HasColumnName("action_type")
            .HasConversion<string>()
            .HasMaxLength(100)
            .IsRequired();

        remediation.Property(x => x.RiskLevel)
            .HasColumnName("risk_level")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        remediation.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        remediation.Property(x => x.ApprovalStatus)
            .HasColumnName("approval_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        remediation.Property(x => x.RunbookName)
            .HasColumnName("runbook_name")
            .HasMaxLength(200);

        remediation.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasColumnType("text");

        remediation.Property(x => x.RequestedBy)
            .HasColumnName("requested_by")
            .HasMaxLength(200);

        remediation.Property(x => x.ApprovedBy)
            .HasColumnName("approved_by")
            .HasMaxLength(200);

        remediation.Property(x => x.ExecutionOutput)
            .HasColumnName("execution_output")
            .HasColumnType("text");

        remediation.Property(x => x.FailureReason)
            .HasColumnName("failure_reason")
            .HasColumnType("text");

        remediation.Property(x => x.VerificationSuccessful)
            .HasColumnName("verification_successful");

        remediation.Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        remediation.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        remediation.Property(x => x.ApprovedAt)
            .HasColumnName("approved_at");

        remediation.Property(x => x.StartedAt)
            .HasColumnName("started_at");

        remediation.Property(x => x.CompletedAt)
            .HasColumnName("completed_at");

        remediation.Property(x => x.VerifiedAt)
            .HasColumnName("verified_at");

        remediation.HasIndex(x => x.RemediationNumber)
            .IsUnique();

        remediation.HasIndex(x => x.IncidentId);

        remediation.HasIndex(x => x.Status);

        remediation.HasIndex(x => new
        {
            x.IncidentId,
            x.Status
        });

        remediation.Property(x => x.RejectedBy)
            .HasColumnName("rejected_by")
            .HasMaxLength(200);

        remediation.Property(x => x.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(2000);

        remediation.HasOne(x => x.Incident)
            .WithMany(x => x.RemediationActions)
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureWorkflowExecution(ModelBuilder modelBuilder)
    {
        var workflow = modelBuilder.Entity<WorkflowExecution>();
        workflow.ToTable("workflow_executions");
        workflow.HasKey(x => x.Id);
        workflow.Property(x => x.Id).HasColumnName("id");
        workflow.Property(x => x.AnomalyId).HasColumnName("anomaly_id").IsRequired();
        workflow.Property(x => x.IncidentId).HasColumnName("incident_id");
        workflow.Property(x => x.RemediationId).HasColumnName("remediation_id");
        workflow.Property(x => x.Status).HasColumnName("status").HasMaxLength(50).IsRequired();
        workflow.Property(x => x.CurrentStage).HasColumnName("current_stage").HasMaxLength(100);
        workflow.Property(x => x.Error).HasColumnName("error").HasColumnType("text");
        workflow.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        workflow.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        workflow.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        workflow.Property(x => x.StartedAt).HasColumnName("started_at");
        workflow.Property(x => x.CompletedAt).HasColumnName("completed_at");
        workflow.Property(x => x.EmailNotificationStatus).HasColumnName("email_notification_status").HasMaxLength(50).IsRequired();
        workflow.Property(x => x.SmsNotificationStatus).HasColumnName("sms_notification_status").HasMaxLength(50).IsRequired();
        workflow.Property(x => x.WebhookNotificationStatus).HasColumnName("webhook_notification_status").HasMaxLength(50).IsRequired();
        workflow.Property(x => x.NotificationError).HasColumnName("notification_error").HasColumnType("text");
        workflow.Property(x => x.StageHistory)
            .HasColumnName("stage_history")
            .HasColumnType("jsonb")
            .HasConversion(
                stages => System.Text.Json.JsonSerializer.Serialize(stages, (System.Text.Json.JsonSerializerOptions?)null),
                json => System.Text.Json.JsonSerializer.Deserialize<List<WorkflowStageEvent>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<WorkflowStageEvent>());
        workflow.HasIndex(x => x.AnomalyId).IsUnique();
        workflow.HasIndex(x => x.Status);
        workflow.HasIndex(x => x.UpdatedAt);
    }

    private static void ConfigureMonitoredEndpoint(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MonitoredEndpoint>();

        entity.ToTable("monitored_endpoints");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id");

        entity.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        entity.Property(x => x.Url)
            .HasColumnName("url")
            .HasMaxLength(2048)
            .IsRequired();

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        entity.HasIndex(x => x.Url)
            .IsUnique();
    }

    private static void ConfigureEndpointMetrics(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<EndpointMetrics>();

        entity.ToTable("endpoint_metrics");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id");

        entity.Property(x => x.EndpointId)
            .HasColumnName("endpoint_id")
            .IsRequired();

        entity.Property(x => x.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        entity.Property(x => x.LatencyMs)
            .HasColumnName("latency_ms");

        entity.Property(x => x.PacketLossPercent)
            .HasColumnName("packet_loss_percent");

        entity.Property(x => x.AvailabilityPercent)
            .HasColumnName("availability_percent");

        entity.Property(x => x.ErrorRatePercent)
            .HasColumnName("error_rate_percent");

        entity.Property(x => x.ThroughputMbps)
            .HasColumnName("throughput_mbps");

        entity.Property(x => x.IsReachable)
            .HasColumnName("is_reachable");

        entity.HasIndex(x => new { x.EndpointId, x.Timestamp });
    }

    private static void ConfigureKpiSnapshot(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<KpiSnapshot>();

        entity.ToTable("kpi_snapshots");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id");

        entity.Property(x => x.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        entity.Property(x => x.TotalEndpoints)
            .HasColumnName("total_endpoints")
            .IsRequired();

        entity.Property(x => x.HealthyEndpoints)
            .HasColumnName("healthy_endpoints")
            .IsRequired();

        entity.Property(x => x.HealthyPercent)
            .HasColumnName("healthy_percent")
            .IsRequired();

        entity.Property(x => x.AverageLatencyMs)
            .HasColumnName("average_latency_ms")
            .IsRequired();

        entity.Property(x => x.P95LatencyMs)
            .HasColumnName("p95_latency_ms")
            .IsRequired();

        entity.Property(x => x.ActiveAnomalies)
            .HasColumnName("active_anomalies")
            .IsRequired();

        entity.Property(x => x.AnomalyRatePerHour)
            .HasColumnName("anomaly_rate_per_hour")
            .IsRequired();

        entity.Property(x => x.EndpointsAtRisk)
            .HasColumnName("endpoints_at_risk")
            .IsRequired();

        entity.HasIndex(x => x.Timestamp);
    }

    private static void ConfigureDetectedAnomaly(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DetectedAnomaly>();

        entity.ToTable("detected_anomalies");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id");

        entity.Property(x => x.EndpointId)
            .HasColumnName("endpoint_id")
            .IsRequired();

        entity.Property(x => x.MetricId)
            .HasColumnName("metric_id");

        entity.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.Severity)
            .HasColumnName("severity")
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(1000)
            .IsRequired();

        entity.Property(x => x.DetectedAt)
            .HasColumnName("detected_at")
            .IsRequired();

        entity.Property(x => x.ObservedValue)
            .HasColumnName("observed_value");

        entity.Property(x => x.ExpectedValue)
            .HasColumnName("expected_value");

        entity.Property(x => x.DetectionMethod)
            .HasColumnName("detection_method")
            .HasMaxLength(50);

        entity.HasIndex(x => new { x.EndpointId, x.DetectedAt });
    }

    private static void ConfigureIncident(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Incident>();

        entity.ToTable("incidents");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id");

        entity.Property(x => x.IncidentNumber)
            .HasColumnName("incident_number")
            .HasMaxLength(50)
            .IsRequired();

        entity.HasIndex(x => x.IncidentNumber)
            .IsUnique();

        entity.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        entity.Property(x => x.Description)
            .HasColumnName("description");

        entity.Property(x => x.Source)
            .HasColumnName("source")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.SourceType)
            .HasColumnName("source_type")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.SourceId)
            .HasColumnName("source_id");

        entity.Property(x => x.Target)
            .HasColumnName("target")
            .HasMaxLength(500)
            .IsRequired();

        entity.Property(x => x.TargetType)
            .HasColumnName("target_type")
            .HasMaxLength(100)
            .IsRequired();

        entity.Property(x => x.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(20);

        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);

        entity.Property(x => x.FailureType)
            .HasColumnName("failure_type")
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(x => x.HttpStatusCode)
            .HasColumnName("http_status_code");

        entity.Property(x => x.LatencyMs)
            .HasColumnName("latency_ms");

        entity.Property(x => x.ErrorMessage)
            .HasColumnName("error_message");

        entity.Property(x => x.DetectedAt)
            .HasColumnName("detected_at");

        entity.Property(x => x.AcknowledgedAt)
            .HasColumnName("acknowledged_at");

        entity.Property(x => x.ResolvedAt)
            .HasColumnName("resolved_at");

        entity.Property(x => x.AssignedAgent)
            .HasColumnName("assigned_agent")
            .HasMaxLength(100);

        entity.Property(x => x.RootCause)
            .HasColumnName("root_cause");

        entity.Property(x => x.RecommendedAction)
            .HasColumnName("recommended_action");

        entity.Property(x => x.Resolution)
            .HasColumnName("resolution");

        entity.Property(x => x.RequiresApproval)
            .HasColumnName("requires_approval");

        entity.Property(x => x.RemediationAttempted)
            .HasColumnName("remediation_attempted");

        entity.Property(x => x.RemediationSuccessful)
            .HasColumnName("remediation_successful");

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        entity.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        entity.HasIndex(x => new
        {
            x.SourceId,
            x.Status
        });

        entity.HasIndex(x => x.DetectedAt);

        entity.HasIndex(x => x.Severity);

    }

    private void ConfigureKnowledgeArticle(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<KnowledgeArticle>();

        entity.ToTable("knowledge_articles");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id");

        entity.Property(x => x.ArticleNumber)
            .HasColumnName("article_number")
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(250)
            .IsRequired();

        entity.Property(x => x.Description)
            .HasColumnName("description")
            .IsRequired();

        entity.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(x => x.Target)
            .HasColumnName("target")
            .HasMaxLength(500);

        entity.Property(x => x.TargetType)
            .HasColumnName("target_type")
            .HasMaxLength(100);

        entity.Property(x => x.FailureType)
            .HasColumnName("failure_type")
            .HasMaxLength(100);

        entity.Property(x => x.RootCause)
            .HasColumnName("root_cause");

        entity.Property(x => x.Resolution)
            .HasColumnName("resolution");

        entity.Property(x => x.RecommendedActions)
            .HasColumnName("recommended_actions");

        entity.Property(x => x.RunbookReference)
            .HasColumnName("runbook_reference")
            .HasMaxLength(500);

        entity.Property(x => x.Tags)
            .HasColumnName("tags")
            .HasMaxLength(1000);

        entity.Property(x => x.SourceIncidentId)
            .HasColumnName("source_incident_id");

        entity.Property(x => x.Confidence)
            .HasColumnName("confidence");

        entity.Property(x => x.TimesMatched)
            .HasColumnName("times_matched");

        entity.Property(x => x.TimesSuccessful)
            .HasColumnName("times_successful");

        entity.Property(x => x.IsActive)
            .HasColumnName("is_active");

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        entity.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        entity.Property(x => x.LastUsedAt)
            .HasColumnName("last_used_at");

        entity.HasIndex(x => x.ArticleNumber)
            .IsUnique();

        entity.HasIndex(x => x.Type);

        entity.HasIndex(x => x.FailureType);

        entity.HasIndex(x => x.Target);

        entity.HasIndex(x => x.SourceIncidentId);

        entity.HasIndex(x => x.IsActive);
    }

    private void ConfigureIncidentKnowledgeMatch(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<IncidentKnowledgeMatch>();

        entity.ToTable(
            "incident_knowledge_matches");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Id)
            .HasColumnName("id");

        entity.Property(x => x.IncidentId)
            .HasColumnName("incident_id")
            .IsRequired();

        entity.Property(x => x.KnowledgeArticleId)
            .HasColumnName(
                "knowledge_article_id")
            .IsRequired();

        entity.Property(x => x.MatchScore)
            .HasColumnName("match_score")
            .IsRequired();

        entity.Property(x => x.MatchLevel)
            .HasColumnName("match_level")
            .HasMaxLength(50)
            .IsRequired();

        entity.Property(x => x.MatchReasons)
            .HasColumnName("match_reasons");

        entity.Property(x => x.WasSelected)
            .HasColumnName("was_selected")
            .IsRequired();

        entity.Property(x => x.WasSuccessful)
            .HasColumnName("was_successful");

        entity.Property(x => x.MatchedAt)
            .HasColumnName("matched_at")
            .IsRequired();

        entity.Property(x => x.VerifiedAt)
            .HasColumnName("verified_at");

        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        entity.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        entity.HasOne(x => x.Incident)
            .WithMany(x => x.KnowledgeMatches)
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.KnowledgeArticle)
            .WithMany(x => x.IncidentMatches)
            .HasForeignKey(
                x => x.KnowledgeArticleId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => x.IncidentId);

        entity.HasIndex(
            x => x.KnowledgeArticleId);

        entity.HasIndex(
            x => new
            {
                x.IncidentId,
                x.KnowledgeArticleId
            })
            .IsUnique();

        entity.HasIndex(
            x => new
            {
                x.IncidentId,
                x.WasSelected
            });

        entity.Property(
            x => x.LearningAppliedAt)
        .HasColumnName(
            "learning_applied_at");
    }

    private static void ConfigureAlerting(ModelBuilder modelBuilder)
    {



        var contact = modelBuilder.Entity<SupportContact>();
        contact.ToTable("support_contacts");
        contact.HasKey(x => x.Id);
        contact.Property(x => x.Id).HasColumnName("id");
        contact.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        contact.Property(x => x.Email).HasColumnName("email").HasMaxLength(254);
        contact.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
        contact.Property(x => x.Active).HasColumnName("active").IsRequired();
        contact.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        var rule = modelBuilder.Entity<AlertRule>();
        rule.ToTable("alert_rules");
        rule.HasKey(x => x.Id);
        rule.Property(x => x.Id).HasColumnName("id");
        rule.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        rule.Property(x => x.TargetType).HasColumnName("target_type").HasMaxLength(30).IsRequired();
        rule.Property(x => x.TargetId).HasColumnName("target_id").HasMaxLength(200).IsRequired();
        rule.Property(x => x.Metric).HasColumnName("metric").HasMaxLength(40).IsRequired();
        rule.Property(x => x.Threshold).HasColumnName("threshold").IsRequired();
        rule.Property(x => x.DurationMinutes).HasColumnName("duration_minutes").IsRequired();
        rule.Property(x => x.Severity).HasColumnName("severity").HasMaxLength(20).IsRequired();
        rule.Property(x => x.NotifyEmail).HasColumnName("notify_email").IsRequired();
        rule.Property(x => x.NotifySms).HasColumnName("notify_sms").IsRequired();
        rule.Property(x => x.ContactIds).HasColumnName("contact_ids").HasColumnType("jsonb")
            .HasConversion(ids => AlertJson.Serialize(ids), json => AlertJson.Deserialize<List<Guid>>(json));
        rule.Property(x => x.CooldownMinutes).HasColumnName("cooldown_minutes").IsRequired();
        rule.Property(x => x.Enabled).HasColumnName("enabled").IsRequired();
        rule.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        rule.Property(x => x.BreachStartedAt).HasColumnName("breach_started_at");
        rule.Property(x => x.Firing).HasColumnName("firing").IsRequired();
        rule.Property(x => x.LastNotifiedAt).HasColumnName("last_notified_at");

        var alertEvent = modelBuilder.Entity<AlertEvent>();
        alertEvent.ToTable("alert_events");
        alertEvent.HasKey(x => x.Id);
        alertEvent.Property(x => x.Id).HasColumnName("id");
        alertEvent.Property(x => x.RuleId).HasColumnName("rule_id").IsRequired();
        alertEvent.Property(x => x.RuleName).HasColumnName("rule_name").HasMaxLength(100).IsRequired();
        alertEvent.Property(x => x.Target).HasColumnName("target").HasMaxLength(300).IsRequired();
        alertEvent.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(20).IsRequired();
        alertEvent.Property(x => x.Severity).HasColumnName("severity").HasMaxLength(20).IsRequired();
        alertEvent.Property(x => x.Message).HasColumnName("message").HasColumnType("text").IsRequired();
        alertEvent.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        alertEvent.Property(x => x.Deliveries).HasColumnName("deliveries").HasColumnType("jsonb")
            .HasConversion(items => AlertJson.Serialize(items), json => AlertJson.Deserialize<List<AlertDelivery>>(json));
        alertEvent.HasIndex(x => x.CreatedAt);
    }
}

internal static class AlertJson
{
    public static string Serialize<T>(T value) =>
        System.Text.Json.JsonSerializer.Serialize(value, (System.Text.Json.JsonSerializerOptions?)null);

    public static T Deserialize<T>(string json) where T : new() =>
        System.Text.Json.JsonSerializer.Deserialize<T>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new T();
}