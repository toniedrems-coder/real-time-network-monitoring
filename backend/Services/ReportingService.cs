using System.Globalization;
using System.Text;
using backend.Models;

namespace backend.Services;

public sealed class ReportingService
{
    private readonly EndpointService endpointService;
    private readonly MonitoringService monitoringService;
    private readonly AnomalyDetectionService anomalyDetectionService;

    public ReportingService(
        EndpointService endpointService,
        MonitoringService monitoringService,
        AnomalyDetectionService anomalyDetectionService)
    {
        this.endpointService = endpointService;
        this.monitoringService = monitoringService;
        this.anomalyDetectionService = anomalyDetectionService;
    }

    public DashboardView BuildDashboard()
    {
        var summaries = endpointService.GetAll()
            .Select(endpoint =>
            {
                var metrics = monitoringService.GetLatest(endpoint.Id);
                var anomalies = anomalyDetectionService.Detect(
                    endpoint.Id,
                    monitoringService.GetHistory(endpoint.Id));

                return new DashboardEndpointSummary(
                    endpoint.Id,
                    endpoint.Name,
                    endpoint.Url,
                    metrics?.IsReachable == true ? endpoint.Status : endpoint.Status,
                    metrics?.LatencyMs ?? 0,
                    metrics?.AvailabilityPercent ?? 0,
                    anomalies.Count);
            })
            .ToArray();

        return new DashboardView(
            DateTimeOffset.UtcNow,
            summaries.Length,
            summaries.Count(endpoint => endpoint.Status.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase)),
            summaries.Sum(endpoint => endpoint.AnomalyCount),
            summaries);
    }

    public byte[] GenerateCsv(DashboardView dashboard)
    {
        var csv = new StringBuilder()
            .AppendLine("EndpointId,Name,Url,Status,LatencyMs,AvailabilityPercent,AnomalyCount");

        foreach (var endpoint in dashboard.Endpoints)
        {
            csv.AppendLine(string.Join(",",
                endpoint.EndpointId,
                EscapeCsv(endpoint.Name),
                EscapeCsv(endpoint.Url),
                EscapeCsv(endpoint.Status),
                endpoint.LatencyMs.ToString("F2", CultureInfo.InvariantCulture),
                endpoint.AvailabilityPercent.ToString("F2", CultureInfo.InvariantCulture),
                endpoint.AnomalyCount));
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
            $"Healthy: {dashboard.HealthyEndpoints}",
            $"Active anomalies: {dashboard.ActiveAnomalies}",
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