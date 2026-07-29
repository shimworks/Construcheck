using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.SharedValueObjects;

public sealed class Money
{
    public decimal Amount { get; }

    private Money(decimal amount)
    {
        Amount = amount;
    }

    /// <summary>
    /// Cria um Money que deve ser estritamente positivo (ex: valor de contrato).
    /// </summary>
    public static Result<Money> CreatePositive(decimal amount)
    {
        if (amount <= 0)
            return Result<Money>.Validation("O valor deve ser maior que zero.");

        return Result<Money>.Success(new Money(amount));
    }

    /// <summary>
    /// Cria um Money que permite zero, mas não negativo (ex: preço unitário de item de orçamento,
    /// onde zero pode representar item de cortesia/doação).
    /// </summary>
    public static Result<Money> CreateNonNegative(decimal amount)
    {
        if (amount < 0)
            return Result<Money>.Validation("O valor não pode ser negativo.");

        return Result<Money>.Success(new Money(amount));
    }

    /// <summary>
    /// Reconstrói a partir de um valor já validado e persistido (vindo do banco).
    /// </summary>
    public static Money FromExistingValue(decimal amount) => new(amount);

    public static Money operator *(Money money, decimal multiplier) =>
        new(money.Amount * multiplier);

    public static Money operator +(Money a, Money b) =>
        new(a.Amount + b.Amount);

    public override string ToString() => Amount.ToString("C");

    public override bool Equals(object? obj) =>
        obj is Money other && Amount == other.Amount;

    public override int GetHashCode() => Amount.GetHashCode();
}
