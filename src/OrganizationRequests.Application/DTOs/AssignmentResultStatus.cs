namespace OrganizationRequests.Application.DTOs;

public enum AssignmentResultStatus
{
    Assigned,
    AlreadyProcessed,
    NoEligibleExpert,
    RequestNotFound,
    RetryableConflict
}
