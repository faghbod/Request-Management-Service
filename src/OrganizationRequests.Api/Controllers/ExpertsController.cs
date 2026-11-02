using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Services;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Api.Controllers;

[ApiController]
[Authorize(Policy = "ExpertsManage")]
[Route("api/experts")]
public sealed class ExpertsController(IExpertManagementService experts) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ExpertResponseDto>> Create(CreateExpertDto input, CancellationToken cancellationToken)
    {
        var result = await experts.CreateExpertAsync(input, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExpertResponseDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await experts.GetExpertByIdAsync(id, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<ExpertManagementPage>> GetPage([FromQuery] int? afterId, [FromQuery] int pageSize = 50,
        [FromQuery] bool? isActive = null, [FromQuery] RequestSubject? subject = null, CancellationToken cancellationToken = default) =>
        Ok(await experts.GetExpertsAsync(afterId, pageSize, isActive, subject, cancellationToken));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ExpertResponseDto>> Update(int id, UpdateExpertDto input, CancellationToken cancellationToken) =>
        Ok(await experts.UpdateExpertAsync(id, input, cancellationToken));

    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ExpertResponseDto>> SetStatus(int id, SetExpertActiveStatusDto input, CancellationToken cancellationToken) =>
        Ok(await experts.SetExpertActiveStatusAsync(id, input, cancellationToken));
}
