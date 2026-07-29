using Construcheck.Construction.Application.Projects.DTOs;
using Construcheck.SharedKernel;

namespace Construcheck.Construction.Application.Projects.Interfaces;

public interface IProjectApplicationService
{
    Task<Result<ProjectResponse>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<Result<List<ProjectResponse>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<ProjectResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<ProjectResponse>> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);
    Task<Result<bool>> ArchiveAsync(Guid id, CancellationToken ct = default);
}
