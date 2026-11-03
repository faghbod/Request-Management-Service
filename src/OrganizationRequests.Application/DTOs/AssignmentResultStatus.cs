namespace OrganizationRequests.Application.DTOs;

// حالت‌های ممکن نتیجه تلاش تخصیص.
public enum AssignmentResultStatus
{
    Assigned,
    AlreadyProcessed,
    NoEligibleExpert,
    RequestNotFound,
    RetryableConflict
}
