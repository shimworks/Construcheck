namespace Construcheck.Construction.Domain.Schedule;

/// <summary>
/// Resultado de Activity.Complete(). Carrega o fato de "houve atraso?" já resolvido,
/// para que o Application Service nunca precise recalcular isso por fora comparando
/// datas de novo — evita duas leituras separadas de "agora" representando o mesmo instante.
/// </summary>
public record ActivityCompletionOutcome(bool WasLate, DateOnly CompletionDate);
