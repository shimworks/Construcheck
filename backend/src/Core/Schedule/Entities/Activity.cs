using Construcheck.Core.Schedule.Enums;

namespace Construcheck.Core.Schedule.Entities;

public class Activity
{
    public Guid Id { get; set; }
    public Guid SchedulePhaseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
    public ActivityStatus Status { get; set; } = ActivityStatus.NotStarted;

    public SchedulePhase? SchedulePhase { get; set; }
    public ICollection<Dependency> Dependencies { get; set; } = [];
}