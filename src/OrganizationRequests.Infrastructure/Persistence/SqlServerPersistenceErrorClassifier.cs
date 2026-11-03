using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrganizationRequests.Application.Abstractions;

namespace OrganizationRequests.Infrastructure.Persistence;

// تشخیص خطاهای یکتایی و هم‌زمانی مربوط به SQL Server.
public sealed class SqlServerPersistenceErrorClassifier : IPersistenceErrorClassifier
{
    public bool IsOptimisticConcurrencyConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
                return true;
        }

        return false;
    }

    public bool IsRequestKeyUniqueViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is not SqlException sqlException)
                continue;

            foreach (SqlError error in sqlException.Errors)
            {
                if (error.Number is 2601 or 2627 &&
                    error.Message.Contains("'UX_ServiceRequests_RequestKey'", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
