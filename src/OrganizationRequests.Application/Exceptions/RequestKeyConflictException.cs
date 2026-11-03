namespace OrganizationRequests.Application.Exceptions;

// خطای استفاده ناسازگار از کلید ثبت درخواست.
public sealed class RequestKeyConflictException() : Exception("The request key cannot be used for this registration.");
