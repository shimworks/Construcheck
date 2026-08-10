using Construcheck.Construction.Domain.SharedValueObjects;
using Construcheck.SharedKernel;

namespace Construcheck.Unit.Tests.Construction.Domain.SharedValueObjects;

public class MoneyTests
{
    // -------------------------------------------------------------------------
    // CreatePositive
    // -------------------------------------------------------------------------

    [Fact]
    public void CreatePositive_PositiveAmount_ReturnsSuccess()
    {
        // Act
        var result = Money.CreatePositive(100.50m);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(100.50m, result.Value!.Amount);
    }

    [Fact]
    public void CreatePositive_ZeroAmount_ReturnsValidationFailure()
    {
        // Arrange — fronteira exata: amount <= 0 inclui zero

        // Act
        var result = Money.CreatePositive(0m);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("O valor deve ser maior que zero.", result.Error);
    }

    [Fact]
    public void CreatePositive_NegativeAmount_ReturnsValidationFailure()
    {
        // Act
        var result = Money.CreatePositive(-0.01m);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public void CreatePositive_SmallestPositiveAmount_ReturnsSuccess()
    {
        // Arrange — fronteira do outro lado: o menor valor positivo representável

        // Act
        var result = Money.CreatePositive(0.01m);

        // Assert
        Assert.True(result.IsSuccess);
    }

    // -------------------------------------------------------------------------
    // CreateNonNegative
    // -------------------------------------------------------------------------

    [Fact]
    public void CreateNonNegative_ZeroAmount_ReturnsSuccess()
    {
        // Arrange — diferente de CreatePositive: zero é explicitamente permitido aqui

        // Act
        var result = Money.CreateNonNegative(0m);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value!.Amount);
    }

    [Fact]
    public void CreateNonNegative_PositiveAmount_ReturnsSuccess()
    {
        // Act
        var result = Money.CreateNonNegative(50m);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CreateNonNegative_NegativeAmount_ReturnsValidationFailure()
    {
        // Arrange — fronteira: o menor valor negativo possível (-0.01) já deve falhar

        // Act
        var result = Money.CreateNonNegative(-0.01m);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("O valor não pode ser negativo.", result.Error);
    }

    // -------------------------------------------------------------------------
    // FromExistingValue
    // -------------------------------------------------------------------------

    [Fact]
    public void FromExistingValue_NegativeAmount_DoesNotValidateAndReturnsAsIs()
    {
        // Arrange — reconstrução a partir do banco não revalida a política de negócio

        // Act
        var money = Money.FromExistingValue(-500m);

        // Assert
        Assert.Equal(-500m, money.Amount);
    }

    // -------------------------------------------------------------------------
    // Operators
    // -------------------------------------------------------------------------

    [Theory]
    // Multiplicador fracionário; Multiplicador zero; Multiplicador negativo
    [InlineData(10, 2.5, 25)]
    [InlineData(10, 0, 0)]
    [InlineData(10, -1, -10)]
    public void MultiplyOperator_ReturnsAmountTimesMultiplier(decimal amount, decimal multiplier, decimal expected)
    {
        // Arrange
        var money = Money.FromExistingValue(amount);

        // Act
        var result = money * multiplier;

        // Assert
        Assert.Equal(expected, result.Amount);
    }

    [Fact]
    public void AddOperator_TwoMoneyValues_ReturnsSumOfAmounts()
    {
        // Arrange
        var a = Money.FromExistingValue(100m);
        var b = Money.FromExistingValue(50m);

        // Act
        var result = a + b;

        // Assert
        Assert.Equal(150m, result.Amount);
    }

    // -------------------------------------------------------------------------
    // Equality
    // -------------------------------------------------------------------------

    [Fact]
    public void Equals_SameAmount_ReturnsTrue()
    {
        // Arrange
        var a = Money.FromExistingValue(100m);
        var b = Money.FromExistingValue(100m);

        // Act & Assert
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentAmount_ReturnsFalse()
    {
        // Arrange
        var a = Money.FromExistingValue(100m);
        var b = Money.FromExistingValue(200m);

        // Act & Assert
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_NonMoneyObject_ReturnsFalse()
    {
        // Arrange
        var money = Money.FromExistingValue(100m);

        // Act & Assert
        Assert.False(money.Equals("100"));
    }
}
