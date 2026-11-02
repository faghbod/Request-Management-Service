using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Services;

namespace OrganizationRequests.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/requests")]
public sealed class RequestsController(IRequestRegistrationService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<RequestResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<RequestResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestResponseDto>> Register(RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await service.RegisterAsync(request, cancellationToken);
        return result.IsNewlyCreated
            ? CreatedAtAction(nameof(GetById), new { id = result.Request.Id }, result.Request)
            : Ok(result.Request);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<RequestResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestResponseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var request = await service.GetByIdAsync(id, cancellationToken);
        return request is null ? NotFound() : Ok(request);
    }
}
