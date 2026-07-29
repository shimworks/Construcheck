using Construcheck.Construction.Domain.Schedule;

namespace Construcheck.Construction.Application.Schedule.DTOs;

public record SchedulePhaseResponse(
    Guid Id, string Name, int Order, PhaseStatus Status, List<ActivityResponse> Activities);
