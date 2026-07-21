namespace Construcheck.Core.Teams.DTOs;

public record TeamResponse(Guid Id, Guid ProjectId, string Name, string? Specialty, int MemberCount);