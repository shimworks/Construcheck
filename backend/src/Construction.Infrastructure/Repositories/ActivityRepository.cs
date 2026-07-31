using Construcheck.Construction.Domain.Schedule;
using Construcheck.Construction.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Construcheck.Construction.Infrastructure.Repositories;

public class ActivityRepository(ConstructionDbContext db) : IActivityRepository
{
    public Task<Activity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Activities.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<Activity>> GetByPhaseIdAsync(Guid schedulePhaseId, CancellationToken ct = default) =>
        db.Activities.Where(a => a.SchedulePhaseId == schedulePhaseId).ToListAsync(ct);

    public Task<List<Activity>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) =>
        db.Activities.Where(a => ids.Contains(a.Id)).ToListAsync(ct);

    public async Task<List<Activity>> GetByPredecessorIdAsync(Guid projectId, Guid predecessorActivityId, CancellationToken ct = default)
    {
        // PredecessorIds é uma coluna JSON (List<Guid>) — Contains sobre coleção JSON
        // não é traduzível para SQL de forma confiável em todas as versões do EF Core,
        // então o filtro final acontece em memória. O filtro por ProjectId acontece
        // no SQL primeiro, restringindo as candidatas às Activities do mesmo projeto
        // (dependências nunca cruzam projetos), o que evita varrer a tabela inteira.
        var candidateActivities = await db.Activities
            .Where(a => a.ProjectId == projectId && a.DeletionStatus == ActivityDeletionStatus.Active)
            .ToListAsync(ct);

        return candidateActivities
            .Where(a => a.PredecessorIds.Contains(predecessorActivityId))
            .ToList();
    }

    public async Task AddAsync(Activity activity, CancellationToken ct = default) =>
        await db.Activities.AddAsync(activity, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
