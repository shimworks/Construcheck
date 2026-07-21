using Construcheck.Core.Schedule.Enums;

namespace Construcheck.Core.Schedule.DTOs;

public record UpdateActivityRequest(
    string Name, DateOnly PlannedStartDate, DateOnly PlannedEndDate,
    DateOnly? ActualStartDate, DateOnly? ActualEndDate, ActivityStatus Status);