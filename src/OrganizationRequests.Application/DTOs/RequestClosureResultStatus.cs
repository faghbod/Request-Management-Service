namespace OrganizationRequests.Application.DTOs;

public enum RequestClosureResultStatus
{
    Closed,
    AlreadyClosed,
    InvalidState,
    RequestNotFound,
    RetryableConflict,
    DataIntegrityFailure
}
