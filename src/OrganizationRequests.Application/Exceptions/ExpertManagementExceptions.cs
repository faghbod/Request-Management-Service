namespace OrganizationRequests.Application.Exceptions;

// خطاهای اعتبارسنجی و هم‌زمانی مدیریت کارشناس.
public sealed class ExpertValidationException(string message) : Exception(message);
public sealed class ExpertConcurrencyException() : Exception("The expert was modified concurrently.");
