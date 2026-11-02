# OrganizationRequests

سامانه ثبت درخواست سازمانی و تخصیص خودکار آن به کارشناس، بر پایه معماری Clean ساده، Repository و UnitOfWork است.

## ساختار

- `Domain`: موجودیت‌ها، enumها و قرارداد UnitOfWork.
- `Application`: قوانین ثبت، صف، انتخاب و تخصیص و بستن درخواست؛ وابسته به Domain و قراردادها.
- `Infrastructure`: پیاده‌سازی Repository و UnitOfWork با EF Core و SQL Server، پیکربندی و migrationها؛ وابسته به Application و Domain.
- `Api`: کنترلر، JWT و دریافت هویت کاربر؛ سرویس‌های Application و Infrastructure را ثبت می‌کند.
- `Worker`: پردازش مستقل صف با BackgroundService و scope جداگانه برای هر تلاش؛ سرویس‌های مشترک را از طریق DI ثبت می‌کند.
- `Tests`: آزمون‌های واحد، HTTP درون‌حافظه‌ای و آزمون‌های اختیاری SQL Server.

## جریان کار

کاربر احراز هویت‌شده درخواست Financial، Legal یا Technical ثبت می‌کند. `RequestKey` شناسه idempotency است: تکرار با همان کاربر و ورودی همان نتیجه را برمی‌گرداند و استفاده ناسازگار خطای 409 دارد. خواندن درخواست به سازنده آن محدود است.

Worker درخواست‌های PendingAssignment را صفحه‌بندی و بر اساس اولویت و Priority Aging پردازش می‌کند. کارشناس فعال با تخصص مناسب و ظرفیت خالی انتخاب می‌شود؛ کمترین نسبت بار به ظرفیت و سپس ترتیب قطعی ملاک است. تخصیص، شمارنده و تغییر وضعیت داخل تراکنش انجام می‌شوند؛ rowversion و به‌روزرسانی شرطی برای رقابت استفاده می‌شوند و retryها محدود و با scope تازه هستند. سرویس داخلی بستن درخواست وضعیت را Closed و ظرفیت را آزاد می‌کند و بستن دوباره نباید ظرفیت را دوباره آزاد کند. هنوز مسیر HTTP بستن درخواست وجود ندارد؛ برای ایجاد آن ابتدا باید رابطه معتبر هویت کاربر با کارشناس و مجوز مالکیت پیاده شود.

## پیش‌نیاز و راه‌اندازی محلی

.NET 10 SDK، SQL Server و دسترسی به NuGet لازم است. تنظیمات نمونه زیر را با مقادیر واقعی در محیط خود جایگزین کنید؛ هیچ credential یا کلید واقعی را commit نکنید. User Secrets برای توسعه است و رمزنگاری نمی‌شود.

1. `dotnet restore OrganizationRequests.sln`
2. رشته اتصال را به شکل امن برای API و Worker تنظیم کنید؛ مثلاً در محیط محلی مقدار `ConnectionStrings__DefaultConnection` را برای هر دو فرایند بگذارید.
3. یک کلید تصادفی قوی و محرمانه با حداقل ۳۲ بایت تولید و با User Secrets پروژه API ثبت کنید. مقدار زیر فقط placeholder است و باید تعویض شود:
   `dotnet user-secrets set "Jwt:Key" "<YOUR_RANDOM_SECRET>" --project src/OrganizationRequests.Api`
   تنظیمات توسعه شامل issuer و audience نمونه است؛ در سایر محیط‌ها `Jwt__Issuer` و `Jwt__Audience` را نیز تنظیم کنید. صدور توکن و نحوه رساندن آن به کاربر بر عهده ارائه‌دهنده هویت است.
4. پس از تنظیم connection string و JWT برای startup project، ابزار را با `dotnet tool restore` آماده و migrationها را **دستی** اعمال کنید:
   `dotnet ef database update --project src/OrganizationRequests.Infrastructure --startup-project src/OrganizationRequests.Api`
   قبل از اجرا مقصد را بررسی کنید. Worker خودکار migration اجرا نمی‌کند.
5. API: `dotnet run --project src/OrganizationRequests.Api`
6. Worker مستقل: `dotnet run --project src/OrganizationRequests.Worker`
7. تست‌های بدون SQL: `dotnet test OrganizationRequests.sln --filter "Category=Unit"`
8. تست‌های SQL: فقط یک دیتابیس اختصاصی خالی با نامی که با `OrganizationRequests_Test_` شروع می‌شود و migrationهای اعمال‌شده آماده کنید. متغیر `ORGANIZATIONREQUESTS_TEST_SQL` را برای همان دیتابیس تنظیم کنید و `dotnet test OrganizationRequests.sln --filter "Category=SqlServer"` را اجرا کنید. این تست‌ها داده درج می‌کنند و پاک‌سازی خودکار ندارند؛ هرگز دیتابیس واقعی را استفاده نکنید. تست کارایی نیز در همین دسته است. بدون متغیر اتصال، این تست‌ها skip می‌شوند.

## تنظیمات

| کلید | توضیح |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | اتصال SQL Server؛ در محیط از `ConnectionStrings__DefaultConnection` استفاده کنید. |
| `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience` | کلید و issuer/audience مورد اعتماد API؛ اعتبار امضا، الگوریتم HMAC، انقضا، issuer و audience بررسی می‌شود. claim عددی `user_id` لازم است. |
| `QueueProcessing:BatchSize` | اندازه صفحه، پیش‌فرض 50 و بازه 1 تا 500. |
| `QueueProcessing:AgingThresholdMinutes` | آستانه aging، پیش‌فرض 30 و حداقل 1. |
| `AssignmentWorker:PollingIntervalSeconds` | فاصله بین چرخه‌ها، پیش‌فرض 30 و حداقل 1. |
| `AssignmentWorker:MaxAssignmentAttempts` | تعداد کل تلاش‌ها، پیش‌فرض 3 و بازه 1 تا 10. |
| `AssignmentWorker:InitialRetryDelayMilliseconds`, `MaxRetryDelayMilliseconds` | پیش‌فرض 100 و 1000؛ غیرمنفی و مقدار بیشینه حداقل برابر اولیه است. |
| `Logging:LogLevel` | سطح ثبت برای دسته‌های لاگ؛ شامل تنظیمات مجزای API و Worker. |

## استقرار و محدودیت‌ها

API و Worker مستقل deploy و restart می‌شوند، ولی به همان SQL Server و تنظیمات معتبر دسترسی نیاز دارند. secrets را از secret manager یا provider امن محیط تهیه کنید؛ در production از کلید تصادفی و قوی و issuer/audience واقعی استفاده شود. کلیدی که قبلاً در فایل توسعه و تاریخچه Git بوده باید هرجا استفاده شده چرخانده شود؛ حذف فایل فعلی تاریخچه را پاک نمی‌کند. JWTهای صادرشده با کلید قبلی بعد از چرخش نامعتبر می‌شوند.

Migration مرحله‌ای جداگانه است. برای shutdown فرصت کافی برای لغو کار و rollback در نظر بگیرید و برای restart، خطاهای بحرانی، تأخیر صف، تعارض‌ها و سلامت SQL Server مانیتورینگ خارجی تنظیم کنید. endpoint اختصاصی health در این پروژه اضافه نشده است. لاگ خطا شامل exception است و باید با کنترل دسترسی و سیاست نگهداری مناسب محافظت شود. رفتار هم‌زمانی واقعی SQL Server، بازیابی پس از crash و کارایی صف با داده زیاد در محیط فعلی تأیید نشده‌اند. جزئیات و نتایج در `PHASE11_SECURITY_REPORT.md` و `PHASE10_TEST_REPORT.md` است.
