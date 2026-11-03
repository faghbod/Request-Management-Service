using System.Globalization;
using OrganizationRequests.Application.Abstractions;

namespace OrganizationRequests.Api.Security;

// استخراج شناسه‌های کاربر و کارشناس از زمینه درخواست HTTP.
public sealed class HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) : ICurrentUserContext
{
    public int? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.Identities
                .Where(identity => identity.IsAuthenticated)
                .SelectMany(identity => identity.FindAll("user_id"))
                .Select(claim => claim.Value).FirstOrDefault();
            return int.TryParse(value, out var userId) && userId != 0 ? userId : null;
        }
    }

    public int? ExpertId
    {
        get
        {
            // این گزینه فقط پس از اطمینان از کنترل این Claim توسط صادرکننده معتبر فعال شود.
            if (!bool.TryParse(configuration["Jwt:TrustExpertIdClaim"], out var trusted) || !trusted)
                return null;
            var identities = httpContextAccessor.HttpContext?.User.Identities
                .Where(identity => identity.IsAuthenticated).ToArray();
            if (identities is not { Length: 1 }) return null;
            var claims = identities[0].FindAll("expert_id").ToArray();
            return claims is { Length: 1 } && int.TryParse(claims[0].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var expertId) && expertId > 0 ? expertId : null;
        }
    }
}
