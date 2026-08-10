using Construcheck.Construction.Application.Budget.DTOs;
using Construcheck.Construction.Application.Budget.Services;
using Construcheck.Construction.Domain.Budget;
using Construcheck.Construction.Domain.Projects;
using Construcheck.SharedKernel;
using NSubstitute;

namespace Construcheck.Unit.Tests.Construction.Application.Budget;

public class BudgetApplicationServiceTests
{
    private readonly IBudgetItemRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly BudgetApplicationService _sut;

    private static readonly Guid ProjectId = Guid.NewGuid();

    public BudgetApplicationServiceTests()
    {
        _repository = Substitute.For<IBudgetItemRepository>();
        _projectRepository = Substitute.For<IProjectRepository>();
        _sut = new BudgetApplicationService(_repository, _projectRepository);
    }

    private static Project BuildProject() =>
        Project.Create("Obra", "Endereço", "Gestor", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 1)).Value!;

    private static BudgetItem BuildItem(Guid projectId, string costCenter, decimal quantity, decimal unitPrice) =>
        BudgetItem.Create(projectId, costCenter, "Item", "un", quantity, unitPrice, null).Value!;

    // -------------------------------------------------------------------------
    // CreateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ProjectNotFound_ReturnsNotFoundFailure()
    {
        // Arrange
        var request = new CreateBudgetItemRequest("Fundação", "Concreto", "m³", 10m, 100m, null);
        _projectRepository.GetByIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns((Project?)null);

        // Act
        var result = await _sut.CreateAsync(ProjectId, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
        await _repository.DidNotReceive().AddAsync(Arg.Any<BudgetItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ProjectExistsAndValidData_ReturnsSuccessAndPersists()
    {
        // Arrange
        var project = BuildProject();
        var request = new CreateBudgetItemRequest("Fundação", "Concreto", "m³", 10m, 100m, "SINAPI-1");
        _projectRepository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var result = await _sut.CreateAsync(project.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, result.Value!.TotalValue);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ProjectExistsButInvalidQuantity_ReturnsValidationFailureWithoutPersisting()
    {
        // Arrange
        var project = BuildProject();
        var request = new CreateBudgetItemRequest("Fundação", "Concreto", "m³", 0m, 100m, null);
        _projectRepository.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        // Act
        var result = await _sut.CreateAsync(project.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        await _repository.DidNotReceive().AddAsync(Arg.Any<BudgetItem>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // GetByProjectIdAsync — foco na lógica de agregação (GroupBy + Sum), não só passagem de dados
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByProjectIdAsync_NoItems_ReturnsEmptySummaryWithZeroTotal()
    {
        // Arrange
        _repository.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _sut.GetByProjectIdAsync(ProjectId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Items);
        Assert.Empty(result.Value.TotalsByCostCenter);
        Assert.Equal(0m, result.Value.ProjectTotalValue);
    }

    [Fact]
    public async Task GetByProjectIdAsync_SingleCostCenter_GroupsCorrectly()
    {
        // Arrange
        var item1 = BuildItem(ProjectId, "Fundação", 10m, 50m);  // 500
        var item2 = BuildItem(ProjectId, "Fundação", 5m, 20m);   // 100
        _repository.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns([item1, item2]);

        // Act
        var result = await _sut.GetByProjectIdAsync(ProjectId);

        // Assert
        Assert.True(result.IsSuccess);
        var total = Assert.Single(result.Value!.TotalsByCostCenter);
        Assert.Equal("Fundação", total.CostCenter);
        Assert.Equal(600m, total.Total);
        Assert.Equal(600m, result.Value.ProjectTotalValue);
    }

    [Fact]
    public async Task GetByProjectIdAsync_MultipleCostCenters_GroupsEachSeparatelyAndSumsProjectTotal()
    {
        // Arrange — combinação real: dois centros de custo distintos, cada um com
        // múltiplos itens, verificando que o agrupamento não mistura os totais
        var fundacao1 = BuildItem(ProjectId, "Fundação", 10m, 50m);   // 500
        var fundacao2 = BuildItem(ProjectId, "Fundação", 2m, 100m);   // 200
        var eletrica1 = BuildItem(ProjectId, "Elétrica", 20m, 15m);   // 300
        _repository.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns([fundacao1, fundacao2, eletrica1]);

        // Act
        var result = await _sut.GetByProjectIdAsync(ProjectId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalsByCostCenter.Count);

        var fundacaoTotal = result.Value.TotalsByCostCenter.Single(t => t.CostCenter == "Fundação");
        Assert.Equal(700m, fundacaoTotal.Total);

        var eletricaTotal = result.Value.TotalsByCostCenter.Single(t => t.CostCenter == "Elétrica");
        Assert.Equal(300m, eletricaTotal.Total);

        Assert.Equal(1000m, result.Value.ProjectTotalValue); // soma de TODOS os centros
    }

    [Fact]
    public async Task GetByProjectIdAsync_TotalsByCostCenter_AreOrderedAlphabetically()
    {
        // Arrange — a ordenação (.OrderBy(t => t.CostCenter)) é comportamento observável
        var zCenter = BuildItem(ProjectId, "Zeladoria", 1m, 10m);
        var aCenter = BuildItem(ProjectId, "Acabamentos", 1m, 10m);
        _repository.GetByProjectIdAsync(ProjectId, Arg.Any<CancellationToken>()).Returns([zCenter, aCenter]);

        // Act
        var result = await _sut.GetByProjectIdAsync(ProjectId);

        // Assert
        Assert.Equal("Acabamentos", result.Value!.TotalsByCostCenter[0].CostCenter);
        Assert.Equal("Zeladoria", result.Value.TotalsByCostCenter[1].CostCenter);
    }

    // -------------------------------------------------------------------------
    // UpdateAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ItemDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new UpdateBudgetItemRequest("Centro", "Descrição", "un", 1m, 10m, null);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((BudgetItem?)null);

        // Act
        var result = await _sut.UpdateAsync(id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
        Assert.Equal("Item de orçamento não encontrado.", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesAndPersists()
    {
        // Arrange
        var item = BuildItem(ProjectId, "Centro", 1m, 10m);
        var request = new UpdateBudgetItemRequest("Centro Novo", "Descrição Nova", "kg", 5m, 20m, "COD");
        _repository.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        // Act
        var result = await _sut.UpdateAsync(item.Id, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value!.TotalValue);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_InvalidQuantity_ReturnsValidationFailureWithoutSaving()
    {
        // Arrange
        var item = BuildItem(ProjectId, "Centro", 1m, 10m);
        var request = new UpdateBudgetItemRequest("Centro", "Descrição", "un", -1m, 10m, null);
        _repository.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        // Act
        var result = await _sut.UpdateAsync(item.Id, request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // RemoveAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemoveAsync_ItemDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((BudgetItem?)null);

        // Act
        var result = await _sut.RemoveAsync(id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task RemoveAsync_ItemExists_RemovesAndPersists()
    {
        // Arrange
        var item = BuildItem(ProjectId, "Centro", 1m, 10m);
        _repository.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        // Act
        var result = await _sut.RemoveAsync(item.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(BudgetItemStatus.Removed, item.Status);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
