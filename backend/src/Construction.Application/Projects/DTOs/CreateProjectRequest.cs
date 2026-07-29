namespace Construcheck.Construction.Application.Projects.DTOs;

public record CreateProjectRequest(
    string Name,
    string Address,
    string TechnicalManager,
    DateOnly StartDate,
    DateOnly TargetEndDate);
