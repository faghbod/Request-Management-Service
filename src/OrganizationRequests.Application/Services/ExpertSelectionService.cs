using OrganizationRequests.Application.IServiceContracts;
using OrganizationRequests.Domain.IRepositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Services;

// انتخاب کارشناس با کمترین نسبت بار به ظرفیت از میان افراد واجد شرایط.
public sealed class ExpertSelectionService(IExpertRepository repository) : IExpertSelectionService
{
    public async Task<Expert?> SelectBestExpertAsync(RequestSubject subject, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(subject))
            throw new ArgumentOutOfRangeException(nameof(subject), subject, "Subject is invalid.");

        #region دریافت کارشناسان واجد شرایط

        var candidates = await repository.GetEligibleExpertsAsync(subject, cancellationToken);
        Expert? best = null;

        #endregion

        foreach (var candidate in candidates)
        {
            if (best is null)
            {
                best = candidate;
                continue;
            }

            #region مقایسه نسبت ظرفیت مصرف‌شده

            // معیار اول همچنان نسبت کمتر است؛ ضرب متقاطع به تقسیم اعشاری نیاز ندارد.
            var candidateRatio = (long)candidate.ActiveRequestCount * best.Capacity;
            var bestRatio = (long)best.ActiveRequestCount * candidate.Capacity;

            if (candidateRatio != bestRatio)
            {
                if (candidateRatio < bestRatio)
                    best = candidate;

                continue;
            }

            #endregion

            #region مقایسه ظرفیت باقی‌مانده

            // در نسبت برابر، گزینه دارای جای خالی بیشتر انتخاب می‌شود.
            var candidateAvailableCapacity = candidate.Capacity - candidate.ActiveRequestCount;
            var bestAvailableCapacity = best.Capacity - best.ActiveRequestCount;

            if (candidateAvailableCapacity != bestAvailableCapacity)
            {
                if (candidateAvailableCapacity > bestAvailableCapacity)
                    best = candidate;

                continue;
            }

            #endregion

            #region مقایسه آخرین تخصیص فعال

            // جدیدترین تخصیص میان درخواست‌های باز هر کارشناس را می‌گیریم؛ زمان قدیمی‌تر اولویت دارد.
            var candidateLastAssignment = GetLatestActiveAssignmentAt(candidate);
            var bestLastAssignment = GetLatestActiveAssignmentAt(best);
            var assignmentComparison = Nullable.Compare(candidateLastAssignment, bestLastAssignment);

            // نبود درخواست باز از زمان موجود جلوتر است؛ تساوی کامل با شناسه کمتر حل می‌شود.
            if (assignmentComparison < 0 ||
                (assignmentComparison == 0 && candidate.Id < best.Id))
            {
                best = candidate;
            }

            #endregion
        }

        return best;
    }

    #region استخراج زمان مقایسه

    // درخواست‌های بسته یا حذف‌شده در این معیار دخالت ندارند و موضوع درخواست محدود نمی‌شود.
    private static DateTime? GetLatestActiveAssignmentAt(Expert expert) =>
        expert.AssignedRequests
            .Where(request => !request.IsDeleted && request.Status == RequestStatus.Assigned && request.ClosedAt is null)
            .Max(request => request.AssignedAt);

    #endregion
}
