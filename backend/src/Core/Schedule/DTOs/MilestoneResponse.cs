namespace Construcheck.Core.Schedule.DTOs;

public record MilestoneResponse(Guid Id, string Name, DateOnly PlannedDate, DateOnly? ActualDate, bool Achieved);