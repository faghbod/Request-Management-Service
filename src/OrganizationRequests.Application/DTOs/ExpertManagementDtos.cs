using System.Text.Json.Serialization;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.DTOs;

// مدل‌های ورودی و خروجی عملیات مدیریت کارشناس.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CreateExpertDto
{
    public string? Name { get; set; }
    public int Capacity { get; set; }
    public bool? IsActive { get; set; }
    public IReadOnlyCollection<RequestSubject>? Specialties { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateExpertDto
{
    public string? Name { get; set; }
    public int Capacity { get; set; }
    public bool? IsActive { get; set; }
    public IReadOnlyCollection<RequestSubject>? Specialties { get; set; }
    public string? ExpectedRowVersion { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SetExpertActiveStatusDto
{
    public bool? IsActive { get; set; }
    public string? ExpectedRowVersion { get; set; }
}

public sealed record ExpertResponseDto(int Id, string Name, int Capacity, int ActiveRequestCount,
    int AvailableCapacity, bool IsActive, IReadOnlyList<RequestSubject> Specialties, string RowVersion, DateTime CreatedOn);
public sealed record ExpertManagementPage(IReadOnlyList<ExpertResponseDto> Items, int? NextAfterId);
