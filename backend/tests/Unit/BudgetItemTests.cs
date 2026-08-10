using Construcheck.Construction.Domain.Budget;
using Construcheck.SharedKernel;

namespace Construcheck.Unit.Tests.Construction.Domain.Budget;

public class BudgetItemTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_ValidData_ReturnsSuccessWithActiveStatus()
    {
        // Act
        var result = BudgetItem.Create(ProjectId, "Fundação", "Concreto usinado", "m³", 10m, 350.50m, "SINAPI-001");

        // Assert
        Assert.True(result.IsSuccess);
        var item = result.Value!;
        Assert.Equal(ProjectId, item.ProjectId);
        Assert.Equal("Fundação", item.CostCenter);
        Assert.Equal(10m, item.Quantity);
        Assert.Equal(350.50m, item.UnitPrice.Amount);
        Assert.Equal("SINAPI-001", item.SinapiCode);
        Assert.Equal(BudgetItemStatus.Active, item.Status);
    }

    [Fact]
    public void Create_NullSinapiCode_IsAccepted()
    {
        // Act
        var result = BudgetItem.Create(ProjectId, "Estrutura", "Aço CA-50", "kg", 500m, 8.90m, null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.SinapiCode);
    }

    [Theory]
    // Zero não é maior que zero; Negativa
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_NonPositiveQuantity_ReturnsValidationFailure(decimal quantity)
    {
        // Act
        var result = BudgetItem.Create(ProjectId, "Cobertura", "Telha cerâmica", "un", quantity, 12m, null);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("A quantidade deve ser maior que zero.", result.Error);
    }

    [Fact]
    public void Create_UnitPriceAtZeroBoundary_ReturnsSuccess()
    {
        // Arrange — Money.CreateNonNegative permite zero explicitamente (item de cortesia)

        // Act
        var result = BudgetItem.Create(ProjectId, "Doação", "Material recebido", "un", 1m, 0m, null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value!.UnitPrice.Amount);
    }

    [Fact]
    public void Create_NegativeUnitPrice_ReturnsValidationFailure()
    {
        // Act
        var result = BudgetItem.Create(ProjectId, "Item", "Descrição", "un", 1m, -1m, null);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("O valor não pode ser negativo.", result.Error);
    }

    // -------------------------------------------------------------------------
    // TotalValue (transformação, não apenas leitura de campo)
    // -------------------------------------------------------------------------

    [Theory]
    // Quantidade inteira; Quantidade fracionária; Preço zero resulta em total zero
    [InlineData(10, 5.5, 55.0)]
    [InlineData(2.5, 4.0, 10.0)]
    [InlineData(1, 0, 0.0)]
    public void TotalValue_MultipliesQuantityByUnitPrice(decimal quantity, decimal unitPrice, decimal expectedTotal)
    {
        // Arrange
        var item = BudgetItem.Create(ProjectId, "Centro", "Item", "un", quantity, unitPrice, null).Value!;

        // Act
        var total = item.TotalValue;

        // Assert
        Assert.Equal(expectedTotal, total.Amount);
    }

    // -------------------------------------------------------------------------
    // Reconstitute
    // -------------------------------------------------------------------------

    [Fact]
    public void Reconstitute_ValidData_RestoresAllFieldsExactly()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var item = BudgetItem.Reconstitute(
            id, ProjectId, "Centro", "Descrição", "un", 3m, 100m, "COD-1",
            BudgetItemStatus.Removed, DateTime.UtcNow, DateTime.UtcNow);

        // Assert
        Assert.Equal(id, item.Id);
        Assert.Equal(BudgetItemStatus.Removed, item.Status);
        Assert.Equal(300m, item.TotalValue.Amount);
    }

    // -------------------------------------------------------------------------
    // UpdateDetails
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateDetails_ValidData_UpdatesAllFieldsAndReturnsSuccess()
    {
        // Arrange
        var item = BudgetItem.Create(ProjectId, "Centro Antigo", "Descrição Antiga", "un", 1m, 10m, null).Value!;

        // Act
        var result = item.UpdateDetails("Centro Novo", "Descrição Nova", "kg", 2m, 20m, "COD-NOVO");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Centro Novo", item.CostCenter);
        Assert.Equal("Descrição Nova", item.Description);
        Assert.Equal("kg", item.Unit);
        Assert.Equal(2m, item.Quantity);
        Assert.Equal(20m, item.UnitPrice.Amount);
        Assert.Equal("COD-NOVO", item.SinapiCode);
    }

    [Fact]
    public void UpdateDetails_NonPositiveQuantity_ReturnsValidationFailureAndKeepsOriginalQuantity()
    {
        // Arrange
        var item = BudgetItem.Create(ProjectId, "Centro", "Descrição", "un", 5m, 10m, null).Value!;

        // Act
        var result = item.UpdateDetails("Centro", "Descrição", "un", 0m, 10m, null);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(5m, item.Quantity);
    }

    // -------------------------------------------------------------------------
    // Remove
    // -------------------------------------------------------------------------

    [Fact]
    public void Remove_ActiveItem_SetsStatusToRemoved()
    {
        // Arrange
        var item = BudgetItem.Create(ProjectId, "Centro", "Descrição", "un", 1m, 10m, null).Value!;

        // Act
        item.Remove();

        // Assert
        Assert.Equal(BudgetItemStatus.Removed, item.Status);
    }
}
