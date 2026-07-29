using Construcheck.Construction.Application.Projects.DTOs;
using Construcheck.Construction.Application.Projects.Interfaces;
using Construcheck.Construction.Domain.Projects;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Application.Projects.Services;

public class ProjectApplicationService(IProjectRepository repository) : IProjectApplicationService
{
    public async Task<Result<ProjectResponse>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var projectResult = Project.Create(
            request.Name, request.Address, request.TechnicalManager,
            request.StartDate, request.TargetEndDate);

        if (projectResult.IsFailure)
            return Result<ProjectResponse>.Validation(projectResult.Error);

        var project = projectResult.Value!;

        await repository.AddAsync(project, ct);
        await repository.SaveChangesAsync(ct);

        return Result<ProjectResponse>.Success(ToResponse(project));
    }

    public async Task<Result<List<ProjectResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var projects = await repository.GetAllAsync(ct);
        return Result<List<ProjectResponse>>.Success(projects.Select(ToResponse).ToList());
    }

    public async Task<Result<ProjectResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await repository.GetByIdAsync(id, ct);
        return project is null
            ? Result<ProjectResponse>.NotFound("Obra não encontrada.")
            : Result<ProjectResponse>.Success(ToResponse(project));
    }

    public async Task<Result<ProjectResponse>> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await repository.GetByIdAsync(id, ct);
        if (project is null)
            return Result<ProjectResponse>.NotFound("Obra não encontrada.");

        var updateResult = project.UpdateDetails(
            request.Name, request.Address, request.TechnicalManager,
            request.StartDate, request.TargetEndDate);

        if (updateResult.IsFailure)
            return Result<ProjectResponse>.Validation(updateResult.Error);

        await repository.SaveChangesAsync(ct);

        return Result<ProjectResponse>.Success(ToResponse(project));
    }

    public async Task<Result<bool>> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var project = await repository.GetByIdAsync(id, ct);
        if (project is null)
            return Result<bool>.NotFound("Obra não encontrada.");

        project.Archive();
        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    private static ProjectResponse ToResponse(Project project) => new(
        project.Id, project.Name, project.Address, project.TechnicalManager,
        project.Schedule.Start, project.Schedule.End, project.Status);
}
