using Construcheck.Core.Teams.DTOs;
using Construcheck.Core.Teams.Interfaces;
using Construcheck.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Construcheck.Core.Teams.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class TeamsController(ITeamService teamService) : ControllerBase
{
    [HttpPost("projects/{projectId:guid}/teams")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateTeamRequest request, CancellationToken ct)
    {
        var result = await teamService.CreateAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpGet("projects/{projectId:guid}/teams")]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
    {
        var result = await teamService.GetByProjectIdAsync(projectId, ct);
        return result.ToActionResult(this);
    }

    [HttpPut("teams/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTeamRequest request, CancellationToken ct)
    {
        var result = await teamService.UpdateAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("teams/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await teamService.DeleteAsync(id, ct);
        return result.ToActionResult(this);
    }
}