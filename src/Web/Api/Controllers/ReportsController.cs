using Garage.Application.Reporting;
using Garage.Web.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Garage.Web.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("vehicles")]
    public async Task<ActionResult<IReadOnlyList<ReportVehicleOption>>> ListVehiclesAsync(CancellationToken cancellationToken)
    {
        var vehicles = await reports.ListVehiclesAsync(cancellationToken);
        return Ok(vehicles.Select(v => new ReportVehicleOption(v.Id, v.Nickname)).ToList());
    }

    [HttpPost("screen")]
    public async Task<ActionResult<ReportScreen>> GetScreenAsync([FromBody] ReportFilter filter, CancellationToken cancellationToken)
    {
        var screen = await reports.GetScreenAsync(filter, cancellationToken);
        return Ok(screen);
    }

    [HttpPost("export")]
    public async Task<ActionResult<CsvExport>> ExportAsync([FromBody] ReportFilter filter, CancellationToken cancellationToken)
    {
        var export = await reports.ExportAsync(filter, cancellationToken);
        return Ok(export);
    }
}
