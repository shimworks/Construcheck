using Construcheck.Construction.Domain.Schedule;
using Construcheck.Construction.Domain.Schedule.Data;

namespace Construcheck.Unit.Tests.Construction.Domain.Schedule.Data;

public class WbsTemplateSeedTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // CreateDefaultPhasesWithActivities
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateDefaultPhasesWithActivities_ReturnsExactlyTenPhases()
    {
        // Arrange — o template fixo em Template[] tem 10 entradas; qualquer
        // mudança nesse array deve quebrar este teste, tornando a mudança consciente

        // Act
        var (phases, _) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(ProjectId);

        // Assert
        Assert.Equal(10, phases.Count);
    }

    [Fact]
    public void CreateDefaultPhasesWithActivities_PhasesAreOrderedSequentiallyStartingAtOne()
    {
        // Act
        var (phases, _) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(ProjectId);

        // Assert
        for (var i = 0; i < phases.Count; i++)
            Assert.Equal(i + 1, phases[i].Order);
    }

    [Fact]
    public void CreateDefaultPhasesWithActivities_AllPhasesBelongToTheGivenProject()
    {
        // Act
        var (phases, activities) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(ProjectId);

        // Assert
        Assert.All(phases, p => Assert.Equal(ProjectId, p.ProjectId));
        Assert.All(activities, a => Assert.Equal(ProjectId, a.ProjectId));
    }

    [Fact]
    public void CreateDefaultPhasesWithActivities_AllPhasesStartAsNotStartedAndActive()
    {
        // Act
        var (phases, _) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(ProjectId);

        // Assert
        Assert.All(phases, p => Assert.Equal(PhaseStatus.NotStarted, p.Status));
        Assert.All(phases, p => Assert.Equal(SchedulePhaseDeletionStatus.Active, p.DeletionStatus));
    }

    [Fact]
    public void CreateDefaultPhasesWithActivities_FirstPhaseIsFundacaoWithFourActivities()
    {
        // Arrange — verifica o conteúdo real do template, não apenas a contagem total.
        // "Fundação" é a primeira fase e tem 4 atividades: Escavação, Estacas, Blocos, Baldrames.

        // Act
        var (phases, activities) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(ProjectId);

        // Assert
        var fundacao = phases[0];
        Assert.Equal("Fundação", fundacao.Name);

        var fundacaoActivities = activities.Where(a => a.SchedulePhaseId == fundacao.Id).OrderBy(a => a.Order).ToList();
        Assert.Equal(4, fundacaoActivities.Count);
        Assert.Equal(["Escavação", "Estacas", "Blocos", "Baldrames"], fundacaoActivities.Select(a => a.Name));
    }

    [Fact]
    public void CreateDefaultPhasesWithActivities_LastPhaseIsEntregaWithSingleActivity()
    {
        // Act
        var (phases, activities) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(ProjectId);

        // Assert
        var entrega = phases[^1];
        Assert.Equal("Entrega", entrega.Name);
        Assert.Equal(10, entrega.Order);

        var entregaActivities = activities.Where(a => a.SchedulePhaseId == entrega.Id).ToList();
        Assert.Single(entregaActivities);
        Assert.Equal("Entrega", entregaActivities[0].Name);
    }

    [Fact]
    public void CreateDefaultPhasesWithActivities_ActivitiesWithinEachPhaseAreOrderedSequentiallyStartingAtOne()
    {
        // Act
        var (phases, activities) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(ProjectId);

        // Assert
        foreach (var phase in phases)
        {
            var phaseActivities = activities.Where(a => a.SchedulePhaseId == phase.Id).OrderBy(a => a.Order).ToList();
            for (var i = 0; i < phaseActivities.Count; i++)
                Assert.Equal(i + 1, phaseActivities[i].Order);
        }
    }

    [Fact]
    public void CreateDefaultPhasesWithActivities_AllActivitiesStartAsNotStartedAndActive()
    {
        // Act
        var (_, activities) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(ProjectId);

        // Assert
        Assert.All(activities, a => Assert.Equal(ActivityStatus.NotStarted, a.Status));
        Assert.All(activities, a => Assert.Equal(ActivityDeletionStatus.Active, a.DeletionStatus));
    }

    [Fact]
    public void CreateDefaultPhasesWithActivities_AllActivitiesUsePlaceholderDateEqualToToday()
    {
        // Arrange — comentário no código-fonte é explícito: "start == end (placeholder)"
        // usa a data de hoje para ambos os limites, já que a duração real é desconhecida
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var (_, activities) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(ProjectId);

        // Assert
        Assert.All(activities, a => Assert.Equal(today, a.PlannedPeriod.Start));
        Assert.All(activities, a => Assert.Equal(today, a.PlannedPeriod.End));
    }

    [Fact]
    public void CreateDefaultPhasesWithActivities_TotalActivityCountMatchesSumAcrossAllPhases()
    {
        // Arrange — soma manual das atividades no template fixo:
        // Fundação(4) + Estrutura(3) + Alvenaria(2) + Cobertura(1) + Elétrica(1) +
        // Hidráulica(1) + Revestimento(1) + Pintura(1) + Acabamentos(1) + Entrega(1) = 16
        const int expectedTotal = 16;

        // Act
        var (_, activities) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(ProjectId);

        // Assert
        Assert.Equal(expectedTotal, activities.Count);
    }

    [Fact]
    public void CreateDefaultPhasesWithActivities_EachActivityReferencesItsOwnPhaseId()
    {
        // Arrange — garante que cada Activity aponta para o Id da SchedulePhase correta
        // (não uma indexação errada entre as duas listas retornadas separadamente)

        // Act
        var (phases, activities) = WbsTemplateSeed.CreateDefaultPhasesWithActivities(ProjectId);
        var validPhaseIds = phases.Select(p => p.Id).ToHashSet();

        // Assert
        Assert.All(activities, a => Assert.Contains(a.SchedulePhaseId, validPhaseIds));
    }
}
