namespace Construcheck.Construction.Domain.Schedule;

public interface ISchedulePhaseRepository
{
    Task<SchedulePhase?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<SchedulePhase>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);

    /// <summary>
    /// Retorna a fase imediatamente anterior (Order - 1) dentro do mesmo projeto,
    /// ou null se a fase informada for a primeira (Order == 1 ou o menor Order existente).
    /// </summary>
    Task<SchedulePhase?> GetPreviousPhaseAsync(Guid projectId, int currentOrder, CancellationToken ct = default);

    Task AddAsync(SchedulePhase phase, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<SchedulePhase> phases, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
