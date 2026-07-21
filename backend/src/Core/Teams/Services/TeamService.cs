using Construcheck.Core.Teams.DTOs;
using Construcheck.Core.Teams.Entities;
using Construcheck.Core.Teams.Interfaces;
using Construcheck.Core.Projects.Interfaces;
using Construcheck.SharedKernel;

namespace Construcheck.Core.Teams.Services;

public class TeamService(ITeamRepository repository, IProjectRepository projectRepository) : ITeamService
{
    public async Task<Result<TeamResponse>> CreateAsync(Guid projectId, CreateTeamRequest request, CancellationToken ct = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, ct);
        if (project is null)
            return Result<TeamResponse>.NotFound("Obra não encontrada.");

        var team = new Team
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = request.Name,
            Specialty = request.Specialty,
            MemberCount = request.MemberCount,
            CreatedAt = DateTime.UtcNow
        };

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

        team.Name = request.Name;
        team.Specialty = request.Specialty;
        team.MemberCount = request.MemberCount;

        await repository.SaveChangesAsync(ct);

        return Result<TeamResponse>.Success(ToResponse(team));
    }

    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var team = await repository.GetByIdAsync(id, ct);
        if (team is null)
            return Result<bool>.NotFound("Equipe não encontrada.");

        await repository.DeleteAsync(team, ct);
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    private static TeamResponse ToResponse(Team team) => new(
        team.Id, team.ProjectId, team.Name, team.Specialty, team.MemberCount);
}