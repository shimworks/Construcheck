using Construcheck.Core.Projects.DTOs;
using Construcheck.SharedKernel;

namespace Construcheck.Core.Projects.Interfaces;

public interface IProjectService
{
    Task<Result<ProjectResponse>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<Result<List<ProjectResponse>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<ProjectResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<ProjectResponse>> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);
    Task<Result<bool>> ArchiveAsync(Guid id, CancellationToken ct = default);
}