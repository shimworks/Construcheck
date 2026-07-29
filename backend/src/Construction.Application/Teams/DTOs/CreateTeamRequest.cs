namespace Construcheck.Construction.Application.Teams.DTOs;

public record CreateTeamRequest(string Name, string? Specialty, int MemberCount);
