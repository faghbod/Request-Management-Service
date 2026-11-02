namespace OrganizationRequests.Application.Exceptions;

public sealed class RequestKeyConflictException() : Exception("The request key cannot be used for this registration.");
