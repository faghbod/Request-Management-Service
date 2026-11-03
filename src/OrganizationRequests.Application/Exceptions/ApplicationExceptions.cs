namespace OrganizationRequests.Application.Exceptions;

// خطاهای کاربردی مورد استفاده فرایندهای برنامه.
public sealed class RequestValidationException(string message) : Exception(message);
public sealed class ResourceNotFoundException(string message) : Exception(message);
public sealed class AccessDeniedException(string message) : Exception(message);

