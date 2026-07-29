using System.Text.RegularExpressions;
using Construcheck.SharedKernel;

namespace Construcheck.Auth.Domain.ValueObjects;

public sealed class Email
{
    private static readonly Regex FormatRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled);

    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Result<Email> Create(string rawEmail)
    {
        if (string.IsNullOrWhiteSpace(rawEmail))
            return Result<Email>.Validation("E-mail não pode ser vazio.");

        var normalized = rawEmail.ToLowerInvariant().Trim();

        if (!FormatRegex.IsMatch(normalized))
            return Result<Email>.Validation("E-mail em formato inválido.");

        return Result<Email>.Success(new Email(normalized));
    }

    /// <summary>
    /// Reconstrói a partir de um valor já validado e persistido (vindo do banco).
    /// </summary>
    public static Email FromExistingValue(string value) => new(value);

    public override string ToString() => Value;

    public override bool Equals(object? obj) =>
        obj is Email other && Value == other.Value;

    public override int GetHashCode() => Value.GetHashCode();
}
