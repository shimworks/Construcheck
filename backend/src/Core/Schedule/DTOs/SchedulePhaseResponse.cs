namespace Construcheck.Core.Schedule.DTOs;

public record SchedulePhaseResponse(Guid Id, string Name, int Order, List<ActivityResponse> Activities);
