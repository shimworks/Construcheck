namespace Construcheck.Construction.Domain.Schedule.Data;

public static class WbsTemplateSeed
{
    private static readonly (string Phase, string[] Activities)[] Template =
    [
        ("Fundação", ["Escavação", "Estacas", "Blocos", "Baldrames"]),
        ("Estrutura", ["Pilares", "Vigas", "Lajes"]),
        ("Alvenaria", ["Interna", "Externa"]),
        ("Cobertura", ["Cobertura"]),
        ("Elétrica", ["Elétrica"]),
        ("Hidráulica", ["Hidráulica"]),
        ("Revestimento", ["Revestimento"]),
        ("Pintura", ["Pintura"]),
        ("Acabamentos", ["Acabamentos"]),
        ("Entrega", ["Entrega"]),
    ];

    /// <summary>
    /// Cria as fases e atividades padrão para uma obra nova. Retorna as duas listas
    /// separadas porque SchedulePhase e Activity são Aggregates distintos — cada um
    /// precisa ser persistido através do seu próprio Repository.
    /// </summary>
    public static (List<SchedulePhase> Phases, List<Activity> Activities) CreateDefaultPhasesWithActivities(Guid projectId)
    {
        // Datas nascem iguais a hoje só como placeholder — sem duração conhecida de cada
        // atividade, não dá pra prever datas com sentido. O usuário ajusta depois do seed.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var phases = new List<SchedulePhase>();
        var activities = new List<Activity>();

        for (var phaseIndex = 0; phaseIndex < Template.Length; phaseIndex++)
        {
            var (phaseName, activityNames) = Template[phaseIndex];

            var phase = SchedulePhase.Create(projectId, phaseName, phaseIndex + 1);
            phases.Add(phase);

            for (var activityIndex = 0; activityIndex < activityNames.Length; activityIndex++)
            {
                // Create() valida DateRange internamente; com start == end (placeholder),
                // isso sempre passa, então .Value! é seguro aqui.
                var activityResult = Activity.Create(
                    phase.Id, activityNames[activityIndex], activityIndex + 1, today, today);

                activities.Add(activityResult.Value!);
            }
        }

        return (phases, activities);
    }
}
