using System.Security.Claims;
using OrganizationRequests.Application.Abstractions;

namespace OrganizationRequests.Api.Security;

public sealed class HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    public int? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue("user_id");
            return int.TryParse(value, out var userId) && userId != 0 ? userId : null;
        }
    }
}

