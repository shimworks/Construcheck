using Construcheck.API.Extensions;
using Construcheck.Construction.Application.Schedule.DTOs;
using Construcheck.Construction.Application.Schedule.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Construcheck.API.Controllers.Schedule;

[ApiController]
[Route("api")]
[Authorize]
public class ScheduleController(IScheduleApplicationService scheduleService) : ControllerBase
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

    [HttpDelete("phases/{phaseId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemovePhase(Guid phaseId, CancellationToken ct)
    {
        var result = await scheduleService.RemovePhaseAsync(phaseId, ct);
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
    public async Task<IActionResult> UpdateActivityDetails(Guid id, [FromBody] UpdateActivityDetailsRequest request, CancellationToken ct)
    {
        var result = await scheduleService.UpdateActivityDetailsAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    [HttpDelete("activities/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveActivity(Guid id, CancellationToken ct)
    {
        var result = await scheduleService.RemoveActivityAsync(id, ct);
        return result.ToActionResult(this);
    }

    [HttpPatch("activities/{id:guid}/iniciar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> StartActivity(Guid id, CancellationToken ct)
    {
        var result = await scheduleService.StartActivityAsync(id, ct);
        return result.ToActionResult(this);
    }

    [HttpPatch("activities/{id:guid}/concluir")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CompleteActivity(Guid id, CancellationToken ct)
    {
        var result = await scheduleService.CompleteActivityAsync(id, ct);
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
    public async Task<IActionResult> AddPredecessor(Guid id, [FromBody] AddPredecessorRequest request, CancellationToken ct)
    {
        var result = await scheduleService.AddPredecessorAsync(id, request, ct);
        return result.ToActionResult(this);
    }
}
