using Construcheck.API.Extensions;
using Construcheck.Construction.Application.Contracts.DTOs;
using Construcheck.Construction.Application.Contracts.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Construcheck.API.Controllers.Contracts;

[ApiController]
[Route("api")]
[Authorize]
public class ContractsController(IContractApplicationService contractService) : ControllerBase
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
        var result = await contractService.RemoveAsync(id, ct);
        return result.ToActionResult(this);
    }
}
