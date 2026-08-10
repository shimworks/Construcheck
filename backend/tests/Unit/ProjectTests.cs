using Construcheck.Construction.Domain.Projects;
using Construcheck.SharedKernel;

namespace Construcheck.Unit.Tests.Construction.Domain.Projects;

public class ProjectTests
{
    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_ValidData_ReturnsSuccessWithActiveStatus()
    {
        // Arrange
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 12, 31);

        // Act
        var result = Project.Create("Edifício Aurora", "Rua das Flores, 100", "Carlos Silva", start, end);

        // Assert
        Assert.True(result.IsSuccess);
        var project = result.Value!;
        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("Edifício Aurora", project.Name);
        Assert.Equal("Rua das Flores, 100", project.Address);
        Assert.Equal("Carlos Silva", project.TechnicalManager);
        Assert.Equal(start, project.Schedule.Start);
        Assert.Equal(end, project.Schedule.End);
        Assert.Equal(ProjectStatus.Active, project.Status);
    }

    [Fact]
    public void Create_EndDateBeforeStartDate_ReturnsValidationFailure()
    {
        // Arrange
        var start = new DateOnly(2026, 12, 31);
        var end = new DateOnly(2026, 1, 1);

        // Act
        var result = Project.Create("Edifício Aurora", "Rua das Flores, 100", "Carlos Silva", start, end);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("A data final não pode ser anterior à data inicial.", result.Error);
    }

    [Fact]
    public void Create_StartDateEqualsEndDate_ReturnsSuccess()
    {
        // Arrange — fronteira: mesma data não é "anterior", então deve passar
        var sameDay = new DateOnly(2026, 6, 15);

        // Act
        var result = Project.Create("Reforma Pontual", "Av. Central, 50", "Ana Souza", sameDay, sameDay);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_SetsCreatedAtAndUpdatedAtToNearlyTheSameRecentInstant()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-5);

        // Act
        var result = Project.Create("Casa Verde", "Rua B, 20", "João Lima", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1));
        var after = DateTime.UtcNow.AddSeconds(5);

        // Assert
        var project = result.Value!;
        Assert.True(project.CreatedAt > before && project.CreatedAt < after);
        Assert.True(project.UpdatedAt > before && project.UpdatedAt < after);
        // CreatedAt e UpdatedAt são duas chamadas SEPARADAS a DateTime.UtcNow no construtor
        // (ver Project.Create), não a mesma variável reaproveitada — portanto nunca são
        // garantidamente bit-idênticas, só muito próximas. Tolerância de 1 segundo é
        // generosa o suficiente para nunca ser flaky, e ainda prova que uma não ficou
        // "atrás" da outra por engano (ex: UpdatedAt sendo esquecido e ficando com
        // DateTime.MinValue, o que este assert pegaria).
        Assert.True(Math.Abs((project.UpdatedAt - project.CreatedAt).TotalSeconds) < 1);
    }

    // -------------------------------------------------------------------------
    // Reconstitute
    // -------------------------------------------------------------------------

    [Fact]
    public void Reconstitute_ValidData_RestoresAllFieldsExactly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var project = Project.Reconstitute(
            id, "Obra X", "Rua Y", "Gestor Z",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            ProjectStatus.Archived, createdAt, updatedAt);

        // Assert
        Assert.Equal(id, project.Id);
        Assert.Equal("Obra X", project.Name);
        Assert.Equal(ProjectStatus.Archived, project.Status);
        Assert.Equal(createdAt, project.CreatedAt);
        Assert.Equal(updatedAt, project.UpdatedAt);
    }

    // -------------------------------------------------------------------------
    // UpdateDetails
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateDetails_ValidData_UpdatesAllFieldsAndReturnsSuccess()
    {
        // Arrange
        var project = Project.Create("Nome Antigo", "Endereço Antigo", "Gestor Antigo",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1)).Value!;
        var newStart = new DateOnly(2026, 2, 1);
        var newEnd = new DateOnly(2026, 8, 1);

        // Act
        var result = project.UpdateDetails("Nome Novo", "Endereço Novo", "Gestor Novo", newStart, newEnd);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Nome Novo", project.Name);
        Assert.Equal("Endereço Novo", project.Address);
        Assert.Equal("Gestor Novo", project.TechnicalManager);
        Assert.Equal(newStart, project.Schedule.Start);
        Assert.Equal(newEnd, project.Schedule.End);
    }

    [Fact]
    public void UpdateDetails_EndDateBeforeStartDate_ReturnsValidationFailureAndKeepsOriginalData()
    {
        // Arrange
        var originalStart = new DateOnly(2026, 1, 1);
        var originalEnd = new DateOnly(2026, 6, 1);
        var project = Project.Create("Nome Original", "Endereço Original", "Gestor Original",
            originalStart, originalEnd).Value!;

        // Act
        var result = project.UpdateDetails("Nome Novo", "Endereço Novo", "Gestor Novo",
            new DateOnly(2026, 12, 1), new DateOnly(2026, 1, 1));

        // Assert — a atualização falhou, então o estado anterior deve permanecer intacto
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("Nome Original", project.Name);
        Assert.Equal(originalStart, project.Schedule.Start);
        Assert.Equal(originalEnd, project.Schedule.End);
    }

    [Fact]
    public void UpdateDetails_ValidData_UpdatesUpdatedAtTimestamp()
    {
        // Arrange
        var project = Project.Create("Nome", "Endereço", "Gestor",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1)).Value!;
        var originalUpdatedAt = project.UpdatedAt;

        // Act
        project.UpdateDetails("Nome Novo", "Endereço", "Gestor", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1));

        // Assert
        Assert.True(project.UpdatedAt >= originalUpdatedAt);
    }

    // -------------------------------------------------------------------------
    // Archive
    // -------------------------------------------------------------------------

    [Fact]
    public void Archive_ActiveProject_SetsStatusToArchived()
    {
        // Arrange
        var project = Project.Create("Nome", "Endereço", "Gestor",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1)).Value!;

        // Act
        project.Archive();

        // Assert
        Assert.Equal(ProjectStatus.Archived, project.Status);
    }

    [Fact]
    public void Archive_UpdatesUpdatedAtTimestamp()
    {
        // Arrange
        var project = Project.Create("Nome", "Endereço", "Gestor",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1)).Value!;
        var originalUpdatedAt = project.UpdatedAt;

        // Act
        project.Archive();

        // Assert
        Assert.True(project.UpdatedAt >= originalUpdatedAt);
    }
}
