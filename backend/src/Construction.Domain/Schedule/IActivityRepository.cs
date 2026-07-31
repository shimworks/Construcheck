namespace Construcheck.Construction.Domain.Schedule;

public interface IActivityRepository
{
    Task<Activity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Activity>> GetByPhaseIdAsync(Guid schedulePhaseId, CancellationToken ct = default);
    Task<List<Activity>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Retorna todas as Activities que têm o Activity informado como predecessor
    /// (ou seja, que dependem dela). Usado pelo Domain Service de recálculo em cascata.
    /// Restrito a um projeto porque dependências nunca cruzam projetos diferentes —
    /// isso evita varrer a tabela Activities inteira do sistema a cada recálculo.
    /// </summary>
    Task<List<Activity>> GetByPredecessorIdAsync(Guid projectId, Guid predecessorActivityId, CancellationToken ct = default);

    Task AddAsync(Activity activity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
