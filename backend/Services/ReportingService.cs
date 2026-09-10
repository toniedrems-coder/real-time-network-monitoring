using System.Globalization;
using System.Text;
using backend.Models;

namespace backend.Services;

public sealed class ReportingService
{
    private readonly EndpointService endpointService;
    private readonly MetricsStore metricsStore;
    private readonly AnomalyStore anomalyStore;
    private readonly KpiHistoryStore kpiHistoryStore;

    public ReportingService(
        EndpointService endpointService,
        MetricsStore metricsStore,
        AnomalyStore anomalyStore,
        KpiHistoryStore kpiHistoryStore)
    {
        this.endpointService = endpointService;
        this.metricsStore = metricsStore;
        this.anomalyStore = anomalyStore;
        this.kpiHistoryStore = kpiHistoryStore;
    }

    public DashboardView BuildDashboard()
    {
        var summaries = endpointService.GetAll()
            .Select(endpoint =>
            {
                var metrics = metricsStore.GetLatest(endpoint.Id);
                var anomalies = anomalyStore.GetForEndpoint(endpoint.Id);

                return new DashboardEndpointSummary(
                    endpoint.Id,
                    endpoint.Name,
                    endpoint.Url,
                    metrics?.IsReachable == true ? endpoint.Status : endpoint.Status,
                    metrics?.LatencyMs ?? 0,
                    metrics?.AvailabilityPercent ?? 0,
                    anomalies.Count,
                    anomalies.Count > 0 ? anomalies.Max(anomaly => anomaly.DetectedAt) : null);
            })
            .ToArray();

        var latencies = summaries.Where(endpoint => endpoint.LatencyMs > 0)
            .Select(endpoint => endpoint.LatencyMs)
            .OrderBy(latency => latency)
            .ToArray();

        var healthyCount = summaries.Count(endpoint => endpoint.Status.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase));
        var healthyPercent = summaries.Length == 0 ? 100 : Math.Round(healthyCount * 100d / summaries.Length, 2);
        var averageLatency = latencies.Length == 0 ? 0 : Math.Round(latencies.Average(), 2);
        var p95Latency = latencies.Length == 0 ? 0 : Math.Round(Percentile(latencies, 0.95), 2);

        var recentAnomalies = anomalyStore.GetAll()
            .Where(anomaly => anomaly.DetectedAt >= DateTimeOffset.UtcNow.AddHours(-1))
            .ToArray();
        var anomalyRatePerHour = recentAnomalies.Length;

        var endpointsAtRisk = summaries.Count(endpoint =>
            !endpoint.Status.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase) ||
            endpoint.AnomalyCount > 0);

        return new DashboardView(
            DateTimeOffset.UtcNow,
            summaries.Length,
            healthyCount,
            summaries.Sum(endpoint => endpoint.AnomalyCount),
            healthyPercent,
            averageLatency,
            p95Latency,
            anomalyRatePerHour,
            endpointsAtRisk,
            summaries,
            kpiHistoryStore.GetAll());
    }

    /// <summary>Builds the KPI portion of the dashboard without the per-endpoint or history detail, for the periodic snapshot worker.</summary>
    public KpiSnapshot BuildKpiSnapshot()
    {
        var dashboard = BuildDashboard();
        return new KpiSnapshot(
            dashboard.GeneratedAt,
            dashboard.TotalEndpoints,
            dashboard.HealthyEndpoints,
            dashboard.HealthyPercent,
            dashboard.AverageLatencyMs,
            dashboard.P95LatencyMs,
            dashboard.ActiveAnomalies,
            dashboard.AnomalyRatePerHour,
            dashboard.EndpointsAtRisk);
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var rank = percentile * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);
        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var weight = rank - lowerIndex;
        return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight;
    }

    public byte[] GenerateCsv(DashboardView dashboard)
    {
        var csv = new StringBuilder()
            .AppendLine("EndpointId,Name,Url,Status,LatencyMs,AvailabilityPercent,AnomalyCount,LastAnomalyAt");

        foreach (var endpoint in dashboard.Endpoints)
        {
            csv.AppendLine(string.Join(",",
                endpoint.EndpointId,
                EscapeCsv(endpoint.Name),
                EscapeCsv(endpoint.Url),
                EscapeCsv(endpoint.Status),
                endpoint.LatencyMs.ToString("F2", CultureInfo.InvariantCulture),
                endpoint.AvailabilityPercent.ToString("F2", CultureInfo.InvariantCulture),
                endpoint.AnomalyCount,
                endpoint.LastAnomalyAt?.ToString("O", CultureInfo.InvariantCulture) ?? ""));
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public byte[] GeneratePdf(DashboardView dashboard)
    {
        var lines = new List<string>
        {
            "Network Monitor Report",
            $"Generated: {dashboard.GeneratedAt:O}",
            $"Endpoints: {dashboard.TotalEndpoints}",
            $"Healthy: {dashboard.HealthyEndpoints} ({dashboard.HealthyPercent:F1}%)",
            $"Active anomalies: {dashboard.ActiveAnomalies} ({dashboard.AnomalyRatePerHour:F0}/hr)",
            $"Average latency: {dashboard.AverageLatencyMs:F2} ms (p95: {dashboard.P95LatencyMs:F2} ms)",
            $"Endpoints at risk: {dashboard.EndpointsAtRisk}",
            string.Empty
        };

        lines.AddRange(dashboard.Endpoints.Select(endpoint =>
            $"{endpoint.Name} | {endpoint.Status} | {endpoint.LatencyMs:F2} ms | {endpoint.AvailabilityPercent:F2}% | anomalies: {endpoint.AnomalyCount}"));

        return CreatePdf(lines);
    }

    private static string EscapeCsv(string value)
    {
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static byte[] CreatePdf(IReadOnlyList<string> lines)
    {
        var content = new StringBuilder("BT\n/F1 10 Tf\n50 780 Td\n");
        foreach (var line in lines)
        {
            content.Append('(')
                .Append(line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)"))
                .AppendLine(") Tj")
                .AppendLine("0 -16 Td");
        }

        content.Append("ET");
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content.ToString())} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var (value, index) in objects.Select((value, index) => (value, index)))
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append($"{index + 1} 0 obj\n{value}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            pdf.Append($"{offset:D10} 00000 n \n");
        }

        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}