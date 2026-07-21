using Construcheck.Core.Schedule.Enums;

namespace Construcheck.Core.Schedule.DTOs;

public record ActivityResponse(
    Guid Id, Guid SchedulePhaseId, string Name,
    DateOnly PlannedStartDate, DateOnly PlannedEndDate,
    DateOnly? ActualStartDate, DateOnly? ActualEndDate, ActivityStatus Status,
    List<Guid> DependsOn);