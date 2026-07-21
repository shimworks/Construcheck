using Construcheck.Core.Projects.Enums;

namespace Construcheck.Core.Projects.DTOs;

public record ProjectResponse(
    Guid Id,
    string Name,
    string Address,
    string TechnicalManager,
    DateOnly StartDate,
    DateOnly TargetEndDate,
    ProjectStatus Status);