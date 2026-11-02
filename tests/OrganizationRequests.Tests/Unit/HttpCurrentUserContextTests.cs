using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using OrganizationRequests.Api.Security;
using Xunit;

namespace OrganizationRequests.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class HttpCurrentUserContextTests
{
    [Fact]
    public void Authenticated_user_and_trusted_expert_are_separate()
    {
        var context = Create(true, true, new Claim("user_id", "101"), new Claim("expert_id", "7"));
        Assert.Equal(101, context.UserId);
        Assert.Equal(7, context.ExpertId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    public void Invalid_or_missing_expert_claim_is_rejected(string? value)
    {
        var claims = value is null ? Array.Empty<Claim>() : [new Claim("expert_id", value)];
        Assert.Null(Create(true, true, claims).ExpertId);
    }

    [Fact]
    public void Duplicate_expert_claims_are_ambiguous_even_when_equal()
    {
        Assert.Null(Create(true, true, new Claim("expert_id", "7"), new Claim("expert_id", "7")).ExpertId);
        Assert.Null(Create(true, true, new Claim("expert_id", "7"), new Claim("expert_id", "8")).ExpertId);
    }

    [Fact]
    public void Unauthenticated_or_untrusted_principal_cannot_supply_an_expert()
    {
        var claims = new[] { new Claim("user_id", "101"), new Claim("expert_id", "7") };
        var anonymous = Create(false, true, claims);
        Assert.Null(anonymous.UserId);
        Assert.Null(anonymous.ExpertId);
        Assert.Null(Create(true, false, claims).ExpertId);
    }

    [Fact]
    public void Claims_on_an_unauthenticated_secondary_identity_are_ignored()
    {
        var principal = new ClaimsPrincipal(new[] { new ClaimsIdentity([new Claim("user_id", "101")], "test"),
            new ClaimsIdentity([new Claim("expert_id", "7")]) });
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:TrustExpertIdClaim"] = "true" }).Build();
        Assert.Null(new HttpCurrentUserContext(accessor, config).ExpertId);
    }

    [Fact]
    public void Multiple_authenticated_identities_are_ambiguous()
    {
        var principal = new ClaimsPrincipal(new[] { new ClaimsIdentity([new Claim("user_id", "101")], "first"),
            new ClaimsIdentity([new Claim("expert_id", "7")], "second") });
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:TrustExpertIdClaim"] = "true" }).Build();
        Assert.Null(new HttpCurrentUserContext(accessor, config).ExpertId);
    }
    private static HttpCurrentUserContext Create(bool authenticated, bool trusted, params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, authenticated ? "test" : null);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Jwt:TrustExpertIdClaim"] = trusted.ToString() }).Build();
        return new HttpCurrentUserContext(accessor, config);
    }
}
