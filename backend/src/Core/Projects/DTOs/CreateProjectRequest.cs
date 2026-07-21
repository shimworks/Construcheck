namespace Construcheck.Core.Projects.DTOs;

public record CreateProjectRequest(
    string Name,
    string Address,
    string TechnicalManager,
    DateOnly StartDate,
    DateOnly TargetEndDate);