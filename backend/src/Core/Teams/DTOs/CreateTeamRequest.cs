namespace Construcheck.Core.Teams.DTOs;

public record CreateTeamRequest(string Name, string? Specialty, int MemberCount);