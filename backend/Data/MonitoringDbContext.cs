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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureMonitoredEndpoint(modelBuilder);
        ConfigureEndpointMetrics(modelBuilder);
        ConfigureDetectedAnomaly(modelBuilder);
        ConfigureKpiSnapshot(modelBuilder);
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
}