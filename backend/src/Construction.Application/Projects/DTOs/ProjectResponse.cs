using Construcheck.Construction.Domain.Projects;

namespace Construcheck.Construction.Application.Projects.DTOs;

public record ProjectResponse(
    Guid Id,
    string Name,
    string Address,
    string TechnicalManager,
    DateOnly StartDate,
    DateOnly TargetEndDate,
    ProjectStatus Status);
