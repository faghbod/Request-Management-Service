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

        #region مقایسه بار کاری

        foreach (var candidate in candidates)
        {
            if (best is null)
            {
                best = candidate;
                continue;
            }

            // مقایسه ضربی نسبت‌ها، محاسبه را بدون تقسیم اعشاری انجام می‌دهد.
            // در نسبت برابر، تعداد درخواست کمتر و سپس شناسه کمتر اولویت دارد.
            var candidateRatio = (long)candidate.ActiveRequestCount * best.Capacity;
            var bestRatio = (long)best.ActiveRequestCount * candidate.Capacity;
            if (candidateRatio < bestRatio ||
                (candidateRatio == bestRatio &&
                 (candidate.ActiveRequestCount < best.ActiveRequestCount ||
                  (candidate.ActiveRequestCount == best.ActiveRequestCount && candidate.Id < best.Id))))
            {
                best = candidate;
            }
        }

        #endregion

        return best;
    }
}
