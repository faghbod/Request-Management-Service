namespace OrganizationRequests.Application.Abstractions;

// قرارداد تشخیص خطاهای ذخیره‌سازی بدون وابستگی Application به دیتابیس.
public interface IPersistenceErrorClassifier
{
    bool IsRequestKeyUniqueViolation(Exception exception);
    bool IsOptimisticConcurrencyConflict(Exception exception);
}
