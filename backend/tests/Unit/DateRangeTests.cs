using Construcheck.Construction.Domain.SharedValueObjects;
using Construcheck.SharedKernel;

namespace Construcheck.Unit.Tests.Construction.Domain.SharedValueObjects;

public class DateRangeTests
{
    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_EndAfterStart_ReturnsSuccess()
    {
        // Act
        var result = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 1, 1), result.Value!.Start);
        Assert.Equal(new DateOnly(2026, 1, 31), result.Value.End);
    }

    [Fact]
    public void Create_EndEqualsStart_ReturnsSuccess()
    {
        // Arrange — fronteira exata: end < start é falso quando são iguais
        var sameDay = new DateOnly(2026, 6, 15);

        // Act
        var result = DateRange.Create(sameDay, sameDay);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_EndOneDayBeforeStart_ReturnsValidationFailure()
    {
        // Arrange — fronteira do outro lado: a menor violação possível

        // Act
        var result = DateRange.Create(new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 14));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("A data final não pode ser anterior à data inicial.", result.Error);
    }

    // -------------------------------------------------------------------------
    // DurationInDays
    // -------------------------------------------------------------------------

    [Fact]
    public void DurationInDays_ThirtyDayRange_ReturnsThirty()
    {
        // Arrange
        var range = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)).Value!;

        // Act
        var duration = range.DurationInDays;

        // Assert
        Assert.Equal(30, duration);
    }

    [Fact]
    public void DurationInDays_SameStartAndEnd_ReturnsZero()
    {
        // Arrange — fronteira: um intervalo degenerado de duração zero
        var sameDay = new DateOnly(2026, 6, 15);
        var range = DateRange.Create(sameDay, sameDay).Value!;

        // Act
        var duration = range.DurationInDays;

        // Assert
        Assert.Equal(0, duration);
    }

    // -------------------------------------------------------------------------
    // Overlaps
    // -------------------------------------------------------------------------

    [Fact]
    public void Overlaps_PartiallyOverlappingRanges_ReturnsTrue()
    {
        // Arrange
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15)).Value!;
        var b = DateRange.Create(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20)).Value!;

        // Act & Assert
        Assert.True(a.Overlaps(b));
        Assert.True(b.Overlaps(a)); // simétrico
    }

    [Fact]
    public void Overlaps_NonOverlappingRanges_ReturnsFalse()
    {
        // Arrange
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10)).Value!;
        var b = DateRange.Create(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 10)).Value!;

        // Act & Assert
        Assert.False(a.Overlaps(b));
    }

    [Fact]
    public void Overlaps_RangesTouchingAtExactBoundary_ReturnsTrue()
    {
        // Arrange — fronteira: End de A é igual a Start de B; Overlaps usa <= / >=,
        // então tocar exatamente na borda ainda conta como sobreposição
        var a = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10)).Value!;
        var b = DateRange.Create(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20)).Value!;

        // Act & Assert
        Assert.True(a.Overlaps(b));
    }

    [Fact]
    public void Overlaps_OneRangeFullyContainsTheOther_ReturnsTrue()
    {
        // Arrange
        var outer = DateRange.Create(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).Value!;
        var inner = DateRange.Create(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 10)).Value!;

        // Act & Assert
        Assert.True(outer.Overlaps(inner));
        Assert.True(inner.Overlaps(outer));
    }

    // -------------------------------------------------------------------------
    // Equality
    // -------------------------------------------------------------------------

    [Fact]
    public void Equals_SameStartAndEnd_ReturnsTrue()
    {
        // Arrange
        var a = DateRange.FromExistingValues(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var b = DateRange.FromExistingValues(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));

        // Act & Assert
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentEnd_ReturnsFalse()
    {
        // Arrange
        var a = DateRange.FromExistingValues(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10));
        var b = DateRange.FromExistingValues(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 20));

        // Act & Assert
        Assert.NotEqual(a, b);
    }
}
