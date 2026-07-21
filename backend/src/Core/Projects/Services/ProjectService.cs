using Construcheck.Core.Projects.DTOs;
using Construcheck.Core.Projects.Entities;
using Construcheck.Core.Projects.Enums;
using Construcheck.Core.Projects.Interfaces;
using Construcheck.SharedKernel;

namespace Construcheck.Core.Projects.Services;

public class ProjectService(IProjectRepository repository) : IProjectService
{
    public async Task<Result<ProjectResponse>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Address = request.Address,
            TechnicalManager = request.TechnicalManager,
            StartDate = request.StartDate,
            TargetEndDate = request.TargetEndDate,
            Status = ProjectStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

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
            ? Result<ProjectResponse>.NotFound("Project não encontrada.")
            : Result<ProjectResponse>.Success(ToResponse(project));
    }

    public async Task<Result<ProjectResponse>> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await repository.GetByIdAsync(id, ct);
        if (project is null)
            return Result<ProjectResponse>.NotFound("Project não encontrada.");

        project.Name = request.Name;
        project.Address = request.Address;
        project.TechnicalManager = request.TechnicalManager;
        project.StartDate = request.StartDate;
        project.TargetEndDate = request.TargetEndDate;
        project.UpdatedAt = DateTime.UtcNow;

        await repository.SaveChangesAsync(ct);

        return Result<ProjectResponse>.Success(ToResponse(project));
    }

    public async Task<Result<bool>> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var project = await repository.GetByIdAsync(id, ct);
        if (project is null)
            return Result<bool>.NotFound("Project não encontrada.");

        project.Status = ProjectStatus.Archived;
        project.UpdatedAt = DateTime.UtcNow;

        await repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }

    private static ProjectResponse ToResponse(Project project) => new(
        project.Id, project.Name, project.Address, project.TechnicalManager,
        project.StartDate, project.TargetEndDate, project.Status);
}