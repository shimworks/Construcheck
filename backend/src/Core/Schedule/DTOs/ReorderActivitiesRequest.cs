namespace Construcheck.Core.Schedule.DTOs;

public record ReorderActivitiesRequest(List<Guid> ActivityIdsInOrder);