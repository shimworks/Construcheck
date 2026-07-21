using Construcheck.Core.Schedule.Entities;
using Construcheck.Core.Schedule.Enums;

namespace Construcheck.Core.Schedule.Data;

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

    public static List<SchedulePhase> CreateDefaultPhases(Guid projectId)
    {
        // Datas nascem iguais a hoje só como placeholder — sem duração conhecida de cada
        // atividade, não dá pra prever datas com sentido. O usuário ajusta depois do seed.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return Template.Select((phaseTemplate, index) =>
        {
            var phase = new SchedulePhase
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = phaseTemplate.Phase,
                Order = index + 1
            };

            phase.Activities = phaseTemplate.Activities.Select(name => new Activity
            {
                Id = Guid.NewGuid(),
                SchedulePhaseId = phase.Id,
                Name = name,
                PlannedStartDate = today,
                PlannedEndDate = today,
                Status = ActivityStatus.NotStarted
            }).ToList();

            return phase;
        }).ToList();
    }
}