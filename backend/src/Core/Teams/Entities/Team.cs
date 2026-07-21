namespace Construcheck.Core.Teams.Entities;

public class Team
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
}