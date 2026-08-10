using Construcheck.Construction.Domain.Teams;
using Construcheck.SharedKernel;

namespace Construcheck.Unit.Tests.Construction.Domain.Teams;

public class TeamTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_ValidData_ReturnsSuccessWithActiveStatus()
    {
        // Act
        var result = Team.Create(ProjectId, "Equipe Alfa", "Elétrica", 5);

        // Assert
        Assert.True(result.IsSuccess);
        var team = result.Value!;
        Assert.Equal(ProjectId, team.ProjectId);
        Assert.Equal("Equipe Alfa", team.Name);
        Assert.Equal("Elétrica", team.Specialty);
        Assert.Equal(5, team.MemberCount);
        Assert.Equal(TeamStatus.Active, team.Status);
    }

    [Fact]
    public void Create_NullSpecialty_IsAccepted()
    {
        // Act
        var result = Team.Create(ProjectId, "Equipe Beta", null, 3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Specialty);
    }

    [Fact]
    public void Create_MemberCountAtMinimumBoundary_ReturnsSuccess()
    {
        // Arrange — fronteira exata: MinMemberCount = 1, 1 < 1 é falso, deve passar

        // Act
        var result = Team.Create(ProjectId, "Equipe Solo", "Hidráulica", 1);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_MemberCountBelowMinimum_ReturnsValidationFailure()
    {
        // Arrange — o outro lado do boundary: 0 < 1 é verdadeiro

        // Act
        var result = Team.Create(ProjectId, "Equipe Vazia", "Pintura", 0);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("Uma equipe deve ter ao menos 1 membro.", result.Error);
    }

    [Fact]
    public void Create_NegativeMemberCount_ReturnsValidationFailure()
    {
        // Act
        var result = Team.Create(ProjectId, "Equipe Inválida", "Alvenaria", -1);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    // -------------------------------------------------------------------------
    // Reconstitute
    // -------------------------------------------------------------------------

    [Fact]
    public void Reconstitute_ValidData_RestoresAllFieldsExactly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        // Act
        var team = Team.Reconstitute(id, ProjectId, "Equipe X", "Cobertura", 4, TeamStatus.Removed, createdAt);

        // Assert
        Assert.Equal(id, team.Id);
        Assert.Equal(TeamStatus.Removed, team.Status);
        Assert.Equal(createdAt, team.CreatedAt);
    }

    // -------------------------------------------------------------------------
    // UpdateDetails
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateDetails_ValidData_UpdatesAllFieldsAndReturnsSuccess()
    {
        // Arrange
        var team = Team.Create(ProjectId, "Nome Antigo", "Especialidade Antiga", 2).Value!;

        // Act
        var result = team.UpdateDetails("Nome Novo", "Especialidade Nova", 6);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Nome Novo", team.Name);
        Assert.Equal("Especialidade Nova", team.Specialty);
        Assert.Equal(6, team.MemberCount);
    }

    [Fact]
    public void UpdateDetails_MemberCountBelowMinimum_ReturnsValidationFailureAndKeepsOriginalCount()
    {
        // Arrange
        var team = Team.Create(ProjectId, "Nome", "Especialidade", 3).Value!;

        // Act
        var result = team.UpdateDetails("Nome", "Especialidade", 0);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(3, team.MemberCount);
    }

    // -------------------------------------------------------------------------
    // Remove
    // -------------------------------------------------------------------------

    [Fact]
    public void Remove_ActiveTeam_SetsStatusToRemoved()
    {
        // Arrange
        var team = Team.Create(ProjectId, "Nome", "Especialidade", 3).Value!;

        // Act
        team.Remove();

        // Assert
        Assert.Equal(TeamStatus.Removed, team.Status);
    }
}
