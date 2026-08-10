using Construcheck.Construction.Application.Teams.DTOs;
using Construcheck.Construction.Application.Teams.Services;
using Construcheck.Construction.Domain.Projects;
using Construcheck.Construction.Domain.Teams;
using Construcheck.SharedKernel;
using NSubstitute;

namespace Construcheck.Unit.Tests.Construction.Application.Teams;

public class TeamApplicationServiceTests
{
    private readonly ITeamRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly TeamApplicationService _sut;

    private static readonly Guid ProjectId = Guid.NewGuid();

    public TeamApplicationServiceTests()
    {
        _repository = Substitute.For<ITeamRepository>();
        _projectRepository = Substitute.For<IProjectRepository>();
        _sut = new TeamApplicationService(_repository, _projectRepository);
    }

    private static Project BuildProject() =>
        Project.Create("Obra", "Endereço", "Gestor", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 1)).Value!;

    private static Team BuildTeam(Guid projectId) =>
        Team.Create(projectId, "Equipe", "Elétrica", 3).Value!;

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ProjectNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var request = new CreateTeamRequest("Equipe", "Elétrica", 3);
        _projectRepository.GetByIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns((Project?)null);

        // Act
        var result = await _sut.CreateAsync(ProjectId, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
        Assert.Equal("Obra não encontrada.", result.Error);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Team>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ProjectExistsAndValidData_ReturnsSuccessAndPersists()
    {
        // Arrange
        var project = BuildProject();
        var request = new CreateTeamRequest("Equipe Alfa", "Hidráulica", 4);
        _projectRepository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var result = await _sut.CreateAsync(project.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Equipe Alfa", result.Value!.Name);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ProjectExistsButInvalidMemberCount_ReturnsValidationFailureWithoutPersisting()
    {
        // Arrange
        var project = BuildProject();
        var request = new CreateTeamRequest("Equipe", "Elétrica", 0);
        _projectRepository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var result = await _sut.CreateAsync(project.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Team>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // GetByProjectIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByProjectIdAsync_ReturnsAllTeamsMappedToResponses()
    {
        // Arrange
        var team1 = BuildTeam(ProjectId);
        var team2 = BuildTeam(ProjectId);
        _repository.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns([team1, team2]);

        // Act
        var result = await _sut.GetByProjectIdAsync(ProjectId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_TeamDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateTeamRequest("Nome", "Especialidade", 3);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Team?)null);

        // Act
        var result = await _sut.UpdateAsync(id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
        Assert.Equal("Equipe não encontrada.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesAndPersists()
    {
        // Arrange
        var team = BuildTeam(ProjectId);
        var request = new UpdateTeamRequest("Nome Novo", "Especialidade Nova", 8);
        _repository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);

        // Act
        var result = await _sut.UpdateAsync(team.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Nome Novo", result.Value!.Name);
        Assert.Equal(8, result.Value.MemberCount);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_InvalidMemberCount_ReturnsValidationFailureWithoutSaving()
    {
        // Arrange
        var team = BuildTeam(ProjectId);
        var request = new UpdateTeamRequest("Nome", "Especialidade", -1);
        _repository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);

        // Act
        var result = await _sut.UpdateAsync(team.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // RemoveAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemoveAsync_TeamDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Team?)null);

        // Act
        var result = await _sut.RemoveAsync(id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task RemoveAsync_TeamExists_RemovesAndPersists()
    {
        // Arrange
        var team = BuildTeam(ProjectId);
        _repository.GetByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);

        // Act
        var result = await _sut.RemoveAsync(team.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TeamStatus.Removed, team.Status);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
