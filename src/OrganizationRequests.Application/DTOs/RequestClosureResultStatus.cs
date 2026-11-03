namespace OrganizationRequests.Application.DTOs;

// حالت‌های ممکن نتیجه بستن درخواست.
public enum RequestClosureResultStatus
{
    Closed,
    AlreadyClosed,
    InvalidState,
    RequestNotFound,
    RetryableConflict,
    DataIntegrityFailure
}
