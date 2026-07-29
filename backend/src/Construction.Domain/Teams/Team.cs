using Construcheck.SharedKernel;

namespace Construcheck.Construction.Domain.Teams;

public class Team
{
    private const int MinMemberCount = 1;

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Specialty { get; private set; }
    public int MemberCount { get; private set; }
    public TeamStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Team() { }

    public static Result<Team> Create(Guid projectId, string name, string? specialty, int memberCount)
    {
        if (memberCount < MinMemberCount)
            return Result<Team>.Validation($"Uma equipe deve ter ao menos {MinMemberCount} membro.");

        var team = new Team
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Specialty = specialty,
            MemberCount = memberCount,
            Status = TeamStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        return Result<Team>.Success(team);
    }

    public static Team Reconstitute(
        Guid id, Guid projectId, string name, string? specialty,
        int memberCount, TeamStatus status, DateTime createdAt) => new()
    {
        Id = id,
        ProjectId = projectId,
        Name = name,
        Specialty = specialty,
        MemberCount = memberCount,
        Status = status,
        CreatedAt = createdAt
    };

    public Result<bool> UpdateDetails(string name, string? specialty, int memberCount)
    {
        if (memberCount < MinMemberCount)
            return Result<bool>.Validation($"Uma equipe deve ter ao menos {MinMemberCount} membro.");

        Name = name;
        Specialty = specialty;
        MemberCount = memberCount;

        return Result<bool>.Success(true);
    }

    public void Remove() => Status = TeamStatus.Removed;
}
