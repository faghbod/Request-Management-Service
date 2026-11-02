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

کاربر احراز هویت‌شده درخواست Financial، Legal یا Technical ثبت می‌کند. `RequestKey` شناسه idempotency است: تکرار با همان کاربر و ورودی همان نتیجه را برمی‌گرداند و استفاده ناسازگار خطای 409 دارد. خواندن درخواست برای سازنده و کارشناس تخصیص‌یافته با هویت قابل‌اعتماد مجاز است.

Worker درخواست‌های PendingAssignment را صفحه‌بندی و بر اساس اولویت و Priority Aging پردازش می‌کند. کارشناس فعال با تخصص مناسب و ظرفیت خالی انتخاب می‌شود؛ کمترین نسبت بار به ظرفیت و سپس ترتیب قطعی ملاک است. تخصیص، شمارنده و تغییر وضعیت داخل تراکنش انجام می‌شوند؛ rowversion و به‌روزرسانی شرطی برای رقابت استفاده می‌شوند و retryها محدود و با scope تازه هستند. سرویس داخلی بستن درخواست وضعیت را Closed و ظرفیت را آزاد می‌کند و بستن دوباره نباید ظرفیت را دوباره آزاد کند. هنوز مسیر HTTP بستن درخواست وجود ندارد. سرویس داخلی فقط به کارشناس تخصیص‌یافته با هویت قابل‌اعتماد اجازه بستن می‌دهد.

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

## تاریخچه درخواست و فعالیت کارشناس

پس از اعمال دستی migration `AddRequestActivityHistory`، ثبت، تخصیص و بستن درخواست رویدادهای خود را در جدول `RequestActivityHistories` و در همان واحد کاری یا تراکنش مربوط ذخیره می‌کنند. درخواست و تاریخچه ثبت در یک `SaveChanges` درج می‌شوند؛ در تخصیص و بستن، درج تاریخچه پیش از commit تراکنش انجام می‌شود. برای داده‌های پیش از استقرار، رویداد قدیمی ساخته نمی‌شود؛ بنابراین تاریخچه فقط رویدادهای واقعاً ثبت‌شده پس از استقرار را پوشش می‌دهد. بازگرداندن migration به نسخه قبل جدول تاریخچه را حذف می‌کند و نیازمند بررسی و پشتیبان‌گیری است.

`GET /api/requests/{id}/history` به احراز هویت و همان بررسی مالکیت خواندن درخواست نیاز دارد؛ برای کاربری که نه سازنده است و نه کارشناس تخصیص‌یافته، 404 برمی‌گرداند. enumها مطابق تنظیمات موجود API به صورت عددی هستند. جست‌وجوی داخلی تاریخچه کارشناس با ترتیب نزولی و cursor `(OccurredAtUtc, Id)` و حداکثر اندازه صفحه 100 وجود دارد. به دلیل تأیید نشدن قرارداد صادرکننده هویت و نبود نقش مدیریتی موجود، endpoint عمومی آن ارائه نشده است. پیش از ارائه endpoint باید این رابطه و policy مجوز قابل‌اعتماد طراحی شوند.

`ExpertId` کارشناس مرتبط و `ActorUserId` کاربر واقعی انجام‌دهنده است؛ تخصیص Worker با actor سیستم و شناسه کاربر null ذخیره می‌شود. بستن درخواست در سرویس داخلی به user ID معتبر و ExpertId منطبق با کارشناس تخصیص‌یافته نیاز دارد. ایندکس یکتای `(ServiceRequestId, EventType)` به چرخه فعلی متکی است و پیش از افزودن تخصیص مجدد یا بازگشایی باید بازنگری شود. تاریخچه در گردش عادی برنامه فقط اضافه می‌شود و در برابر تغییر مستقیم با دسترسی مدیریتی دیتابیس مصون نیست.

تست SQL تاریخچه با `ORGANIZATIONREQUESTS_HISTORY_TEST_SQL` اجرا می‌شود و به دیتابیس **مجزای خالی** با نام شروع‌شونده با `OrganizationRequests_Test_` و همه migrationهای اعمال‌شده نیاز دارد؛ داده تست پاک نمی‌شود. سایر آزمون‌های SQL نیز برای جلوگیری از تداخل متغیر اتصال و دیتابیس مجزای خود را دارند؛ جزئیات در گزارش مرحله ۱۲ است.
## قرارداد هویت کارشناس

API فقط `expert_id` مثبت و یکتا را از هویت احراز هویت‌شده JWT می‌خواند؛ مقدار گمشده، تکراری، نامعتبر یا از هویت احرازنشده رد می‌شود. شناسه کاربر و شناسه کارشناس مستقل‌اند و شناسه کارشناس از ورودی درخواست یا مسیر استخراج نمی‌شود. چون قرارداد صادرکننده واقعی در پروژه تأیید نشده، `Jwt:TrustExpertIdClaim` به‌طور پیش‌فرض فعال نیست و در این حالت عملیات مخصوص کارشناس قابل دسترسی نیست.

فقط پس از اطمینان از اینکه صادرکننده JWT معتبر، انتساب هر `expert_id` به همان کاربر را کنترل می‌کند و هیچ کلاینتی نمی‌تواند مقدار آن را تعیین یا دستکاری کند، می‌توان مقدار `Jwt__TrustExpertIdClaim=true` را در تنظیمات امن محیط قرار داد. کنترل کلید امضا و چرخه ابطال توکن نیز با صادرکننده و محیط استقرار است. این تنظیم خودش اصالت رابطه کاربر و کارشناس را اثبات نمی‌کند. درخواست متعلق به دیگری فقط برای کارشناس تخصیص‌یافته قابل خواندن است؛ درخواست‌های نامرتبط 404 برمی‌گردانند. بدون expert claim قابل اعتماد، مالک همچنان می‌تواند درخواست خود را بخواند اما نمی‌تواند آن را در سرویس بستن ببندد.
## مدیریت کارشناسان

مسیرهای `POST /api/experts`، `GET /api/experts/{id}`، `GET /api/experts`، `PUT /api/experts/{id}` و `PATCH /api/experts/{id}/status` به مجوز مدیریتی نیاز دارند. فهرست با `afterId` و `pageSize` (پیش‌فرض 50، حداکثر 100) و فیلترهای اختیاری `isActive` و `subject` قابل دریافت است. برای جلوگیری از افشای اطلاعات، خواندن فهرست و جزئیات نیز همان مجوز را نیاز دارد. حذف کارشناس ارائه نشده است.

قرارداد مجوز: توکن JWT اعتبارسنجی‌شده باید claim با نام `permission` و مقدار دقیق `experts.manage` داشته باشد. اعتماد به این claim به‌صورت پیش‌فرض غیرفعال است. فقط پس از اطمینان از اینکه صادرکننده معتبر به‌طور انحصاری مجوز را اعطا می‌کند و کلاینت نمی‌تواند آن را برای خود صادر کند، تنظیم `Jwt__TrustExpertManagementPermission=true` را در محیط استقرار قرار دهید. صرف احراز هویت یا داشتن `expert_id` مجوز مدیریتی نمی‌دهد. تا تأیید این قرارداد، مسیرهای مدیریتی برای توکن‌های احراز‌شده 403 برمی‌گردانند.

به‌روزرسانی و تغییر وضعیت به `expectedRowVersion` دریافتی از خواندن کارشناس به شکل Base64 نیاز دارند. نسخه نامعتبر 400 و تعارض نسخه 409 برمی‌گرداند. تغییر تخصص و به‌روزرسانی رکورد والد با یک تراکنش و شرط RowVersion اجرا می‌شوند؛ این رفتار در SQL Server هنوز آزمون نشده است. ظرفیت نمی‌تواند از تعداد درخواست فعال کمتر باشد و ورودی مدیریتی امکان تغییر مستقیم این شمارنده را ندارد. کارشناس فعال باید حداقل یک تخصص داشته باشد؛ حذف تخصص یا غیرفعال‌سازی درخواست‌های تخصیص‌یافته را تغییر نمی‌دهد. تغییرات تنظیمات کارشناس در تاریخچه درخواست‌ها ثبت نمی‌شوند.
## Expert configuration change history

`GET /api/experts/{id}/changes` is restricted by the existing `ExpertsManage` permission. It accepts `pageSize` (1–100, default 50) and the paired cursor parameters `cursorChangedAtUtc` and `cursorId`; the response supplies the next cursor when more results exist. Each successful administrative configuration change records one before/after snapshot and a positive authenticated user ID. No-op changes do not write history or advance RowVersion. History is append-only in normal application operations, not tamper-proof against privileged database access.

Before deploying code that writes history, manually apply the `AddExpertChangeHistory` migration. It was scaffolded offline and has not been applied or tested against SQL Server. Previous changes are not backfilled; older app versions do not write history during a mixed-version rollout. Reverting the migration drops the history table. The existing `experts.manage` issuer trust setting remains disabled by default. See `EXPERT_CHANGE_HISTORY_REPORT.md` for verification and rollout details.