# Kế hoạch chi tiết — Hangar (Project 2, Giai đoạn 2)

> Cách dùng giống `Pharse1/CRM/docs/1-ke-hoach-chi-tiet.md`: đánh dấu `[x]` khi xong, viết thêm ghi chú ngay dưới mục tiêu ngày nếu có quyết định/khó khăn đáng nhớ. Đánh số ngày riêng cho project này (Ngày 1-49), không dùng số tuyệt đối toàn lộ trình 12 tuần.
>
> Số tuần (Tuần 4-10) khớp với `dotnet-roadmap.md` ở thư mục gốc repo học tập. Nghiệp vụ/entity tham chiếu `docs/2-tai-lieu-nghiep-vu.md` — đọc trước khi code mỗi tuần mới.

---

## Tuần 4: Dựng nền + multi-tenant (Ngày 1-7)

**Mục tiêu tuần:** port được auth/JWT từ Project 1 (đơn giản hóa — bỏ Role toàn cục), dựng xong Organization/OrganizationMember/App, global query filter hoạt động đúng, resource-based authorization phân quyền đúng theo 3 vai trò (Owner/Developer/Tester) ở 2 tầng (Organization và từng App).

### Ngày 1 (Thứ 2) — Port auth nền (đơn giản hóa so với Project 1)
- [ ] Port `ApplicationUser` (`IdentityUser<int>` + `FullName`) — **không** port `ApplicationRole`/role toàn cục, vì vai trò ở Hangar gắn theo từng Organization (xem mục 1 `docs/2-tai-lieu-nghiep-vu.md`)
- [ ] Dùng `AddIdentityCore<ApplicationUser>()` thay cho `AddIdentity<TUser,TRole>()` của Project 1 — không cần `RoleManager` toàn cục nữa
- [ ] Port `ITokenService`/`JwtTokenService`: JWT chỉ chứa `UserId`/`Email`, **không** nhúng claim `role` (role phụ thuộc Organization đang truy cập, không cố định theo user)
- [ ] Port `IIdentityService`/`IdentityService` + refresh token (rotate/revoke) — logic này giữ nguyên, không đổi
- [ ] `POST /api/auth/register`, `/login`, `/refresh`, `/logout` — test bằng curl, xác nhận JWT không có claim role

**Mục tiêu cuối ngày:** đăng ký/đăng nhập được, JWT phát ra đúng (so sánh claims với token Project 1 — thấy rõ thiếu claim `role`), refresh token hoạt động như cũ.

### Ngày 2 (Thứ 3) — ERD + Organization/OrganizationMember
- [ ] Vẽ ERD domain Hangar (xem mục 2 `docs/2-tai-lieu-nghiep-vu.md`), lưu `docs/erd.md`
- [ ] Entity `Organization`, `OrganizationMember` (enum Role: `Owner`/`Developer`) + migration đầu tiên
- [ ] `POST /api/organizations` — tạo Organization, người tạo tự thành `OrganizationMember` Role=`Owner` **trong 1 transaction** (2 thay đổi phải ăn khớp — đúng bài học Ngày 15 Project 1, áp lại ở bài toán mới)
- [ ] `GET /api/organizations` — danh sách Organization user hiện tại là thành viên

**Mục tiêu cuối ngày:** tạo Organization mới → tự động có đúng 1 dòng `OrganizationMember` Role=Owner; verify bằng query DB trực tiếp.

### Ngày 3 (Thứ 4) — Entity App + global query filter
- [ ] Entity `App` (`OrganizationId`, `Name`, `Platform`, `PackageId`) + migration
- [ ] `ICurrentOrganizationContext` — scoped service, có property `OrganizationId` (nullable, set được đúng 1 lần/request)
- [ ] `AppDbContext` nhận `ICurrentOrganizationContext` qua constructor, cấu hình `HasQueryFilter(a => a.OrganizationId == _context.OrganizationId)` cho `App` trong `OnModelCreating`
- [ ] Test thủ công: query `App` khi **chưa set** `ICurrentOrganizationContext` → phải trả về rỗng dù DB có dữ liệu thật — chứng minh filter mặc định chặn hết, không lọt dữ liệu ra ngoài khi quên set context

**Mục tiêu cuối ngày:** global query filter hoạt động đúng — chưa set Organization context thì không thấy App nào.

### Ngày 4 (Thứ 5) — Resource-based authorization
- [ ] `OrganizationMemberRequirement` (giữ Role tối thiểu cần có) — kiểu `IAuthorizationRequirement`, tương tự `PermissionRequirement` Project 1 nhưng kiểm tra theo resource (`{organizationId}` trong route), không theo policy tĩnh
- [ ] `OrganizationMemberAuthorizationHandler` — đọc `{organizationId}` từ route, tra `OrganizationMember` theo `(OrganizationId, UserId từ claim JWT)`, so khớp Role tối thiểu
- [ ] 1 action filter set `ICurrentOrganizationContext.OrganizationId` **sau khi** handler xác nhận hợp lệ — thứ tự quan trọng, không set trước khi check quyền (tránh query filter "mở cửa" cho user chưa được xác nhận là thành viên)
- [ ] Test: user không phải thành viên Organization X gọi `/api/organizations/{X}/apps` → `403` (không phải `404` — phân biệt rõ "không có quyền" và "không tồn tại", đúng nguyên tắc "lỗi rõ ràng" từ Ngày 15/16 Project 1)

**Mục tiêu cuối ngày:** gọi API thuộc Organization không phải thành viên → `403` đúng; gọi đúng Organization mình là thành viên → thấy dữ liệu đúng, không thấy Organization khác.

### Ngày 5 (Thứ 6) — CRUD App
- [ ] `GET/POST/PUT/DELETE /api/organizations/{orgId}/apps` — yêu cầu Role `Owner` hoặc `Developer` (tái dùng handler Ngày 4, chỉ đổi Role tối thiểu theo action)
- [ ] Validator (FluentValidation) cho `CreateAppRequest`/`UpdateAppRequest` — chỉ hình thức, giống quy ước Project 1 (tồn tại thật check ở Service)
- [ ] `PackageId` unique trong phạm vi 1 Organization (check ở Service) — trùng `PackageId` ở Organization **khác** vẫn hợp lệ (đa tenant, không coi là trùng toàn cục)

**Mục tiêu cuối ngày:** CRUD App hoàn chỉnh, đúng phân quyền Owner/Developer; test `PackageId` trùng trong cùng org → lỗi, trùng ở org khác → vẫn tạo được.

### Ngày 6 (Thứ 7, nhiều thời gian hơn) — AppTester
- [ ] Entity `AppTester` + migration
- [ ] `POST /api/organizations/{orgId}/apps/{appId}/testers` (Owner/Developer) — mời tester vào đúng 1 App (không phải cả Organization)
- [ ] `GET .../testers`, `DELETE .../testers/{userId}`
- [ ] `AppTesterRequirement`/handler riêng — Tester chỉ xem được đúng App mình được mời, gọi App khác trong cùng Organization (không được mời) → `403`

**Mục tiêu cuối ngày:** mời tester vào 1 App → tester gọi API xem App đó được; gọi App khác trong cùng Organization → `403` đúng.

### Ngày 7 (Chủ nhật, nhiều thời gian hơn) — Mời thành viên qua email, tổng kết tuần
- [ ] Thêm service `mailhog` (hoặc `smtp4dev`) vào `docker-compose.yml` — bắt email ở local, không cần SMTP/SendGrid thật
- [ ] `POST /api/organizations/{orgId}/members/invite` (Owner) — sinh token, gửi email qua MailHog
- [ ] `POST /api/organizations/{orgId}/members/accept` — người được mời chấp nhận (tài khoản mới hoặc đã có), tạo `OrganizationMember`
- [ ] Test toàn bộ luồng tuần: tạo Organization → mời Developer → accept → Developer tạo App → mời Tester vào App → Tester xem đúng quyền

**Mục tiêu cuối tuần (Tuần 4):** multi-tenant + resource-based authorization hoạt động đúng end-to-end — global query filter chặn đúng, 3 vai trò phân quyền đúng ở 2 tầng (Organization và từng App).

---

## Tuần 5: Upload và lưu trữ (Ngày 8-14)

**Mục tiêu tuần:** upload được file build (APK/IPA) thật, kể cả file lớn (streaming), lưu MinIO, đọc đúng metadata (version/package) từ chính file, tải lại qua presigned URL.

### Ngày 8 (Thứ 2) — Cài MinIO
- [ ] Thêm service `minio` vào `docker-compose.yml` (image `minio/minio`, console UI riêng cổng)
- [ ] Cài SDK `Minio` ở Infrastructure, tạo bucket `hangar-builds` lúc khởi động (idempotent — kiểm tra tồn tại trước khi tạo, giống cách seed Permission Project 1)

**Mục tiêu cuối ngày:** container MinIO chạy, tạo/list bucket được từ code (test nhanh qua 1 endpoint tạm hoặc unit nhỏ).

### Ngày 9 (Thứ 3) — Entity Build + upload cơ bản
- [ ] Entity `Build` + migration
- [ ] `POST .../apps/{appId}/builds` — nhận file qua `multipart/form-data`, lưu MinIO rồi mới ghi `Build` — quyết định rõ thứ tự, viết logic tự xóa file MinIO nếu bước ghi DB thất bại (không phải DB transaction, phải tự code "rollback" cho side-effect ngoài DB)

**Mục tiêu cuối ngày:** upload file nhỏ qua API, lưu đúng MinIO + đúng `Build` record; giả lập lỗi ghi DB → file đã lưu MinIO bị tự xóa lại, không để rác.

### Ngày 10 (Thứ 4) — Upload file lớn (streaming)
- [ ] Đọc stream trực tiếp từ `Request.Body` (không buffer toàn bộ file vào RAM) — tự parse multipart hoặc tắt model binding mặc định cho route này
- [ ] Giới hạn kích thước file tối đa, trả `413` rõ ràng nếu vượt
- [ ] Test với file giả lập vài trăm MB, theo dõi RAM không tăng tương ứng kích thước file

**Mục tiêu cuối ngày:** upload file lớn mà RAM không tăng bất thường (có số liệu quan sát thật, không chỉ tin bằng cảm giác).

### Ngày 11 (Thứ 5) — Presigned URL
- [ ] `GET .../builds/{id}/download-url` — sinh presigned URL (MinIO tự ký, có hạn), client tải trực tiếp từ MinIO, không qua lại API
- [ ] Ghi rõ quyết định: dùng presigned URL cho InstallLink (Tuần 6, giảm tải server), vẫn giữ endpoint tải qua API cho trường hợp cần kiểm soát/log chặt hơn

**Mục tiêu cuối ngày:** lấy presigned URL, tải trực tiếp từ MinIO bằng URL đó (không cần Bearer token), URL hết hạn đúng thời gian cấu hình.

### Ngày 12 (Thứ 6) — Đọc metadata build
- [ ] Parse APK (file zip, đọc `AndroidManifest.xml` dạng binary — dùng thư viện NuGet chuyên dụng) → `versionName`/`versionCode`/`packageName`
- [ ] Parse IPA (file zip, đọc `Info.plist` trong `Payload/*.app/`) → `CFBundleShortVersionString`/`CFBundleVersion`/`CFBundleIdentifier`
- [ ] Tự điền `Version`/`BuildNumber`/`PackageId` từ file; validate `PackageId` đọc được khớp `App.PackageId` đã đăng ký, chặn upload nhầm app

**Mục tiêu cuối ngày:** upload file APK/IPA thật → tự đọc đúng version/package; upload nhầm app (package không khớp) → bị chặn, lỗi rõ ràng.

### Ngày 13 (Thứ 7, nhiều thời gian hơn) — Hoàn thiện API Build
- [ ] `GET .../builds` (list, sort `BuildNumber` giảm dần), `GET .../builds/{id}`
- [ ] `DELETE .../builds/{id}` — xóa file MinIO + record DB cùng lúc, xử lý khi 1 trong 2 bước thất bại
- [ ] Validator cho upload (dung lượng, đúng định dạng `.apk`/`.ipa`)

**Mục tiêu cuối ngày:** CRUD Build hoàn chỉnh, xóa Build dọn sạch cả MinIO lẫn DB.

### Ngày 14 (Chủ nhật, nhiều thời gian hơn) — Test lại + tổng kết tuần
- [ ] Test toàn bộ luồng: tạo App → upload build thật (file mẫu) → metadata đọc đúng → presigned URL tải được → xóa build → MinIO sạch
- [ ] Giải thích được: vì sao dùng streaming thay vì buffer toàn file; presigned URL hoạt động thế nào (MinIO tự ký, server không cần đứng giữa lúc tải)

**Mục tiêu cuối tuần (Tuần 5):** upload/tải/xóa build thật ổn định, không rò rỉ file rác trong MinIO.

---

## Tuần 6: Xác thực máy-máy và link cài đặt (Ngày 15-21)

**Mục tiêu tuần:** CI upload được build bằng API key (không qua JWT), tester tải được build qua link công khai có hạn dùng/giới hạn lượt tải, an toàn với concurrency.

### Ngày 15 (Thứ 2) — Entity ApiKey + sinh key
- [ ] Entity `ApiKey` + migration
- [ ] `POST /api/organizations/{orgId}/api-keys` (Owner) — sinh key ngẫu nhiên đủ entropy, trả về **đúng 1 lần** lúc tạo, từ đó chỉ lưu hash (giống `RefreshToken.TokenHash` Project 1)
- [ ] `DELETE /api/organizations/{orgId}/api-keys/{id}` (revoke)

**Mục tiêu cuối ngày:** tạo API key, DB chỉ lưu hash; gọi lại không lấy được key gốc lần 2.

### Ngày 16 (Thứ 3) — Xác thực bằng ApiKey (khác JWT)
- [ ] Custom `AuthenticationHandler` cho scheme riêng (ví dụ `ApiKeyScheme`) — đọc header `X-Api-Key`, hash, tra DB, set `ClaimsPrincipal` gắn đúng Organization sở hữu key
- [ ] Đăng ký cả 2 scheme (JWT cho user, ApiKey cho CI) cùng lúc — route CI dùng `[Authorize(AuthenticationSchemes = "ApiKeyScheme")]` riêng với route user dùng JWT

**Mục tiêu cuối ngày:** gọi API bằng header `X-Api-Key` xác thực được, không cần Bearer token; key đã revoke → `401`.

### Ngày 17 (Thứ 4) — Endpoint CI upload
- [ ] `POST /api/ci/builds` — nhận `AppId`/`PackageId` trong body (không có `{orgId}` trong route vì CI không biết context qua URL, Organization suy ra từ ApiKey đang dùng) — tái dùng toàn bộ logic upload Tuần 5
- [ ] Validate `App` thuộc đúng Organization của ApiKey đang dùng (chặn key Organization A upload nhầm vào App Organization B)

**Mục tiêu cuối ngày:** script giả lập CI (curl + ApiKey) upload build thành công, build gắn đúng `UploadedByApiKeyId`.

### Ngày 18 (Thứ 5) — InstallLink: tạo + hạn dùng
- [ ] Entity `InstallLink` + migration
- [ ] `POST .../builds/{id}/install-links` — sinh `Token` ngẫu nhiên, `ExpiresAtUtc`/`MaxDownloads` theo request (cả 2 optional)
- [ ] `GET /install/{token}` — endpoint **công khai** (không `[Authorize]`), check hết hạn/vượt giới hạn tải → `404`/`410` rõ ràng trước khi cho tải

**Mục tiêu cuối ngày:** link còn hạn tải được; link hết hạn hoặc vượt `MaxDownloads` → chặn đúng, message rõ ràng.

### Ngày 19 (Thứ 6) — Giới hạn lượt tải + concurrency
- [ ] `DownloadCount` tăng mỗi lần tải thành công — áp lại bài học concurrency Ngày 17 Project 1: 2 người tải cùng lúc khi còn đúng 1 lượt, không được để cả 2 cùng qua
- [ ] Concurrency token cho `InstallLink` (giống `xmin` Project 1) hoặc transaction + check lại trước khi tăng `DownloadCount`

**Mục tiêu cuối ngày:** 2 request tải đồng thời khi còn đúng 1 lượt — đúng 1 thành công, 1 bị chặn rõ ràng, không có tình trạng cả 2 cùng tải được.

### Ngày 20 (Thứ 7, nhiều thời gian hơn) — Trang cho tester
- [ ] `GET /install/{token}` trả HTML đơn giản (tên App/version + nút tải) hoặc JSON (tùy thời gian — JSON là đủ cho mục tiêu học backend)
- [ ] API danh sách build của 1 App cho Tester đã đăng nhập (khác link công khai — cần JWT, trả đúng build Tester được mời xem)

**Mục tiêu cuối ngày:** Tester đăng nhập thấy đúng danh sách build của App được mời; người ngoài có link công khai tải được không cần đăng nhập.

### Ngày 21 (Chủ nhật, nhiều thời gian hơn) — Test lại + tổng kết tuần
- [ ] Test toàn bộ luồng: CI upload bằng ApiKey → tạo InstallLink → tester tải qua link công khai → giới hạn lượt tải hoạt động đúng
- [ ] Giải thích được: vì sao CI cần cơ chế xác thực khác JWT (không có "người" đăng nhập); vì sao InstallLink dùng Token ngẫu nhiên thay vì Id số

**Mục tiêu cuối tuần (Tuần 6):** luồng "CI build → tester tải" hoạt động đầy đủ, kiểm soát đúng hạn dùng/giới hạn tải kể cả khi có concurrency.

---

## Tuần 7: Background job, cache, giới hạn (Ngày 22-28)

**Mục tiêu tuần:** việc nặng (đọc metadata, dọn dữ liệu cũ) chạy nền không làm chậm request chính; cache giảm tải đọc; rate limiting chặn được spam.

### Ngày 22 (Thứ 2) — Cài Hangfire
- [ ] `Hangfire.AspNetCore` + `Hangfire.PostgreSql`, dashboard `/hangfire` (bảo vệ bằng auth, không public)
- [ ] Chuyển đọc metadata build (Ngày 12) từ xử lý đồng bộ trong request sang **background job** — upload trả `202 Accepted` ngay, metadata điền sau khi job chạy xong

**Mục tiêu cuối ngày:** upload trả về nhanh, metadata điền sau vài giây qua job, xem được lịch sử job trên dashboard Hangfire.

### Ngày 23 (Thứ 3) — Trạng thái xử lý Build
- [ ] Thêm `Build.ProcessingStatus` (enum: `Pending`/`Processing`/`Ready`/`Failed`) — API trả đúng trạng thái, tester biết build "đang xử lý" chứ không phải lỗi
- [ ] Job thất bại (file không đọc được metadata) → set `Failed`, lưu lý do, không để job throw im lặng

**Mục tiêu cuối ngày:** upload file hỏng → build chuyển `Failed` kèm lý do rõ ràng, không "treo" ở `Pending` mãi.

### Ngày 24 (Thứ 4) — Job dọn build cũ
- [ ] Recurring job: với mỗi App, giữ N build mới nhất (policy cấu hình), build cũ hơn tự xóa (file MinIO + record DB)
- [ ] Job này dùng `IgnoreQueryFilters()` — phải quét **toàn bộ** Organization, không bị global query filter (chỉ áp cho request có context) giới hạn — đúng tình huống lý thuyết đã ghi ở Ngày 21 Project 1, lần đầu thực sự dùng tới

**Mục tiêu cuối ngày:** job xóa đúng build cũ vượt policy, verify bằng build giả lập vượt ngưỡng rồi trigger job thủ công qua dashboard Hangfire.

### Ngày 25 (Thứ 5) — Redis cache
- [ ] Thêm service `redis` vào `docker-compose.yml`, cài `Microsoft.Extensions.Caching.StackExchangeRedis`
- [ ] Cache danh sách build mới nhất của 1 App (key gắn `AppId`) — `GET .../builds` đọc cache trước, miss mới query DB

**Mục tiêu cuối ngày:** gọi lại `GET .../builds` lần 2 nhanh hơn rõ rệt, không query DB lần 2 (xem log xác nhận).

### Ngày 26 (Thứ 6) — Cache invalidation
- [ ] Upload/xóa build → tự xóa đúng cache key của `AppId` liên quan — không để cache cũ hiển thị sai sau khi có build mới
- [ ] Giải thích: vì sao chọn invalidate-khi-thay-đổi thay vì chỉ để TTL ngắn tự hết hạn (build mới cần hiện ngay, không chấp nhận chờ)

**Mục tiêu cuối ngày:** upload build mới → gọi lại list thấy ngay, không phải dữ liệu cache cũ.

### Ngày 27 (Thứ 7, nhiều thời gian hơn) — Rate limiting
- [ ] `Microsoft.AspNetCore.RateLimiting` — giới hạn `/install/{token}` theo IP (chặn spam vét cạn `MaxDownloads`), giới hạn `/api/ci/builds` theo ApiKey (chặn CI lỗi loop)
- [ ] Trả `429` kèm header `Retry-After` đúng chuẩn

**Mục tiêu cuối ngày:** spam vượt rate limit → `429` đúng kèm `Retry-After`; request bình thường không bị ảnh hưởng.

### Ngày 28 (Chủ nhật, nhiều thời gian hơn) — Test lại + tổng kết tuần
- [ ] Test toàn bộ: upload → job xử lý metadata nền → cache list → invalidate đúng khi đổi dữ liệu → job dọn build cũ đúng → rate limit chặn đúng
- [ ] Giải thích được: khi nào nên xử lý nền thay vì xử lý ngay trong request; cache invalidation khác TTL thế nào

**Mục tiêu cuối tuần (Tuần 7):** hệ thống chịu tải cơ bản tốt hơn hẳn, việc nặng không chặn request chính.

---

## Tuần 8: Quan sát và hoàn thiện (Ngày 29-35)

**Mục tiêu tuần:** quan sát được hệ thống (trace/metric/health theo từng dependency), có integration test luồng chính, `docker compose up` chạy hết 4 service từ máy sạch — kết thúc Project chính (Hangar core).

### Ngày 29 (Thứ 2) — Health checks
- [ ] `AddHealthChecks()` cho Postgres, Redis, MinIO (không chỉ "app còn sống" như Project 1 — kiểm tra từng dependency riêng) — `/health` trả chi tiết

**Mục tiêu cuối ngày:** tắt thử Redis → `/health` báo đúng dependency nào fail, không chỉ "Unhealthy" mù mờ.

### Ngày 30 (Thứ 3) — OpenTelemetry: trace
- [ ] `OpenTelemetry.Extensions.Hosting` + exporter console (hoặc Jaeger/Zipkin để xem UI) — trace tự động HTTP request + EF Core query
- [ ] Theo 1 request upload build từ đầu tới cuối qua trace

**Mục tiêu cuối ngày:** xem được 1 trace hoàn chỉnh của request upload, chỉ ra bước nào chậm nhất.

### Ngày 31 (Thứ 4) — OpenTelemetry: metric
- [ ] Metric: số build upload/phút, tỷ lệ lỗi upload, latency endpoint tải
- [ ] Phân biệt rõ với trace: trace = theo dõi 1 request cụ thể xuyên suốt; metric = số liệu tổng hợp theo thời gian, không gắn 1 request cụ thể

**Mục tiêu cuối ngày:** xem được số liệu tổng hợp thật; giải thích khác biệt trace/metric bằng đúng ví dụ project này.

### Ngày 32 (Thứ 5) — Integration test
- [ ] `Testcontainers.PostgreSql` (+ container Redis/MinIO nếu cần) — test chạy lên hạ tầng thật trong container tạm, không mock
- [ ] `WebApplicationFactory` test luồng chính: tạo Organization → tạo App → upload build (file giả lập nhỏ) → tạo InstallLink → tải qua link

**Mục tiêu cuối ngày:** `dotnet test` pass, tự dọn container sau khi xong (không để container rác).

### Ngày 33 (Thứ 6) — Hoàn thiện ERD + kiến trúc
- [ ] Cập nhật `docs/erd.md` đầy đủ domain hiện tại
- [ ] Sơ đồ kiến trúc (Mermaid) luồng upload build: Client → API → MinIO + Postgres + Hangfire job nền

**Mục tiêu cuối ngày:** ERD + sơ đồ kiến trúc phản ánh đúng code thật.

### Ngày 34 (Thứ 7, nhiều thời gian hơn) — Docker Compose tổng + README
- [ ] Gắn service `api` vào `docker-compose.yml` (giống Ngày 21 Project 1) — migration tự chạy lúc khởi động
- [ ] `docker compose up` từ volume trống (Postgres + Redis + MinIO + api) → xác nhận chạy hết, test luồng chính qua container thật
- [ ] README đầy đủ: cách chạy, bảng endpoint, kiến trúc, ERD

**Mục tiêu cuối ngày:** `docker compose up` từ máy sạch chạy được toàn bộ 4 service.

### Ngày 35 (Chủ nhật, nhiều thời gian hơn) — Tổng kết Project chính
- [ ] Rà lại toàn bộ tiêu chí (giống Ngày 21 Project 1) — đặc biệt rà kỹ phân quyền Owner/Developer/Tester ở **mọi** endpoint, dễ sót hơn Project 1 vì nhiều route hơn
- [ ] Ghi nợ kỹ thuật (nếu có) mang vào Tuần 9-10

**Mục tiêu cuối tuần (Tuần 8) — Tổng kết Project chính (Hangar core):** multi-tenant + resource-based auth + upload/lưu trữ + CI/install link + background job/cache/rate limit + observability đều hoạt động, verify qua Docker Compose từ máy sạch.

---

## Tuần 9: Webhook — Độ tin cậy (Ngày 36-42)

**Mục tiêu tuần:** khi có build mới, gửi được webhook cho endpoint đã đăng ký, không mất sự kiện dù gửi lỗi (outbox pattern), có ký xác thực (HMAC).

### Ngày 36 (Thứ 2) — Entity + CRUD WebhookEndpoint
- [ ] Entity `WebhookEndpoint` + migration
- [ ] `POST/GET/DELETE /api/organizations/{orgId}/webhooks` (Owner) — `Secret` sinh tự động, trả về đúng 1 lần lúc tạo (giống ApiKey)

**Mục tiêu cuối ngày:** CRUD webhook endpoint, secret chỉ hiện 1 lần.

### Ngày 37 (Thứ 3) — Outbox pattern
- [ ] Entity `OutboxEvent` + migration
- [ ] Khi `Build` tạo thành công (Ngày 9) → ghi thêm 1 `OutboxEvent` (`EventType = "build.created"`) **trong cùng transaction** với việc tạo Build

**Mục tiêu cuối ngày:** tạo build mới → có đúng 1 `OutboxEvent` tương ứng cùng transaction; giả lập lỗi giữa chừng → không có Build thiếu OutboxEvent hoặc ngược lại (vẫn là bài học transaction Ngày 15 Project 1, áp dụng vào bài toán mới).

### Ngày 38 (Thứ 4) — Vì sao cần Outbox (lý thuyết + WebhookDelivery)
- [ ] Giải thích vấn đề "dual write": gọi webhook trực tiếp ngay trong request (không qua Outbox) → ghi Build thành công nhưng gọi webhook lỗi mạng → mất sự kiện vĩnh viễn, không ai biết để gửi lại. Outbox tách "ghi sự kiện" (chắc chắn, cùng transaction) khỏi "gửi đi" (có thể lỗi, retry được vì sự kiện vẫn còn trong DB)
- [ ] Entity `WebhookDelivery` + migration — mỗi lần thử gửi 1 `OutboxEvent` tới 1 `WebhookEndpoint` tạo 1 dòng

**Mục tiêu cuối ngày:** giải thích được outbox pattern bằng đúng ví dụ Build/webhook của project này, không chỉ lý thuyết chung.

### Ngày 39 (Thứ 5) — Worker đọc Outbox
- [ ] Worker (Hangfire recurring job hoặc `BackgroundService` + `Channel<T>`) — định kỳ quét `OutboxEvent` chưa `ProcessedAtUtc`, với mỗi `WebhookEndpoint` active của Organization liên quan → tạo `WebhookDelivery`, gửi HTTP POST

**Mục tiêu cuối ngày:** tạo build mới → trong vài giây, endpoint test (`webhook.site` hoặc endpoint giả lập local) nhận được request.

### Ngày 40 (Thứ 6) — Ký HMAC
- [ ] Ký payload bằng `WebhookEndpoint.Secret` (HMAC-SHA256), gửi kèm header (ví dụ `X-Hangar-Signature`)
- [ ] Viết script mẫu "verify ở phía nhận" (dù Hangar không phải bên nhận) — tự tính lại HMAC từ payload+secret, so sánh header, không tin payload nếu không khớp

**Mục tiêu cuối ngày:** verify thủ công xác nhận chữ ký HMAC đúng với payload thật đã gửi.

### Ngày 41 (Thứ 7, nhiều thời gian hơn) — Test độ tin cậy
- [ ] Giả lập webhook endpoint trả lỗi (500) hoặc timeout → `WebhookDelivery.Status = Failed`, không crash worker, không chặn delivery khác

**Mục tiêu cuối ngày:** 1 endpoint lỗi không ảnh hưởng gửi webhook cho endpoint/Organization khác.

### Ngày 42 (Chủ nhật, nhiều thời gian hơn) — Tổng kết tuần
- [ ] Test lại toàn bộ: tạo build → outbox → worker gửi → verify HMAC đúng
- [ ] Giải thích được: outbox giải quyết dual-write ra sao; vì sao cần ký HMAC

**Mục tiêu cuối tuần (Tuần 9):** luồng webhook cơ bản tin cậy (không mất sự kiện dù gửi lỗi), có ký xác thực.

---

## Tuần 10: Webhook — Retry và hoàn thiện (Ngày 43-49)

**Mục tiêu tuần:** webhook tự retry hợp lý khi lỗi, có DLQ, chống xử lý trùng phía nhận (idempotency), có trang lịch sử + gửi lại thủ công, deploy thử + CI/CD, benchmark, hoàn thiện toàn bộ Giai đoạn 2.

### Ngày 43 (Thứ 2) — Retry với exponential backoff
- [ ] `WebhookDelivery` thêm `NextRetryAtUtc` — gửi thất bại thì chờ tăng dần (ví dụ 30s, 2 phút, 10 phút...), worker Ngày 39 chỉ lấy delivery có `NextRetryAtUtc <= now`
- [ ] Giới hạn số lần retry tối đa (ví dụ 5), vượt quá → `Failed` vĩnh viễn

**Mục tiêu cuối ngày:** endpoint lỗi liên tục → thời gian chờ giữa các lần retry tăng dần đúng, dừng đúng sau số lần giới hạn.

### Ngày 44 (Thứ 3) — Dead-letter queue
- [ ] Delivery vượt số lần retry → `Status = Failed` kèm đánh dấu đã hết retry (tái dùng field có sẵn, không tạo thêm entity không cần thiết)
- [ ] `GET /api/organizations/{orgId}/webhooks/{id}/deliveries?status=Failed` — xem danh sách delivery "đã chết"

**Mục tiêu cuối ngày:** lọc được delivery đã hết retry, phân biệt rõ với delivery đang chờ retry tiếp.

### Ngày 45 (Thứ 4) — Idempotency phía nhận
- [ ] Mỗi `WebhookDelivery` gửi kèm `EventId` cố định (= `OutboxEventId`, không đổi giữa các lần retry) — bên nhận dựa vào đây biết "đã xử lý sự kiện này chưa"
- [ ] Viết hướng dẫn ngắn (README/docs) cho bên nhận: lưu `EventId` đã xử lý, bỏ qua nếu gặp lại

**Mục tiêu cuối ngày:** chứng minh bằng ví dụ thật — cùng 1 `EventId` xuất hiện ở lần gửi đầu và lần retry, phía nhận có đủ thông tin chống xử lý trùng.

### Ngày 46 (Thứ 5) — Trang lịch sử + gửi lại thủ công
- [ ] `GET /api/organizations/{orgId}/webhooks/{id}/deliveries` — lịch sử đầy đủ (kể cả thành công), phân trang
- [ ] `POST .../deliveries/{deliveryId}/redeliver` (Owner) — gửi lại 1 delivery đã fail, không chờ retry tự động

**Mục tiêu cuối ngày:** xem lịch sử đầy đủ, gửi lại thủ công 1 delivery đã fail thành công.

### Ngày 47 (Thứ 6) — Benchmark nhẹ bằng k6
- [ ] Script k6 load test `GET .../builds` (có cache, Tuần 7) và `/install/{token}` — so sánh RPS/latency có cache vs tắt cache

**Mục tiêu cuối ngày:** có số liệu benchmark thật (RPS, p95 latency), chứng minh bằng số liệu lợi ích cache đã làm ở Tuần 7.

### Ngày 48 (Thứ 7, nhiều thời gian hơn) — Deploy VPS + CI/CD
- [ ] Deploy thử lên VPS (hoặc máy local giả lập): Docker Compose + nginx reverse proxy
- [ ] GitHub Actions: build + test (+ push image tùy chọn) — chạy pipeline thật

**Mục tiêu cuối ngày:** pipeline CI chạy xanh; app truy cập qua nginx, không trực tiếp port container.

### Ngày 49 (Chủ nhật, nhiều thời gian hơn) — Hoàn thiện README, demo, tổng kết Giai đoạn 2
- [ ] README cuối: mô tả, cách chạy, ERD, kiến trúc, endpoint, demo luồng chính
- [ ] Đối chiếu "Mốc kiểm tra cuối Giai đoạn 2" ở `dotnet-roadmap.md` — giải thích được global query filter, resource-based vs permission tĩnh, outbox, HMAC, idempotency, trace vs metric
- [ ] Cập nhật bảng tiến độ Tuần 4-10 ở `dotnet-roadmap.md`
- [ ] Ghi nợ kỹ thuật (nếu có) mang sang phần tiếp theo lộ trình

**Mục tiêu cuối tuần (Tuần 10) — Tổng kết Giai đoạn 2:** toàn bộ Hangar (multi-tenant, upload/CI/install link, background job/cache/rate limit, observability, webhook tin cậy) chạy được qua Docker Compose, có demo, README đầy đủ, giải thích được mọi mốc kiểm tra.
