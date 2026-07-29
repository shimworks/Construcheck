using Construcheck.Construction.Domain.SharedValueObjects;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.Projects;

public class Project
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string TechnicalManager { get; private set; } = string.Empty;
    public DateRange Schedule { get; private set; } = null!;
    public ProjectStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Project() { }

    public static Result<Project> Create(
        string name, string address, string technicalManager,
        DateOnly startDate, DateOnly targetEndDate)
    {
        var scheduleResult = DateRange.Create(startDate, targetEndDate);
        if (scheduleResult.IsFailure)
            return Result<Project>.Validation(scheduleResult.Error);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            Address = address,
            TechnicalManager = technicalManager,
            Schedule = scheduleResult.Value!,
            Status = ProjectStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return Result<Project>.Success(project);
    }

    public static Project Reconstitute(
        Guid id, string name, string address, string technicalManager,
        DateOnly startDate, DateOnly targetEndDate, ProjectStatus status,
        DateTime createdAt, DateTime updatedAt) => new()
    {
        Id = id,
        Name = name,
        Address = address,
        TechnicalManager = technicalManager,
        Schedule = DateRange.FromExistingValues(startDate, targetEndDate),
        Status = status,
        CreatedAt = createdAt,
        UpdatedAt = updatedAt
    };

    public Result<bool> UpdateDetails(
        string name, string address, string technicalManager,
        DateOnly startDate, DateOnly targetEndDate)
    {
        var scheduleResult = DateRange.Create(startDate, targetEndDate);
        if (scheduleResult.IsFailure)
            return Result<bool>.Validation(scheduleResult.Error);

        Name = name;
        Address = address;
        TechnicalManager = technicalManager;
        Schedule = scheduleResult.Value!;
        UpdatedAt = DateTime.UtcNow;

        return Result<bool>.Success(true);
    }

    public void Archive()
    {
        Status = ProjectStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }
}
