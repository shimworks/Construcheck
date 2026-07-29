using Construcheck.Construction.Application.Teams.DTOs;
using Construcheck.Construction.Application.Teams.Interfaces;
using Construcheck.Construction.Domain.Projects;
using Construcheck.Construction.Domain.Teams;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Application.Teams.Services;

public class TeamApplicationService(ITeamRepository repository, IProjectRepository projectRepository) : ITeamApplicationService
{
    public async Task<Result<TeamResponse>> CreateAsync(Guid projectId, CreateTeamRequest request, CancellationToken ct = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
            return Result<TeamResponse>.NotFound("Obra não encontrada.");

        var teamResult = Team.Create(projectId, request.Name, request.Specialty, request.MemberCount);
        if (teamResult.IsFailure)
            return Result<TeamResponse>.Validation(teamResult.Error);

        var team = teamResult.Value!;

        await repository.AddAsync(team, ct);
        await repository.SaveChangesAsync(ct);

        return Result<TeamResponse>.Success(ToResponse(team));
    }

    public async Task<Result<List<TeamResponse>>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
    {
        var teams = await repository.GetByProjectIdAsync(projectId, ct);
        return Result<List<TeamResponse>>.Success(teams.Select(ToResponse).ToList());
    }

    public async Task<Result<TeamResponse>> UpdateAsync(Guid id, UpdateTeamRequest request, CancellationToken ct = default)
    {
        var team = await repository.GetByIdAsync(id, ct);
        if (team is null)
            return Result<TeamResponse>.NotFound("Equipe não encontrada.");

        var updateResult = team.UpdateDetails(request.Name, request.Specialty, request.MemberCount);
        if (updateResult.IsFailure)
            return Result<TeamResponse>.Validation(updateResult.Error);

        await repository.SaveChangesAsync(ct);

        return Result<TeamResponse>.Success(ToResponse(team));
    }

    public async Task<Result<bool>> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var team = await repository.GetByIdAsync(id, ct);
        if (team is null)
            return Result<bool>.NotFound("Equipe não encontrada.");

        team.Remove();
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    private static TeamResponse ToResponse(Team team) => new(
        team.Id, team.ProjectId, team.Name, team.Specialty, team.MemberCount);
}
