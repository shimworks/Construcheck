namespace Construcheck.Core.Schedule.DTOs;

public record CreateActivityRequest(string Name, DateOnly PlannedStartDate, DateOnly PlannedEndDate);