namespace OrganizationRequests.Application.DTOs;

// اطلاعات درخواست و مشخص بودن تازه‌ساخته‌شدن آن.
public sealed record RegisterRequestResult(RequestResponseDto Request, bool IsNewlyCreated);
