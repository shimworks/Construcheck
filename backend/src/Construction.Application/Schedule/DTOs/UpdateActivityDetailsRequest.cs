namespace Construcheck.Construction.Application.Schedule.DTOs;

public record UpdateActivityDetailsRequest(string Name, DateOnly PlannedStartDate, DateOnly PlannedEndDate);
