using Construcheck.Construction.Application.Projects.DTOs;
using Construcheck.Construction.Application.Projects.Services;
using Construcheck.Construction.Domain.Projects;
using Construcheck.SharedKernel;
using NSubstitute;

namespace Construcheck.Unit.Tests.Construction.Application.Projects;

public class ProjectApplicationServiceTests
{
    private readonly IProjectRepository _repository;
    private readonly ProjectApplicationService _sut;

    public ProjectApplicationServiceTests()
    {
        _repository = Substitute.For<IProjectRepository>();
        _sut = new ProjectApplicationService(_repository);
    }

    private static Project BuildProject() =>
        Project.Create("Obra Teste", "Rua Teste", "Gestor Teste", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 1)).Value!;

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsSuccessAndPersists()
    {
        // Arrange
        var request = new CreateProjectRequest("Obra Nova", "Endereço", "Gestor", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 1));

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Obra Nova", result.Value!.Name);
        await _repository.Received(1).AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_InvalidDateRange_ReturnsValidationFailureWithoutPersisting()
    {
        // Arrange
        var request = new CreateProjectRequest("Obra Inválida", "Endereço", "Gestor", new DateOnly(2026, 12, 1), new DateOnly(2026, 1, 1));

        // Act
        var result = await _sut.CreateAsync(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // GetAllAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_RepositoryReturnsProjects_MapsAllToResponses()
    {
        // Arrange
        var project1 = BuildProject();
        var project2 = BuildProject();
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([project1, project2]);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public async Task GetAllAsync_RepositoryReturnsEmptyList_ReturnsSuccessWithEmptyList()
    {
        // Arrange
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ProjectExists_ReturnsSuccess()
    {
        // Arrange
        var project = BuildProject();
        _repository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var result = await _sut.GetByIdAsync(project.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(project.Id, result.Value!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ProjectDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Project?)null);

        // Act
        var result = await _sut.GetByIdAsync(id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
        Assert.Equal("Obra não encontrada.", result.Error);
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ProjectDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateProjectRequest("Nome", "Endereço", "Gestor", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1));
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Project?)null);

        // Act
        var result = await _sut.UpdateAsync(id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesAndPersists()
    {
        // Arrange
        var project = BuildProject();
        var request = new UpdateProjectRequest("Nome Atualizado", "Endereço Novo", "Gestor Novo", new DateOnly(2026, 2, 1), new DateOnly(2026, 8, 1));
        _repository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var result = await _sut.UpdateAsync(project.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Nome Atualizado", result.Value!.Name);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_InvalidDateRange_ReturnsValidationFailureWithoutSaving()
    {
        // Arrange
        var project = BuildProject();
        var request = new UpdateProjectRequest("Nome", "Endereço", "Gestor", new DateOnly(2026, 8, 1), new DateOnly(2026, 1, 1));
        _repository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var result = await _sut.UpdateAsync(project.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // ArchiveAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ArchiveAsync_ProjectDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Project?)null);

        // Act
        var result = await _sut.ArchiveAsync(id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task ArchiveAsync_ProjectExists_ArchivesAndPersists()
    {
        // Arrange
        var project = BuildProject();
        _repository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var result = await _sut.ArchiveAsync(project.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectStatus.Archived, project.Status);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
