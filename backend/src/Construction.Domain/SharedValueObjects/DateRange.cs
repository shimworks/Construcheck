using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.SharedValueObjects;

public sealed class DateRange
{
    public DateOnly Start { get; }
    public DateOnly End { get; }

    private DateRange(DateOnly start, DateOnly end)
    {
        Start = start;
        End = end;
    }

    public static Result<DateRange> Create(DateOnly start, DateOnly end)
    {
        if (end < start)
            return Result<DateRange>.Validation("A data final não pode ser anterior à data inicial.");

        return Result<DateRange>.Success(new DateRange(start, end));
    }

    /// <summary>
    /// Reconstrói a partir de valores já validados e persistidos (vindo do banco).
    /// </summary>
    public static DateRange FromExistingValues(DateOnly start, DateOnly end) => new(start, end);

    public int DurationInDays => End.DayNumber - Start.DayNumber;

    public bool Overlaps(DateRange other) =>
        Start <= other.End && End >= other.Start;

    public override bool Equals(object? obj) =>
        obj is DateRange other && Start == other.Start && End == other.End;

    public override int GetHashCode() => HashCode.Combine(Start, End);
}
