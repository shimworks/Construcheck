using Construcheck.API.Extensions;
using Construcheck.Construction.Application.Budget.DTOs;
using Construcheck.Construction.Application.Budget.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Construcheck.API.Controllers.Budget;

[ApiController]
[Route("api")]
[Authorize]
public class BudgetController(IBudgetApplicationService budgetService) : ControllerBase
{
    [HttpPost("projects/{projectId:guid}/budget/items")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateBudgetItemRequest request, CancellationToken ct)
    {
        var result = await budgetService.CreateAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpGet("projects/{projectId:guid}/budget")]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
    {
        var result = await budgetService.GetByProjectIdAsync(projectId, ct);
        return result.ToActionResult(this);
    }

    [HttpPut("budget/items/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBudgetItemRequest request, CancellationToken ct)
    {
        var result = await budgetService.UpdateAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("budget/items/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await budgetService.RemoveAsync(id, ct);
        return result.ToActionResult(this);
    }
}
