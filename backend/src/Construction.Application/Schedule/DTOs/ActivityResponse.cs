using Construcheck.Construction.Domain.Schedule;

namespace Construcheck.Construction.Application.Schedule.DTOs;

public record ActivityResponse(
    Guid Id, Guid SchedulePhaseId, string Name,
    DateOnly PlannedStartDate, DateOnly PlannedEndDate,
    DateOnly? ActualStartDate, DateOnly? ActualEndDate, ActivityStatus Status,
    List<Guid> PredecessorIds);
