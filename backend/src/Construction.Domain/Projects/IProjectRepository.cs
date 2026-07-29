namespace Construcheck.Construction.Domain.Projects;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Project>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Project project, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
