using Construcheck.Core.Contracts.DTOs;
using Construcheck.Core.Contracts.Interfaces;
using Construcheck.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Construcheck.Core.Contracts.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class ContractsController(IContractService contractService) : ControllerBase
{
    [HttpPost("projects/{projectId:guid}/contracts")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateContractRequest request, CancellationToken ct)
    {
        var result = await contractService.CreateAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpGet("projects/{projectId:guid}/contracts")]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
    {
        var result = await contractService.GetByProjectIdAsync(projectId, ct);
        return result.ToActionResult(this);
    }

    [HttpGet("contracts/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await contractService.GetByIdAsync(id, ct);
        return result.ToActionResult(this);
    }

    [HttpPut("contracts/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContractRequest request, CancellationToken ct)
    {
        var result = await contractService.UpdateAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("contracts/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await contractService.DeleteAsync(id, ct);
        return result.ToActionResult(this);
    }
}