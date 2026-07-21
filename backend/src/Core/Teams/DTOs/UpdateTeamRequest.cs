namespace Construcheck.Core.Teams.DTOs;

public record UpdateTeamRequest(string Name, string? Specialty, int MemberCount);