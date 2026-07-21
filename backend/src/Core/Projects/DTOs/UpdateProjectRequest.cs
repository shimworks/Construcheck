namespace Construcheck.Core.Projects.DTOs;

public record UpdateProjectRequest(
    string Name,
    string Address,
    string TechnicalManager,
    DateOnly StartDate,
    DateOnly TargetEndDate);