using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Domain.Entities.Enums;
using OrganizationRequests.Infrastructure.Persistence;
using Xunit;

namespace OrganizationRequests.Tests.Integration;

internal static class DatabaseInvariants
{
    public static async Task AssertAsync(ApplicationDbContext db)
    {
        var experts = await db.Experts.AsNoTracking().ToListAsync();
        var requests = await db.ServiceRequests.AsNoTracking().ToListAsync();
        Assert.Equal(requests.Count, requests.Select(x => x.RequestKey).Distinct().Count());
        foreach (var expert in experts)
        {
            Assert.InRange(expert.ActiveRequestCount, 0, expert.Capacity);
            Assert.Equal(requests.Count(x => x.AssignedExpertId == expert.Id && x.Status == RequestStatus.Assigned), expert.ActiveRequestCount);
        }
        foreach (var request in requests)
        {
            switch (request.Status)
            {
                case RequestStatus.PendingAssignment:
                    Assert.Null(request.AssignedExpertId); Assert.Null(request.AssignedAt); Assert.Null(request.ClosedAt); break;
                case RequestStatus.Assigned:
                    Assert.NotNull(request.AssignedExpertId); Assert.NotNull(request.AssignedAt); Assert.Null(request.ClosedAt); break;
                case RequestStatus.Closed:
                    Assert.NotNull(request.AssignedExpertId); Assert.NotNull(request.AssignedAt); Assert.NotNull(request.ClosedAt);
                    Assert.True(request.ClosedAt >= request.AssignedAt); break;
                default: throw new Xunit.Sdk.XunitException($"Unexpected request status: {request.Status}");
            }
        }
    }
}
