using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.IServiceContracts;

// قرارداد ایجاد، مشاهده و ویرایش کارشناس.
public interface IExpertManagementService
{
    Task<ExpertResponseDto> CreateExpertAsync(CreateExpertDto input, CancellationToken cancellationToken = default);

    Task<ExpertResponseDto> GetExpertByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ExpertManagementPage> GetExpertsAsync(int? afterId, int pageSize, bool? isActive, RequestSubject? subject, CancellationToken cancellationToken = default);

    Task<ExpertResponseDto> UpdateExpertAsync(int id, UpdateExpertDto input, CancellationToken cancellationToken = default);

    Task<ExpertResponseDto> SetExpertActiveStatusAsync(int id, SetExpertActiveStatusDto input, CancellationToken cancellationToken = default);
}
