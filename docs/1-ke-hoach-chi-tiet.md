# Kế hoạch chi tiết — Hangar (Project 2, Giai đoạn 2)

> **Làm lại từ đầu (2026-11-09).** Bỏ toàn bộ đăng nhập/đăng ký, Organization, multi-tenant, tester, mời qua email, API key cho CI và webhook. Hangar giờ là **kho build mở**: ai cũng upload được, ai cũng tải được. Webhook (outbox, HMAC, retry) học riêng ở `Pharse2/WebhookDelivery`.
>
> Bản cũ (có auth/org, làm tới Ngày 10 cũ) nằm ở tag git `ban-cu-auth-org` (commit `dd462df`) — xem lại 1 file: `git show ban-cu-auth-org:src/Hangar.Api/Program.cs`.
>
> Cách dùng: đánh dấu `[x]` khi xong, ghi chú quyết định/khó khăn ngay dưới mục tiêu ngày. Ngày đánh số riêng cho project này (Ngày 1-28); số tuần (Tuần 5-8) khớp `dotnet-roadmap.md`. Nghiệp vụ/entity xem `docs/2-tai-lieu-nghiep-vu.md`.

## Điểm xuất phát

- Solution trống 4 project: `Hangar.Domain` / `Hangar.Application` / `Hangar.Infrastructure` / `Hangar.Api`. `Program.cs` chỉ có OpenAPI, chưa có package nào khác.
- `docker-compose.yml`: `postgres` (cổng host `5436`, DB `hangar_db` trống) và `minio` (API `9002`, Console `9003`, tài khoản `hangar` / `hangar123456`).
- `appsettings.Development.json` có sẵn `ConnectionStrings:Default` và mục `Minio`.

Chạy: `docker compose up -d` → `cd src/Hangar.Api` → `dotnet run`.

## Route chính (chốt dần theo ngày)

| Method | Route | Ngày |
|---|---|---|
| POST, GET, PUT, DELETE | `/api/apps`, `/api/apps/{appId}` | 1 |
| POST | `/api/apps/{appId}/builds` | 3, 4 |
| GET | `/api/apps/{appId}/builds/{buildId}/download-url` | 5 |
| GET, DELETE | `/api/apps/{appId}/builds`, `/api/apps/{appId}/builds/{buildId}` | 7 |
| POST | `/api/apps/{appId}/builds/{buildId}/install-links` | 8 |
| GET | `/install/{token}` (trang), `/install/{token}/download` (tải) | 8, 10 |
| GET | `/health/live`, `/health/ready` | 18 |

---

## Tuần 5: Nền + upload và lưu trữ (Ngày 1-7)

**Mục tiêu tuần:** có App, upload được file build thật (kể cả file lớn, đọc bằng streaming) lên MinIO, tự đọc version/package từ chính file, tải lại qua presigned URL, xóa build không để rác.

### Ngày 1 (Thứ 2) — Nền: Postgres + entity App + CRUD App
Phần này đã làm ở CRM — làm nhanh, mục đích là có App để gắn Build vào.
- [ ] Package: `Microsoft.EntityFrameworkCore`, `FluentValidation`, `FluentValidation.DependencyInjectionExtensions` (Application); `Npgsql.EntityFrameworkCore.PostgreSQL` (Infrastructure); `Microsoft.EntityFrameworkCore.Design`, `Scalar.AspNetCore` (Api)
- [ ] Entity `App` (`Name`, `Platform` = `Android`/`iOS`, `PackageId`, `CreatedAtUtc`) + `AppDbContext` + Fluent config — `PackageId` unique **toàn hệ thống** (không còn Organization để chia phạm vi)
- [ ] Migration `InitialCreate`, chạy vào `hangar_db`
- [ ] `POST /api/apps`, `GET /api/apps`, `GET/PUT/DELETE /api/apps/{appId}` — không `[Authorize]`; validate `PackageId` đúng dạng `com.company.app`; trùng `PackageId` → `409`
- [ ] Global exception handler + `ProblemDetails`; enum trả về dạng chuỗi (`"Android"`, không phải `0`) bằng `JsonStringEnumConverter`

**Mục tiêu cuối ngày:** tạo/sửa/xóa App qua Scalar; `PackageId` sai định dạng → `400` kèm lỗi từng field; trùng `PackageId` → `409`; `\dt` trong Postgres thấy bảng `Apps`.

### Ngày 2 (Thứ 3) — Kết nối MinIO
- [ ] Package `Minio` (Infrastructure); class `MinioSettings` đọc mục `Minio` trong config; đăng ký `IMinioClient` dạng singleton (client giữ connection pool, không tạo mới mỗi request)
- [ ] Interface `IFileStorageService` ở Application (`UploadAsync`, `DeleteAsync`), implement `MinioFileStorageService` ở Infrastructure — Application không biết MinIO tồn tại
- [ ] Tạo bucket `hangar-builds` lúc khởi động, idempotent: `BucketExistsAsync` trước rồi mới `MakeBucketAsync`
- [ ] Bucket có thể đã có sẵn từ bản cũ (volume `minio-data` được giữ lại) — xóa bucket trong Console rồi chạy lại để test được cả nhánh tạo mới

**Mục tiêu cuối ngày:** chạy app → thấy bucket `hangar-builds` trong Console `http://localhost:9003`; chạy lại lần 2 không lỗi; `docker stop hangar-minio` rồi chạy app → lỗi rõ ràng ngay lúc khởi động, không đợi tới request upload đầu tiên mới vỡ.

### Ngày 3 (Thứ 4) — Entity Build + upload cơ bản
- [ ] Entity `Build` (`AppId`, `Version`, `BuildNumber`, `StorageKey`, `OriginalFileName`, `FileSizeBytes`, `ReleaseNotes?`, `CreatedAtUtc`) + migration
- [ ] `POST /api/apps/{appId}/builds` — nhận `IFormFile` qua `multipart/form-data`; tạm thời client tự gửi `version`/`buildNumber` trong form (Ngày 6 sẽ đọc từ chính file)
- [ ] `StorageKey` do server sinh (ví dụ `apps/{appId}/{Guid}.apk`), **không** dùng tên file client gửi — tên file là dữ liệu người dùng, có thể chứa `../` hoặc trùng nhau
- [ ] Kiểm tra App tồn tại **trước** khi lưu file; thứ tự: lưu MinIO trước → ghi DB sau; ghi DB lỗi → tự xóa object vừa lưu (compensating action — MinIO không nằm trong transaction của DB, phải tự code "rollback")

**Mục tiêu cuối ngày:** upload file nhỏ → `201`, có object trong MinIO và dòng `Builds` khớp `StorageKey`; App không tồn tại → `404`, không có object nào được tạo; giả lập lỗi ghi DB (tạm `throw` ngay sau bước upload) → object vừa lưu bị xóa lại, bucket sạch.

### Ngày 4 (Thứ 5) — Upload file lớn (streaming)
- [ ] Đọc thẳng `Request.Body` bằng `MultipartReader` (bỏ `IFormFile`): ghi dần ra file tạm trên đĩa → biết size thật → đẩy lên MinIO (SDK cần biết size trước) → xóa file tạm trong `finally`. Các field text (`version`, `buildNumber`) cũng đọc từ section của multipart
- [ ] Giới hạn qua `Upload:MaxSizeBytes` trong config: vượt là dừng đọc ngay giữa chừng, dọn file tạm, trả `413`
- [ ] Bỏ giới hạn body mặc định của Kestrel (~28.6MB) cho riêng endpoint này
- [ ] Đo RAM tiến trình API khi upload file 250MB (tạo file giả: `fsutil file createnew big.apk 262144000`)

<details>
<summary>Gợi ý nếu kẹt (lỗi thật đã gặp ở lần làm trước)</summary>

- Body đọc ra 0 byte, hoặc `IOException: Unexpected end of Stream` → model binding của MVC đã tự đọc form trước khi action chạy. Cần gỡ `FormValueProviderFactory`/`FormFileValueProviderFactory` cho riêng action này (pattern chính thức của Microsoft cho streaming upload).
- `InvalidOperationException ... after the app has already started reading` → đừng tự set `MaxRequestBodySize` trong action, chỉ dùng `[DisableRequestSizeLimit]`.
- `File.Delete(...)` báo `CS0119` trong Controller → `ControllerBase.File(...)` che mất class `System.IO.File`, phải ghi đủ `System.IO.File.Delete(...)`.
</details>

**Mục tiêu cuối ngày:** upload 250MB → `201`, `FileSizeBytes` đúng; RAM không tăng theo kích thước file (upload 250MB lần 2 gần như không tăng thêm); hạ `MaxSizeBytes` xuống 1MB, upload 5MB → `413`, không còn file tạm trong `%TEMP%`, không có object rác trong MinIO.

### Ngày 5 (Thứ 6) — Presigned URL
- [ ] `GET /api/apps/{appId}/builds/{buildId}/download-url` — sinh presigned URL (`PresignedGetObjectAsync`), hạn dùng lấy từ config (ví dụ `Minio:PresignedUrlExpirySeconds`)
- [ ] Người tải nhận file tên dễ đọc (`MyApp-1.4.2.apk`) thay vì GUID — truyền tham số `response-content-disposition` lúc ký URL (S3 hỗ trợ sẵn)
- [ ] Ghi rõ quyết định: tải qua presigned URL (file đi thẳng MinIO → client, API không phải chuyển hàng trăm MB) thay vì stream file qua API
- [ ] Ghi nhớ cho Ngày 24: URL được ký theo host `localhost:9002` — khi API chạy trong Docker và gọi MinIO bằng `minio:9000`, URL ký ra sẽ không dùng được từ máy ngoài

**Mục tiêu cuối ngày:** `curl` URL vừa lấy (không cần gì thêm) → file tải về có SHA-256 trùng file gốc (`certutil -hashfile <file> SHA256`); đặt hạn 60 giây, đợi quá hạn gọi lại → MinIO trả `403` (`Request has expired`); sửa 1 ký tự trong chữ ký → `403`.

### Ngày 6 (Thứ 7, nhiều thời gian hơn) — Đọc metadata build
- [ ] APK: `AndroidManifest.xml` là XML dạng binary, không đọc bằng `XDocument` được → package `AlphaOmega.ApkReader` → `versionName`/`versionCode`/`package`
- [ ] IPA: file zip, tìm `Payload/*.app/Info.plist` bằng `System.IO.Compression`; plist có thể ở dạng binary → package `plist-cil` → `CFBundleShortVersionString`/`CFBundleVersion`/`CFBundleIdentifier`
- [ ] Đọc metadata từ **file tạm** (Ngày 4) trước khi đẩy lên MinIO → file sai bị chặn từ đầu, không phải upload rồi xóa
- [ ] Bỏ `version`/`buildNumber` client gửi, lấy từ file; `PackageId` đọc được phải khớp `App.PackageId`, đuôi file khớp `App.Platform` (`.apk` ↔ Android, `.ipa` ↔ iOS); unique `(AppId, BuildNumber)` — upload lại cùng build number → `409`
- [ ] Ghi quyết định: `BuildNumber` lưu kiểu gì — Android `versionCode` là số nguyên, iOS `CFBundleVersion` là chuỗi (có thể `"42"` hoặc `"1.0.3"`), mà "bản mới nhất" cần sort đúng
- [ ] File mẫu: APK thật bất kỳ; IPA khó kiếm → tự tạo file zip đúng cấu trúc `Payload/Test.app/Info.plist` rồi đổi đuôi `.ipa`

**Mục tiêu cuối ngày:** upload APK thật → tự điền đúng version/build number; upload APK của app khác (package không khớp) → lỗi `4xx` rõ ràng, không có object nào trong MinIO; upload file text đổi đuôi `.apk` → lỗi rõ ràng, không `500`.

### Ngày 7 (Chủ nhật, nhiều thời gian hơn) — Hoàn thiện API Build + tổng kết tuần
- [ ] `GET /api/apps/{appId}/builds` (sort `BuildNumber` giảm dần, phân trang), `GET /api/apps/{appId}/builds/{buildId}`
- [ ] `DELETE /api/apps/{appId}/builds/{buildId}` — xóa record DB + object MinIO; chọn thứ tự và ghi lý do theo câu hỏi: bước sau lỗi thì để lại cái gì? (record trỏ tới file không còn = link hỏng; file không còn record = rác tốn chỗ nhưng dọn được sau)
- [ ] Xóa App đang có build → chặn `409` hay xóa luôn build + file? Chọn 1, ghi lý do
- [ ] Test lại toàn bộ: tạo App → upload APK thật → metadata đúng → presigned URL tải được → xóa build → MinIO sạch
- [ ] Giải thích được: vì sao streaming thay vì buffer cả file; presigned URL hoạt động thế nào (MinIO tự kiểm chữ ký, API không đứng giữa lúc tải)

**Mục tiêu cuối tuần (Tuần 5):** upload/tải/xóa build thật ổn định, không còn file rác trong MinIO hay file tạm trên đĩa.

---

## Tuần 6: Link cài đặt + job chạy nền (Ngày 8-14)

**Mục tiêu tuần:** chia sẻ build qua link có hạn dùng/giới hạn lượt tải, đúng cả khi nhiều người tải cùng lúc; việc nặng (đọc metadata, dọn build cũ) chạy nền, upload trả về ngay.

### Ngày 8 (Thứ 2) — InstallLink: tạo + hạn dùng
- [ ] Entity `InstallLink` (`BuildId`, `Token`, `ExpiresAtUtc?`, `MaxDownloads?`, `DownloadCount`, `CreatedAtUtc`) + migration; `Token` unique
- [ ] `POST /api/apps/{appId}/builds/{buildId}/install-links` — `Token` sinh bằng `RandomNumberGenerator` (32 byte, Base64Url); không dùng Id số (tăng dần, đoán được), cũng không dùng `Guid` (không được thiết kế làm token bảo mật); `ExpiresAtUtc`/`MaxDownloads` đều optional
- [ ] `GET /install/{token}` — không có token → `404`; hết hạn → `410 Gone`; còn hạn → `302` chuyển sang presigned URL hạn ngắn (tái dùng Ngày 5)

**Mục tiêu cuối ngày:** mở link còn hạn bằng trình duyệt → tải được file; link hết hạn → `410` kèm message rõ; token bịa → `404`.

### Ngày 9 (Thứ 3) — Giới hạn lượt tải + concurrency
- [ ] `DownloadCount` tăng mỗi lần tải; hết lượt → `410`
- [ ] Viết bản ngây thơ trước ("đọc → kiểm tra → tăng → lưu"), bắn 20 request cùng lúc vào link `MaxDownloads = 1`, quan sát có bao nhiêu request lọt qua (race condition thật):
  `seq 20 | xargs -P 20 -I{} curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5032/install/<token> | sort | uniq -c`
- [ ] Sửa bằng 1 trong 2 cách, ghi lý do chọn:
  - (a) 1 câu `UPDATE ... SET "DownloadCount" = "DownloadCount" + 1 WHERE "Id" = @id AND ("MaxDownloads" IS NULL OR "DownloadCount" < "MaxDownloads")` qua `ExecuteUpdateAsync` — số dòng bị ảnh hưởng = 0 nghĩa là hết lượt
  - (b) concurrency token `xmin` (như CRM) + bắt `DbUpdateConcurrencyException`

**Mục tiêu cuối ngày:** 20 request đồng thời vào link còn đúng 1 lượt → đúng 1 request `302`, 19 request `410`; `DownloadCount` trong DB = 1.

### Ngày 10 (Thứ 4) — Trang tải cho người nhận link
- [ ] Tách 2 endpoint: `GET /install/{token}` trả trang HTML đơn giản (tên App, version, release notes, ngày upload, nút "Tải về"); `GET /install/{token}/download` mới tính lượt tải và chuyển hướng
- [ ] Lý do tách: Slack/Zalo/Messenger tự mở link để tạo bản xem trước → nếu cứ mở link là tính lượt, bot đã "ăn" mất lượt tải trước khi người thật bấm
- [ ] Encode mọi dữ liệu người dùng nhập khi đưa vào HTML (tên App, release notes) — ai cũng upload được nên ai cũng có thể chèn `<script>`
- [ ] Link hết hạn/hết lượt → trang báo lỗi dễ hiểu (vẫn đúng status code), không trả JSON `ProblemDetails` cho trình duyệt

**Mục tiêu cuối ngày:** mở trang nhiều lần → `DownloadCount` không đổi; bấm "Tải về" → tăng đúng 1 và tải được file; release notes `<script>alert(1)</script>` hiện ra dạng chữ, không chạy.

### Ngày 11 (Thứ 5) — Cài Hangfire, đọc metadata chạy nền
- [ ] Package `Hangfire.AspNetCore` + `Hangfire.PostgreSql`, lưu job vào chính `hangar_db` (schema `hangfire` riêng); dashboard `/hangfire` giữ mặc định **chỉ mở từ máy local** (không có đăng nhập để bảo vệ)
- [ ] Upload chỉ lưu file + tạo Build rồi enqueue job đọc metadata → trả `202 Accepted` ngay; job tải file từ MinIO về file tạm, đọc metadata (code Ngày 6), cập nhật Build — `Version`/`BuildNumber` phải cho phép null trong lúc chờ
- [ ] Ghi quyết định đánh đổi: Ngày 6 chặn sai package **trước** khi lưu; giờ chỉ phát hiện **sau** khi lưu — upload sai vẫn tốn 1 lần lưu và phải dọn (Ngày 12)

**Mục tiêu cuối ngày:** upload trả về ngay khi lưu file xong, không đợi đọc metadata; vài giây sau Build có version đúng; thấy job `Succeeded` trên dashboard Hangfire.

### Ngày 12 (Thứ 6) — Trạng thái xử lý Build
- [ ] `Build.ProcessingStatus` (`Pending`/`Processing`/`Ready`/`Failed`) + `FailureReason`; API list/chi tiết trả về trạng thái
- [ ] Lỗi do file (không đọc được, sai package, trùng build number) → `Failed` + lý do, **không** retry (retry 10 lần thì file hỏng vẫn hỏng); lỗi tạm thời (MinIO/DB chập chờn) → để Hangfire tự retry
- [ ] Chỉ Build `Ready` mới tạo được InstallLink / lấy presigned URL; Build `Failed` → xóa file trên MinIO, giữ record để người upload biết lý do

**Mục tiêu cuối ngày:** upload file hỏng → Build `Failed` kèm lý do rõ, job không retry; `docker stop hangar-minio` lúc job đang chờ chạy → job retry, bật MinIO lại thì Build thành `Ready`; không Build nào kẹt ở `Pending`/`Processing` mãi.

### Ngày 13 (Thứ 7, nhiều thời gian hơn) — Job dọn build cũ
- [ ] Recurring job (`RecurringJob.AddOrUpdate`, chạy mỗi đêm): mỗi App chỉ giữ N build `Ready` mới nhất (`Retention:KeepLatestBuilds` trong config), build cũ hơn → xóa file MinIO + record DB (tái dùng logic xóa Ngày 7)
- [ ] Quyết định: build cũ còn InstallLink đang hiệu lực thì xóa luôn hay giữ lại? Ghi lý do
- [ ] Dọn luôn Build `Failed` quá X ngày
- [ ] Job phải chạy lại được an toàn (tắt app giữa lúc đang xóa, lần sau chạy tiếp không lỗi, không xóa nhầm)

**Mục tiêu cuối ngày:** 1 App có N+3 build, bấm "Trigger now" trên dashboard → còn đúng N build mới nhất, MinIO còn đúng N object của App đó; trigger lần 2 liên tiếp → không lỗi, không xóa thêm gì.

### Ngày 14 (Chủ nhật, nhiều thời gian hơn) — Test lại + tổng kết tuần
- [ ] Toàn bộ luồng: upload → job đọc metadata → `Ready` → tạo InstallLink → mở trang → tải → hết lượt `410` → job dọn build cũ đúng
- [ ] Giải thích được: vì sao token ngẫu nhiên thay vì Id; race condition ở Ngày 9 xảy ra thế nào và cách sửa đã chọn chặn nó ra sao; khi nào nên đưa việc ra chạy nền thay vì xử lý ngay trong request

**Mục tiêu cuối tuần (Tuần 6):** link cài đặt chạy đúng kể cả khi tải đồng thời; upload không phải đợi xử lý nặng; build cũ tự được dọn.

---

## Tuần 7: Cache, giới hạn, quan sát (Ngày 15-21)

**Mục tiêu tuần:** giảm tải đọc bằng cache mà không hiển thị dữ liệu cũ; chặn spam; nhìn được bên trong hệ thống khi đang chạy (health, trace, metric).

### Ngày 15 (Thứ 2) — Redis cache
- [ ] Service `redis` (image `redis`) trong `docker-compose.yml`, cổng host lệch mặc định (ví dụ `6380`) tránh đụng project khác; package `Microsoft.Extensions.Caching.StackExchangeRedis`, dùng qua `IDistributedCache`
- [ ] Cache trang đầu của `GET /api/apps/{appId}/builds` (key gắn `AppId`), có TTL; miss mới query DB
- [ ] Bật log SQL của EF Core để thấy rõ lần gọi thứ 2 không query DB

**Mục tiêu cuối ngày:** gọi list lần 1 → có câu SQL trong log; lần 2 → không có SQL, trả lời nhanh hơn; `docker exec hangar-redis redis-cli KEYS '*'` và `TTL <key>` thấy đúng key và thời gian sống.

### Ngày 16 (Thứ 3) — Cache invalidation
- [ ] Liệt kê **mọi** chỗ làm danh sách build thay đổi rồi xóa đúng cache key ở từng chỗ: upload, xóa build, job đọc metadata đổi trạng thái (Ngày 12), job dọn build cũ (Ngày 13) — 2 chỗ cuối chạy trong job nền, rất dễ sót
- [ ] Giải thích: vì sao xóa-khi-thay-đổi thay vì chỉ để TTL ngắn tự hết hạn (build mới phải hiện ngay)

**Mục tiêu cuối ngày:** upload build mới → list thấy ngay khi `Ready`; trigger job dọn → list cập nhật ngay; không lúc nào list hiện build đã bị xóa.

### Ngày 17 (Thứ 4) — Rate limiting
- [ ] Rate limiter có sẵn của ASP.NET Core (`AddRateLimiter`, không cần package) — giới hạn theo IP: `/install/{token}/download` (chặn spam vét lượt tải) và `POST /api/apps/{appId}/builds` (ai cũng upload được → chặn 1 máy đẩy liên tục làm đầy ổ)
- [ ] Trả `429` kèm header `Retry-After`
- [ ] Ghi nhớ cho Ngày 26: sau nginx, mọi request đều mang IP của nginx → cần `ForwardedHeaders`, nếu không cả thế giới dùng chung 1 hạn mức

**Mục tiêu cuối ngày:** bắn vượt giới hạn → `429` có `Retry-After`; đợi hết thời gian → gọi lại được; endpoint khác không bị ảnh hưởng.

### Ngày 18 (Thứ 5) — Health checks
- [ ] `AddHealthChecks()` + `AspNetCore.HealthChecks.NpgSql`, `AspNetCore.HealthChecks.Redis`; MinIO tự viết `IHealthCheck` (gọi `BucketExistsAsync`)
- [ ] 2 endpoint: `/health/live` (app còn sống, không kiểm dependency) và `/health/ready` (kiểm Postgres/Redis/MinIO), trả JSON chi tiết từng dependency
- [ ] Redis chết thì list build có chết theo không? Nên bắt lỗi cache và đọc thẳng DB → Redis lỗi báo `Degraded` thay vì `Unhealthy` — ghi quyết định

**Mục tiêu cuối ngày:** `docker stop hangar-redis` → `/health/ready` chỉ rõ Redis lỗi, `/health/live` vẫn `200`, list build vẫn trả về được (chậm hơn); bật lại → `Healthy`.

### Ngày 19 (Thứ 6) — OpenTelemetry: trace
- [ ] Package `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`; EF Core: `OpenTelemetry.Instrumentation.EntityFrameworkCore` (hiện chỉ có bản beta)
- [ ] Service Jaeger v2 (image `jaegertracing/jaeger` — bản `all-in-one` cũ đã ngừng phát hành) trong compose: UI cổng `16686`, nhận OTLP
- [ ] `ActivitySource` riêng cho các bước upload: ghi file tạm → đẩy MinIO → ghi DB (lời gọi tới MinIO đi qua `HttpClient` nên instrumentation Http tự bắt được)

**Mục tiêu cuối ngày:** mở Jaeger thấy 1 trace upload đầy đủ (HTTP request → các bước con → câu SQL → lời gọi HTTP tới MinIO), chỉ ra bước chậm nhất bằng số liệu.

### Ngày 20 (Thứ 7, nhiều thời gian hơn) — OpenTelemetry: metric
- [ ] `Meter` riêng: counter build upload (thành công/thất bại), counter lượt tải, histogram thời gian upload
- [ ] Xem bằng `dotnet-counters monitor` (không cần dựng thêm hạ tầng); muốn có biểu đồ thì thêm Prometheus + Grafana (tùy chọn)
- [ ] Job Hangfire chạy ở thời điểm khác nên không tự nối vào trace của request upload → tự tạo span trong job, đủ để thấy job chậm ở đâu
- [ ] Phân biệt: trace = theo dõi 1 request cụ thể xuyên suốt; metric = số liệu tổng hợp theo thời gian

**Mục tiêu cuối ngày:** upload vài build (có cái lỗi) → `dotnet-counters` thấy counter tăng đúng số; giải thích được trace/metric bằng ví dụ của Hangar.

### Ngày 21 (Chủ nhật, nhiều thời gian hơn) — Test lại + tổng kết tuần
- [ ] Toàn bộ: cache hit/miss + invalidate đúng → rate limit chặn đúng → health báo đúng khi tắt từng dependency → trace/metric hiện đúng
- [ ] Giải thích được: cache invalidation khác TTL thế nào; vì sao tách `live`/`ready`; khi nào nhìn trace, khi nào nhìn metric

**Mục tiêu cuối tuần (Tuần 7):** đọc nhanh hơn nhờ cache mà không sai dữ liệu, chặn được spam, biết ngay dependency nào hỏng và bước nào chậm.

---

## Tuần 8: Hoàn thiện + deploy (Ngày 22-28)

**Mục tiêu tuần:** có integration test luồng chính, `docker compose up` từ máy sạch chạy đủ mọi service, có số liệu benchmark thật, deploy lên VPS sau nginx với pipeline CI — kết thúc Hangar.

### Ngày 22 (Thứ 2) — Integration test
- [ ] Project `tests/Hangar.IntegrationTests` (xUnit) + `WebApplicationFactory<Program>`; hạ tầng thật bằng `Testcontainers.PostgreSql`, `Testcontainers.Minio`, `Testcontainers.Redis` — không mock
- [ ] File mẫu nhỏ trong test (IPA tự tạo đúng cấu trúc là dễ nhất); job Hangfire chạy bất đồng bộ → test chờ Build `Ready` có giới hạn thời gian, không `Thread.Sleep` cố định
- [ ] Test luồng chính: tạo App → upload → `Ready` → tạo InstallLink `MaxDownloads = 1` → tải lần 1 `302`, lần 2 `410`

**Mục tiêu cuối ngày:** `dotnet test` pass trên máy không chạy sẵn container nào; chạy xong không để container rác (`docker ps -a`).

### Ngày 23 (Thứ 3) — ERD + sơ đồ kiến trúc
- [ ] Cập nhật `docs/erd.md` theo code thật (App, Build, InstallLink)
- [ ] Sơ đồ kiến trúc (Mermaid): Client → API → Postgres / MinIO / Redis / Hangfire; sơ đồ luồng upload và luồng tải qua link

**Mục tiêu cuối ngày:** ERD + sơ đồ khớp đúng code (đối chiếu với migration cuối cùng).

### Ngày 24 (Thứ 4) — Docker Compose đầy đủ + README
- [ ] Service `api` (build từ `docker/Dockerfile` có sẵn), migration tự chạy lúc khởi động, `depends_on` chờ Postgres healthy
- [ ] Bẫy presigned URL (đã ghi từ Ngày 5): trong Docker, API gọi MinIO qua `minio:9000`, nhưng URL đưa cho người tải phải là địa chỉ máy ngoài gọi được → ký URL bằng endpoint công khai (không thể ký xong rồi thay host, vì host nằm trong chữ ký)
- [ ] `docker compose down -v` rồi `up` lại từ volume trống → test luồng chính qua container thật (kiểm tra MinIO ghi được vào volume mới — image chạy non-root, xem ghi chú trong `docker-compose.yml`)
- [ ] README: cách chạy, bảng endpoint, kiến trúc, ERD

**Mục tiêu cuối ngày:** máy sạch: `docker compose up` → mọi service healthy; upload qua API trong container → tải được bằng presigned URL từ trình duyệt máy host.

### Ngày 25 (Thứ 5) — Benchmark bằng k6
- [ ] Script k6 chạy bằng image `grafana/k6` (không cần cài; gọi API trên máy host qua `host.docker.internal`): `GET /api/apps/{appId}/builds` bật cache vs tắt cache; trang `/install/{token}`
- [ ] Tắt rate limit khi đo (nếu không số liệu chỉ toàn `429`)

**Mục tiêu cuối ngày:** có bảng số liệu thật (RPS, p95 latency) có/không cache, kết luận bằng số chứ không bằng cảm giác.

### Ngày 26 (Thứ 6) — Deploy VPS + nginx
- [ ] VPS (hoặc máy ảo/WSL giả lập): Docker Compose + nginx reverse proxy trước API, chỉ mở cổng 80/443
- [ ] Bẫy upload: nginx mặc định chỉ nhận body 1MB (`client_max_body_size`) và buffer cả body trước khi chuyển tiếp (`proxy_request_buffering`) → phải chỉnh, nếu không streaming ở Ngày 4 mất tác dụng
- [ ] `ForwardedHeaders` để rate limit (Ngày 17) thấy IP thật; MinIO cũng cần đường công khai cho presigned URL (qua nginx hoặc cổng riêng)

**Mục tiêu cuối ngày:** từ máy khác upload file 250MB qua nginx → `201`; rate limit tính theo IP của máy gọi, không phải IP của nginx.

### Ngày 27 (Thứ 7, nhiều thời gian hơn) — CI/CD bằng GitHub Actions
- [ ] Workflow mỗi lần push/PR: build + chạy integration test (runner `ubuntu-latest` có sẵn Docker cho Testcontainers)
- [ ] Build Docker image, đẩy lên GitHub Container Registry (`ghcr.io`); (tùy chọn) bước deploy tự động lên VPS qua SSH
- [ ] Bí mật (mật khẩu DB/MinIO trên VPS, SSH key) để trong GitHub Secrets, không commit

**Mục tiêu cuối ngày:** push 1 commit → pipeline xanh; cố tình làm hỏng 1 test → pipeline đỏ và không đẩy image.

### Ngày 28 (Chủ nhật, nhiều thời gian hơn) — Tổng kết Hangar
- [ ] README cuối: mô tả, cách chạy, ERD, kiến trúc, endpoint, demo luồng chính
- [ ] Đối chiếu "Mốc kiểm tra cuối Giai đoạn 2" trong `dotnet-roadmap.md`
- [ ] Cập nhật bảng tiến độ Tuần 5-8 trong `dotnet-roadmap.md`; ghi nợ kỹ thuật (nếu có)

**Mục tiêu cuối tuần (Tuần 8) — Tổng kết Hangar:** upload/lưu trữ, link cài đặt, job nền, cache, rate limit, quan sát, test, deploy đều chạy được qua Docker Compose từ máy sạch và trên VPS, có số liệu benchmark, giải thích được từng quyết định đã ghi.

> Nếu trễ tiến độ: cắt theo thứ tự metric (Ngày 20) → k6 (Ngày 25) → deploy tự động qua SSH (giữ bản deploy tay). Không cắt streaming upload, concurrency lượt tải, cache invalidation, integration test — đây là phần cốt lõi của Hangar.
