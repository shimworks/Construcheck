namespace Construcheck.Core.Schedule.DTOs;

public record ScheduleResponse(List<SchedulePhaseResponse> Phases, List<MilestoneResponse> Milestones);