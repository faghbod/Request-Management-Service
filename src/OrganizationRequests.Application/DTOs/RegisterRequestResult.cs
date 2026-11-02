namespace OrganizationRequests.Application.DTOs;

public sealed record RegisterRequestResult(RequestResponseDto Request, bool IsNewlyCreated);
