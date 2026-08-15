using Garage.Domain;

namespace Garage.Application.ServiceLogging;

/// <summary>A job people log often enough to be worth offering rather than typing [1f].</summary>
public record CommonJob(string Name, ServiceCategory Category);

public static class CommonJobs
{
    /// <summary>
    /// The "common" list of wireframe 1f, widened a little. Each carries the category
    /// its spending belongs under, so the reports breakdown does not depend on the user
    /// classifying their own oil change.
    /// </summary>
    public static readonly IReadOnlyList<CommonJob> All =
    [
        new("Oil & filter", ServiceCategory.ScheduledService),
        new("Tire rotation", ServiceCategory.ScheduledService),
        new("Tires ×4", ServiceCategory.Tires),
        new("Brake pads", ServiceCategory.Repair),
        new("Brake fluid", ServiceCategory.ScheduledService),
        new("Battery", ServiceCategory.Repair),
        new("Wipers", ServiceCategory.ScheduledService),
        new("Air filter", ServiceCategory.ScheduledService),
        new("Cabin air filter", ServiceCategory.ScheduledService),
        new("Spark plugs", ServiceCategory.ScheduledService),
        new("Coolant flush", ServiceCategory.ScheduledService),
        new("Transmission fluid", ServiceCategory.ScheduledService),
        new("Alignment", ServiceCategory.ScheduledService),
        new("State inspection", ServiceCategory.Inspection),
        new("Alternator", ServiceCategory.Repair),
        new("Starter", ServiceCategory.Repair),
        new("Timing belt", ServiceCategory.ScheduledService)
    ];

    public static IEnumerable<CommonJob> Search(string? term) =>
        string.IsNullOrWhiteSpace(term)
            ? All
            : All.Where(j => j.Name.Contains(term.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Best category for a set of job names. Repairs and tyres outrank scheduled work,
    /// because a visit that replaced an alternator is a repair even if the oil was done too.
    /// </summary>
    public static ServiceCategory CategoryFor(IEnumerable<string> jobNames)
    {
        var categories = jobNames
            .Select(name => All.FirstOrDefault(j => string.Equals(j.Name, name, StringComparison.OrdinalIgnoreCase)))
            .Where(job => job is not null)
            .Select(job => job!.Category)
            .ToList();

        if (categories.Count == 0)
        {
            return ServiceCategory.Other;
        }

        foreach (var priority in new[] { ServiceCategory.Repair, ServiceCategory.Tires, ServiceCategory.Inspection })
        {
            if (categories.Contains(priority))
            {
                return priority;
            }
        }

        return categories.Contains(ServiceCategory.ScheduledService)
            ? ServiceCategory.ScheduledService
            : ServiceCategory.Other;
    }
}
