using OrganizationRequests.Application.IServiceContracts;
using Microsoft.Extensions.Logging;
using OrganizationRequests.Application.Abstractions;
using OrganizationRequests.Application.DTOs;
using OrganizationRequests.Application.Exceptions;
using OrganizationRequests.Application.Mapping;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Domain.UnitOfWork;

namespace OrganizationRequests.Application.Services;

// ثبت درخواست و مدیریت تکرار یک عملیات با کلید یکسان.
public sealed class RequestRegistrationService(
    IServiceRequestRepository repository,
    IUnitOfWork unitOfWork,
    IPersistenceErrorClassifier errorClassifier,
    ICurrentUserContext currentUser,
    IRequestHistoryService history,
    ILogger<RequestRegistrationService> logger) : IRequestRegistrationService
{
    public async Task<RegisterRequestResult> RegisterAsync(RegisterRequestDto input, CancellationToken cancellationToken = default)
    {
        #region اعتبارسنجی و بررسی درخواست تکراری

        // سرویس مستقل از اعتبارسنجی API نیز از ورودی معتبر اطمینان می‌گیرد.
        if (input is null || input.RequestKey == Guid.Empty || !Enum.IsDefined(input.Subject) || !Enum.IsDefined(input.Priority))
            throw new RequestValidationException("The request key, subject or priority is invalid.");

        var userId = GetUserId();
        var existing = await repository.GetByRequestKeyAsync(input.RequestKey, cancellationToken);
        if (existing is not null)
            return Replay(existing, input, userId);

        #endregion

        #region ثبت درخواست و تاریخچه

        var request = input.ToEntity();

        // هر دو موجودیت در یک SaveChanges ذخیره می‌شوند.
        await repository.AddAsync(request, cancellationToken);
        try
        {
            await history.RecordRegistrationAsync(request, userId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // محدودیت یکتای دیتابیس رقابت دو ثبت هم‌زمان را پوشش می‌دهد.
            repository.DiscardAdded(request);
            if (!errorClassifier.IsRequestKeyUniqueViolation(exception))
                throw;

            existing = await repository.GetByRequestKeyAsync(input.RequestKey, cancellationToken);
            if (existing is null)
                throw;

            logger.LogInformation("Recovered a concurrent request registration.");
            return Replay(existing, input, userId);
        }

        #endregion

        logger.LogInformation("Request {RequestId} registered.", request.Id);
        return request.ToRegistrationResult(true);
    }

    public async Task<RequestResponseDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var request = await repository.GetByIdAsync(id, cancellationToken);
        return request is not null && (request.CreatedBy == userId ||
            (currentUser.ExpertId is int expertId && expertId > 0 && request.AssignedExpertId == expertId))
            ? request.ToResponseDto() : null;
    }

    private RegisterRequestResult Replay(ServiceRequest existing, RegisterRequestDto input, int userId)
    {
        // یک کلید فقط برای همان کاربر و همان محتوای ثبت قابل استفاده مجدد است.
        if (existing.IsDeleted || existing.CreatedBy != userId || existing.Subject != input.Subject || existing.Priority != input.Priority)
        {
            logger.LogInformation("Request key conflict detected.");
            throw new RequestKeyConflictException();
        }

        logger.LogInformation("Existing request {RequestId} replayed.", existing.Id);
        return existing.ToRegistrationResult(false);
    }

    private int GetUserId() => currentUser.UserId is int userId && userId != 0
        ? userId
        : throw new AccessDeniedException("A valid user identity is required.");

}
