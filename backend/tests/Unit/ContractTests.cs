using Construcheck.Construction.Domain.Contracts;
using Construcheck.SharedKernel;

namespace Construcheck.Unit.Tests.Construction.Domain.Contracts;

public class ContractTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_ValidData_ReturnsSuccessWithActiveStatus()
    {
        // Act
        var result = Contract.Create(
            ProjectId, ContractType.Contractor, "Empreiteira ABC", 50000m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Fulano");

        // Assert
        Assert.True(result.IsSuccess);
        var contract = result.Value!;
        Assert.Equal(ProjectId, contract.ProjectId);
        Assert.Equal(ContractType.Contractor, contract.Type);
        Assert.Equal("Empreiteira ABC", contract.CounterpartyName);
        Assert.Equal(50000m, contract.Value.Amount);
        Assert.Equal(ContractStatus.Active, contract.Status);
    }

    [Theory]
    // Zero não é positivo; Negativo
    [InlineData(0)]
    [InlineData(-100)]
    public void Create_NonPositiveValue_ReturnsValidationFailure(decimal value)
    {
        // Act
        var result = Contract.Create(
            ProjectId, ContractType.Supplier, "Fornecedor X", value,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Fulano");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("O valor deve ser maior que zero.", result.Error);
    }

    [Fact]
    public void Create_DueDateBeforeStartDate_ReturnsValidationFailure()
    {
        // Act
        var result = Contract.Create(
            ProjectId, ContractType.Equipment, "Locadora Y", 1000m,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1), "Fulano");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
        Assert.Equal("A data final não pode ser anterior à data inicial.", result.Error);
    }

    [Fact]
    public void Create_InvalidMoneyAndInvalidDateRangeTogether_ReturnsMoneyValidationFirst()
    {
        // Arrange — combinação de duas violações simultâneas: Money é validado antes
        // de DateRange no código-fonte, então a mensagem de erro deve refletir isso.

        // Act
        var result = Contract.Create(
            ProjectId, ContractType.Contractor, "Empreiteira Z", -1m,
            new DateOnly(2026, 12, 1), new DateOnly(2026, 1, 1), "Fulano");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("O valor deve ser maior que zero.", result.Error);
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
        var contract = Contract.Reconstitute(
            id, ProjectId, ContractType.Equipment, "Locadora W", 2500m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 1), "Ciclano",
            ContractStatus.Removed, DateTime.UtcNow, DateTime.UtcNow);

        // Assert
        Assert.Equal(id, contract.Id);
        Assert.Equal(ContractStatus.Removed, contract.Status);
        Assert.Equal(2500m, contract.Value.Amount);
    }

    // -------------------------------------------------------------------------
    // UpdateDetails
    // -------------------------------------------------------------------------

    [Fact]
    public void UpdateDetails_ValidData_UpdatesAllFieldsAndReturnsSuccess()
    {
        // Arrange
        var contract = Contract.Create(
            ProjectId, ContractType.Contractor, "Nome Antigo", 1000m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Responsável Antigo").Value!;

        // Act
        var result = contract.UpdateDetails(
            ContractType.Supplier, "Nome Novo", 2000m,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 7, 1), "Responsável Novo");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ContractType.Supplier, contract.Type);
        Assert.Equal("Nome Novo", contract.CounterpartyName);
        Assert.Equal(2000m, contract.Value.Amount);
        Assert.Equal("Responsável Novo", contract.Responsible);
    }

    [Fact]
    public void UpdateDetails_NonPositiveValue_ReturnsValidationFailureAndKeepsOriginalValue()
    {
        // Arrange
        var contract = Contract.Create(
            ProjectId, ContractType.Contractor, "Nome", 1000m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Responsável").Value!;

        // Act
        var result = contract.UpdateDetails(
            ContractType.Contractor, "Nome", 0m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Responsável");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(1000m, contract.Value.Amount);
    }

    // -------------------------------------------------------------------------
    // Remove
    // -------------------------------------------------------------------------

    [Fact]
    public void Remove_ActiveContract_SetsStatusToRemoved()
    {
        // Arrange
        var contract = Contract.Create(
            ProjectId, ContractType.Contractor, "Nome", 1000m,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1), "Responsável").Value!;

        // Act
        contract.Remove();

        // Assert
        Assert.Equal(ContractStatus.Removed, contract.Status);
    }
}
