using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly ReportingService reportingService;

    public DashboardController(ReportingService reportingService)
    {
        this.reportingService = reportingService;
    }

    [HttpGet]
    public ActionResult<DashboardView> GetDashboard()
    {
        return Ok(reportingService.BuildDashboard());
    }

    [HttpGet("reports")]
    public IActionResult GetReport([FromQuery] string format = "csv")
    {
        var dashboard = reportingService.BuildDashboard();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

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
            _ => BadRequest("Supported report formats are csv and pdf.")
        };
    }
}
