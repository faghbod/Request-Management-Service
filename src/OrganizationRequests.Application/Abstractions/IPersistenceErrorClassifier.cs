namespace OrganizationRequests.Application.Abstractions;

public interface IPersistenceErrorClassifier
{
    bool IsRequestKeyUniqueViolation(Exception exception);
}
