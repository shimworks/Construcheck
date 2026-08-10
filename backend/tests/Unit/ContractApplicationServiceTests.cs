using Construcheck.Construction.Application.Contracts.DTOs;
using Construcheck.Construction.Application.Contracts.Services;
using Construcheck.Construction.Domain.Contracts;
using Construcheck.Construction.Domain.Projects;
using Construcheck.SharedKernel;
using NSubstitute;

namespace Construcheck.Unit.Tests.Construction.Application.Contracts;

public class ContractApplicationServiceTests
{
    private readonly IContractRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly ContractApplicationService _sut;

    private static readonly Guid ProjectId = Guid.NewGuid();

    public ContractApplicationServiceTests()
    {
        _repository = Substitute.For<IContractRepository>();
        _projectRepository = Substitute.For<IProjectRepository>();
        _sut = new ContractApplicationService(_repository, _projectRepository);
    }

    private static Project BuildProject() =>
        Project.Create("Obra", "Endereço", "Gestor", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 1)).Value!;

    private static Contract BuildContract(Guid projectId) =>
        Contract.Create(projectId, ContractType.Contractor, "Empreiteira", 1000m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Responsável").Value!;

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ProjectNotFound_ReturnsNotFoundFailureWithoutTouchingContractRepository()
    {
        // Arrange
        var request = new CreateContractRequest(ContractType.Contractor, "Empreiteira", 1000m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Responsável");
        _projectRepository.GetByIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns((Project?)null);

        // Act
        var result = await _sut.CreateAsync(ProjectId, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
        Assert.Equal("Obra não encontrada.", result.Error);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Contract>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ProjectExistsAndValidData_ReturnsSuccessAndPersists()
    {
        // Arrange
        var project = BuildProject();
        var request = new CreateContractRequest(ContractType.Supplier, "Fornecedor", 5000m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Responsável");
        _projectRepository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var result = await _sut.CreateAsync(project.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(project.Id, result.Value!.ProjectId);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ProjectExistsButInvalidValue_ReturnsValidationFailureWithoutPersisting()
    {
        // Arrange
        var project = BuildProject();
        var request = new CreateContractRequest(ContractType.Contractor, "Empreiteira", 0m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Responsável");
        _projectRepository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var result = await _sut.CreateAsync(project.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Contract>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // GetByProjectIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByProjectIdAsync_ReturnsAllContractsMappedToResponses()
    {
        // Arrange
        var contract1 = BuildContract(ProjectId);
        var contract2 = BuildContract(ProjectId);
        _repository.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns([contract1, contract2]);

        // Act
        var result = await _sut.GetByProjectIdAsync(ProjectId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
    }

    // -------------------------------------------------------------------------
    // GetByIdAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_ContractDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Contract?)null);

        // Act
        var result = await _sut.GetByIdAsync(id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
        Assert.Equal("Contrato não encontrado.", result.Error);
    }

    [Fact]
    public async Task GetByIdAsync_ContractExists_ReturnsSuccess()
    {
        // Arrange
        var contract = BuildContract(ProjectId);
        _repository.GetByIdAsync(contract.Id, Arg.Any<CancellationToken>()).Returns(contract);

        // Act
        var result = await _sut.GetByIdAsync(contract.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(contract.Id, result.Value!.Id);
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ContractDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateContractRequest(ContractType.Contractor, "Nome", 1000m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Responsável");
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Contract?)null);

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
        var contract = BuildContract(ProjectId);
        var request = new UpdateContractRequest(ContractType.Equipment, "Nome Novo", 2000m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Responsável Novo");
        _repository.GetByIdAsync(contract.Id, Arg.Any<CancellationToken>()).Returns(contract);

        // Act
        var result = await _sut.UpdateAsync(contract.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Nome Novo", result.Value!.CounterpartyName);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_InvalidValue_ReturnsValidationFailureWithoutSaving()
    {
        // Arrange
        var contract = BuildContract(ProjectId);
        var request = new UpdateContractRequest(ContractType.Contractor, "Nome", -1m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Responsável");
        _repository.GetByIdAsync(contract.Id, Arg.Any<CancellationToken>()).Returns(contract);

        // Act
        var result = await _sut.UpdateAsync(contract.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // RemoveAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemoveAsync_ContractDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Contract?)null);

        // Act
        var result = await _sut.RemoveAsync(id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task RemoveAsync_ContractExists_RemovesAndPersists()
    {
        // Arrange
        var contract = BuildContract(ProjectId);
        _repository.GetByIdAsync(contract.Id, Arg.Any<CancellationToken>()).Returns(contract);

        // Act
        var result = await _sut.RemoveAsync(contract.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ContractStatus.Removed, contract.Status);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
