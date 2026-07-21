namespace Construcheck.Core.Schedule.Entities;

public class Milestone
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly PlannedDate { get; set; }
    public DateOnly? ActualDate { get; set; }
    public bool Achieved { get; set; }
}