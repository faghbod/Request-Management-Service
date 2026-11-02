namespace OrganizationRequests.Application.Exceptions;

public sealed class ExpertValidationException(string message) : Exception(message);
public sealed class ExpertConcurrencyException() : Exception("The expert was modified concurrently.");
