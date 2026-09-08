using Garage.Domain;
using Garage.Domain.Repositories;
using Garage.Domain.Services;

namespace Garage.Application.Reporting;

/// <summary>Story R-1's range control [1m].</summary>
public enum ReportRange
{
    YearToDate = 0,
    TwelveMonths = 1,
    All = 2
}

/// <summary>Story R-2's sortable columns [1m].</summary>
public enum HistorySort
{
    Date = 0,
    Vehicle = 1,
    Odometer = 2,
    Category = 3,
    Item = 4,
    Shop = 5,
    Cost = 6
}

/// <summary>
/// What the whole screen is filtered by. One filter drives the dashboard, the table and
/// the comparison, so no two figures on screen can describe different periods.
/// </summary>
public class ReportFilter
{
    /// <summary>Null means every vehicle in the household.</summary>
    public Guid? VehicleId { get; set; }

    public ReportRange Range { get; set; } = ReportRange.TwelveMonths;

    /// <summary>Story R-2's extra category filter, which the dashboard ignores.</summary>
    public string? Category { get; set; }

    public HistorySort Sort { get; set; } = HistorySort.Date;

    public bool Descending { get; set; } = true;
}

/// <summary>Everything the reports screen renders for a given filter.</summary>
public record ReportScreen(
    CostDashboard Dashboard,
    IReadOnlyList<CostLine> History,
    IReadOnlyList<VehicleComparison> Comparison,
    IReadOnlyList<string> Categories,
    DateOnly From,
    DateOnly To,
    string RangeDescription);

/// <summary>Story R-4: the export states its own row count [1m].</summary>
public record CsvExport(string FileName, string Content, int RowCount);
