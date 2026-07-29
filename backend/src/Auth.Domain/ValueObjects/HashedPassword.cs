using System.Text.RegularExpressions;
using Construcheck.SharedKernel;

namespace Construcheck.Auth.Domain.ValueObjects;

public sealed class HashedPassword
{
    private const int MinLength = 8;

    public string Value { get; }

    private HashedPassword(string hashedValue)
    {
        Value = hashedValue;
    }

    public static Result<HashedPassword> Create(string plainTextPassword)
    {
        var violations = ValidatePolicy(plainTextPassword);

        if (violations.Count > 0)
            return Result<HashedPassword>.Validation(string.Join(" ", violations));

        var hash = BCrypt.Net.BCrypt.HashPassword(plainTextPassword);
        return Result<HashedPassword>.Success(new HashedPassword(hash));
    }

    /// <summary>
    /// Reconstrói o Value Object a partir de um hash já existente (vindo do banco).
    /// Não revalida a política — a política se aplica na criação, não na leitura.
    /// </summary>
    public static HashedPassword FromExistingHash(string hashedValue) => new(hashedValue);

    public bool Verify(string plainTextPassword) =>
        BCrypt.Net.BCrypt.Verify(plainTextPassword, Value);

    public override bool Equals(object? obj) =>
        obj is HashedPassword other && Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();

    private static List<string> ValidatePolicy(string plainTextPassword)
    {
        var violations = new List<string>();

        if (string.IsNullOrEmpty(plainTextPassword) || plainTextPassword.Length < MinLength)
            violations.Add($"A senha deve ter no mínimo {MinLength} caracteres.");

        if (!Regex.IsMatch(plainTextPassword, "[A-Z]"))
            violations.Add("A senha deve conter ao menos uma letra maiúscula.");

        if (!Regex.IsMatch(plainTextPassword, "[0-9]"))
            violations.Add("A senha deve conter ao menos um número.");

        if (!Regex.IsMatch(plainTextPassword, @"[^a-zA-Z0-9]"))
            violations.Add("A senha deve conter ao menos um caractere especial.");

        return violations;
    }
}
