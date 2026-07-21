using Construcheck.Core.Projects.Enums;

namespace Construcheck.Core.Projects.Entities;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TechnicalManager { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly TargetEndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}