namespace Construcheck.Core.Schedule.Entities;

public class SchedulePhase
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }

    public ICollection<Activity> Activities { get; set; } = [];
}