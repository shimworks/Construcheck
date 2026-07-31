using Construcheck.API.Extensions;
using Construcheck.Construction.Application.Projects.DTOs;
using Construcheck.Construction.Application.Projects.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Construcheck.API.Controllers.Projects;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController(IProjectApplicationService projectService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var result = await projectService.CreateAsync(request, ct);
        return result.ToActionResult(this);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await projectService.GetAllAsync(ct);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await projectService.GetByIdAsync(id, ct);
        return result.ToActionResult(this);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        var result = await projectService.UpdateAsync(id, request, ct);
        return result.ToActionResult(this);
    }

    [HttpPatch("{id:guid}/arquivar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var result = await projectService.ArchiveAsync(id, ct);
        return result.ToActionResult(this);
    }
}
