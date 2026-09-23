using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly ReportingService reportingService;
    private readonly IKpiSnapshotRepository kpiSnapshotRepository;

    public DashboardController(
        ReportingService reportingService,
        IKpiSnapshotRepository kpiSnapshotRepository)
    {
        this.reportingService = reportingService;
        this.kpiSnapshotRepository = kpiSnapshotRepository;
    }

    /// <summary>
    /// Returns the current dashboard state.
    /// </summary>
    [HttpGet]
    public ActionResult<DashboardView> GetDashboard()
    {
        return Ok(reportingService.BuildDashboard());
    }

    /// <summary>
    /// Returns durable KPI trend history from PostgreSQL.
    /// </summary>
    [HttpGet("kpis/history")]
    public async Task<ActionResult<IReadOnlyList<KpiSnapshot>>> GetKpiHistory(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (from.HasValue &&
            to.HasValue &&
            from.Value > to.Value)
        {
            return BadRequest(
                "'from' must be earlier than or equal to 'to'.");
        }

        var history =
            await kpiSnapshotRepository.GetHistoryAsync(
                from,
                to,
                cancellationToken);

        return Ok(history);
    }

    /// <summary>
    /// Generates downloadable dashboard reports.
    /// </summary>
    [HttpGet("reports")]
    public IActionResult GetReport(
        [FromQuery] string format = "csv")
    {
        var dashboard =
            reportingService.BuildDashboard();

        var timestamp =
            DateTime.UtcNow.ToString("yyyyMMddHHmmss");

        return format.ToLowerInvariant() switch
        {
            "csv" => File(
                reportingService.GenerateCsv(dashboard),
                "text/csv",
                $"network-monitor-report-{timestamp}.csv"),

            "pdf" => File(
                reportingService.GeneratePdf(dashboard),
                "application/pdf",
                $"network-monitor-report-{timestamp}.pdf"),

            _ => BadRequest(
                "Supported report formats are csv and pdf.")
        };
    }
}