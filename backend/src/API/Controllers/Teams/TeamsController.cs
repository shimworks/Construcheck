using Construcheck.API.Extensions;
using Construcheck.Construction.Application.Teams.DTOs;
using Construcheck.Construction.Application.Teams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Construcheck.API.Controllers.Teams;

[ApiController]
[Route("api")]
[Authorize]
public class TeamsController(ITeamApplicationService teamService) : ControllerBase
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
        var result = await teamService.RemoveAsync(id, ct);
        return result.ToActionResult(this);
    }
}
