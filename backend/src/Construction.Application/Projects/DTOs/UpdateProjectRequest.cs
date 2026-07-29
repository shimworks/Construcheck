namespace Construcheck.Construction.Application.Projects.DTOs;

public record UpdateProjectRequest(
    string Name,
    string Address,
    string TechnicalManager,
    DateOnly StartDate,
    DateOnly TargetEndDate);
