using OrganizationRequests.Application.Repositories;
using OrganizationRequests.Domain.Entities;
using OrganizationRequests.Domain.Entities.Enums;

namespace OrganizationRequests.Application.Services;

public sealed class ExpertSelectionService(IExpertRepository repository) : IExpertSelectionService
{
    public async Task<Expert?> SelectBestExpertAsync(RequestSubject subject, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(subject))
            throw new ArgumentOutOfRangeException(nameof(subject), subject, "Subject is invalid.");

        var candidates = await repository.GetEligibleExpertsAsync(subject, cancellationToken);
        Expert? best = null;

        foreach (var candidate in candidates)
        {
            if (best is null)
            {
                best = candidate;
                continue;
            }

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

        return best;
    }
}
