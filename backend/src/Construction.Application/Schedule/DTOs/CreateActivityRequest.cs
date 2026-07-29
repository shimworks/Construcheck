namespace Construcheck.Construction.Application.Schedule.DTOs;

public record CreateActivityRequest(string Name, DateOnly PlannedStartDate, DateOnly PlannedEndDate);
