namespace OrganizationRequests.Application.Abstractions;

// قرارداد دریافت هویت کاربر و کارشناس جاری.
public interface ICurrentUserContext
{
    int? UserId { get; }
    int? ExpertId { get; }
}

