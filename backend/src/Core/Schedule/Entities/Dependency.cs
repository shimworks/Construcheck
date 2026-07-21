namespace Construcheck.Core.Schedule.Entities;

public class Dependency
{
    public Guid Id { get; set; }
    public Guid ActivityId { get; set; }
    public Guid PredecessorActivityId { get; set; }

    public Activity? Activity { get; set; }
    public Activity? PredecessorActivity { get; set; }
}