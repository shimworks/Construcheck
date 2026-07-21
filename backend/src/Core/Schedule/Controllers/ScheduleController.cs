using Construcheck.Core.Schedule.DTOs;
using Construcheck.Core.Schedule.Interfaces;
using Construcheck.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Construcheck.Core.Schedule.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class ScheduleController(IScheduleService scheduleService) : ControllerBase
{
    [HttpPost("projects/{projectId:guid}/schedule/seed")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Seed(Guid projectId, CancellationToken ct)
    {
        var result = await scheduleService.SeedDefaultWbsAsync(projectId, ct);
        return result.ToActionResult(this);
    }

    [HttpGet("projects/{projectId:guid}/schedule")]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
    {
        var result = await scheduleService.GetByProjectIdAsync(projectId, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("projects/{projectId:guid}/schedule/phases")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreatePhase(Guid projectId, [FromBody] CreateSchedulePhaseRequest request, CancellationToken ct)
    {
        var result = await scheduleService.CreatePhaseAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("phases/{phaseId:guid}/activities")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateActivity(Guid phaseId, [FromBody] CreateActivityRequest request, CancellationToken ct)
    {
        var result = await scheduleService.CreateActivityAsync(phaseId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPut("activities/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateActivity(Guid id, [FromBody] UpdateActivityRequest request, CancellationToken ct)
    {
        var result = await scheduleService.UpdateActivityAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPatch("phases/{phaseId:guid}/activities/reorder")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reorder(Guid phaseId, [FromBody] ReorderActivitiesRequest request, CancellationToken ct)
    {
        var result = await scheduleService.ReorderActivitiesAsync(phaseId, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("activities/{id:guid}/dependencies")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddDependency(Guid id, [FromBody] CreateDependencyRequest request, CancellationToken ct)
    {
        var result = await scheduleService.AddDependencyAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPost("projects/{projectId:guid}/milestones")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateMilestone(Guid projectId, [FromBody] CreateMilestoneRequest request, CancellationToken ct)
    {
        var result = await scheduleService.CreateMilestoneAsync(projectId, request, ct);
        return result.ToActionResult(this);
    }
}