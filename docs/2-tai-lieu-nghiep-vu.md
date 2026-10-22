# Tài liệu nghiệp vụ — Hangar (Project 2, Giai đoạn 2)

> Vai trò tài liệu này giống `Pharse1/CRM/docs/2-tai-lieu-nghiep-vu.md` — là nguồn tham chiếu nghiệp vụ duy nhất khi code, tránh tự suy diễn giữa đường. Cập nhật lại nếu nghiệp vụ thay đổi trong lúc làm.

## 1. Bối cảnh

Hangar là nền tảng nội bộ để build mobile (APK/IPA) được **upload, lưu trữ, phân phối cho tester** mà không cần qua App Store/Play Store. Mỗi công ty/nhóm dùng Hangar quản lý **nhiều tổ chức (Organization)** độc lập — đây là lần đầu trong lộ trình học multi-tenant thật (khác Project 1: 1 hệ thống, 1 bộ role/permission áp dụng toàn cục).

**Khác biệt cốt lõi so với Project 1 cần nắm trước khi code:**
- Project 1: 1 user có đúng 1 role cố định (Admin/Manager/Staff), áp dụng cho toàn hệ thống.
- Hangar: 1 user (1 tài khoản) có thể là **Owner ở Organization A, chỉ là Developer ở Organization B** — vai trò gắn theo **từng Organization**, không gắn theo user toàn cục. JWT chỉ xác định "đây là ai", **không** xác định "họ có quyền gì" (vì quyền phụ thuộc đang thao tác ở Organization nào) — mọi request tới tài nguyên thuộc 1 Organization phải tự tra DB: "user này có là thành viên Organization X không, vai trò gì".

## 2. Domain entities

### Organization
Đơn vị tenant — mọi dữ liệu nghiệp vụ (App, Build, ApiKey, WebhookEndpoint) đều thuộc về đúng 1 Organization.

| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| Name | string | required, maxlength 200 |
| Slug | string | required, unique — dùng trong URL thay vì Id số (ví dụ `/o/acme-corp/apps`) |
| CreatedAtUtc | datetime | |

### OrganizationMember
Vai trò **của 1 user trong 1 Organization cụ thể** — đây là chỗ thay thế "Role" toàn cục của Project 1.

| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| OrganizationId | int | FK |
| UserId | int | FK → AspNetUsers (port từ Project 1) |
| Role | enum | `Owner`, `Developer` — **không** có `Tester` ở đây (xem `AppTester`) |
| JoinedAtUtc | datetime | |

Unique index `(OrganizationId, UserId)` — 1 user chỉ có đúng 1 vai trò trong 1 Organization, không trùng dòng.

### AppTester
Tester **không phải vai trò toàn Organization** — tester chỉ được cấp quyền xem/tải build của **từng App cụ thể** được mời (khác Owner/Developer thấy toàn bộ Organization). Mô hình giống TestFlight/Firebase App Distribution thật.

| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| AppId | int | FK |
| UserId | int | FK |
| InvitedAtUtc | datetime | |

Unique index `(AppId, UserId)`.

### App
| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| OrganizationId | int | FK — cột multi-tenant, mọi query lọc theo field này |
| Name | string | required |
| Platform | enum | `Android`, `iOS` |
| PackageId | string | ví dụ `com.company.app` — required, unique trong 1 Organization |
| CreatedAtUtc | datetime | |

### Build
| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| AppId | int | FK |
| Version | string | ví dụ "1.4.2" |
| BuildNumber | int | tăng dần, dùng để sort "bản mới nhất" |
| StorageKey | string | key/path trong MinIO, không phải URL public trực tiếp |
| FileSizeBytes | long | |
| ReleaseNotes | string? | nullable |
| UploadedByUserId | int? | nullable — null nếu CI upload qua ApiKey |
| UploadedByApiKeyId | int? | nullable — null nếu user upload qua UI |
| CreatedAtUtc | datetime | |

Ràng buộc: **đúng 1 trong 2** cột `UploadedByUserId`/`UploadedByApiKeyId` có giá trị, không cả 2 cùng null hoặc cùng có giá trị — check ở Service layer (giống phong cách Project 1), không ép bằng CHECK constraint DB.

### InstallLink
| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| BuildId | int | FK |
| Token | string | random, unique — dùng trong URL public (`/install/{token}`), không phải Id số (tránh đoán được build khác) |
| ExpiresAtUtc | datetime? | nullable = không hết hạn |
| MaxDownloads | int? | nullable = không giới hạn |
| DownloadCount | int | default 0, tăng dần mỗi lần tải |
| CreatedAtUtc | datetime | |

### ApiKey
| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| OrganizationId | int | FK |
| Name | string | tên gợi nhớ, ví dụ "GitHub Actions CI" |
| HashedKey | string | SHA-256, không lưu key thật — giống `RefreshToken.TokenHash` ở Project 1 |
| CreatedAtUtc | datetime | |
| RevokedAtUtc | datetime? | null = còn hiệu lực |

### WebhookEndpoint (Tuần 9-10)
| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| OrganizationId | int | FK |
| Url | string | |
| Secret | string | dùng ký HMAC payload gửi đi |
| IsActive | bool | |
| CreatedAtUtc | datetime | |

### OutboxEvent (Tuần 9-10)
| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| EventType | string | ví dụ `build.created` |
| PayloadJson | string | |
| CreatedAtUtc | datetime | |
| ProcessedAtUtc | datetime? | null = chưa gửi |

### WebhookDelivery (Tuần 9-10)
| Field | Type | Ghi chú |
|---|---|---|
| Id | int | PK |
| WebhookEndpointId | int | FK |
| OutboxEventId | int | FK |
| Status | enum | `Pending`, `Succeeded`, `Failed` |
| AttemptCount | int | |
| LastAttemptAtUtc | datetime? | |
| ResponseStatusCode | int? | |

## 3. Vai trò và quyền theo Organization (thay cho ma trận permission toàn cục của Project 1)

| Hành động | Owner | Developer | Tester (per-App) |
|---|---|---|---|
| Sửa thông tin Organization, xóa Organization | ✓ | | |
| Mời/xóa thành viên Organization (Owner/Developer) | ✓ | | |
| Tạo/xóa ApiKey | ✓ | | |
| Tạo/sửa/xóa WebhookEndpoint | ✓ | | |
| Tạo/xóa App | ✓ | ✓ | |
| Upload Build (qua UI) | ✓ | ✓ | |
| Mời Tester vào 1 App | ✓ | ✓ | |
| Xem danh sách Build của App | ✓ | ✓ | ✓ (chỉ App được mời) |
| Tạo InstallLink | ✓ | ✓ | |
| Tải build qua InstallLink | ✓ | ✓ | ✓ (chỉ App được mời) |

**Nguyên tắc check quyền:** mọi action đụng tới 1 Organization/App cụ thể phải xác định đúng 2 bước — (1) resource đó thuộc Organization nào (từ route, không tin client gửi OrganizationId tùy ý trong body), (2) user hiện tại có phải thành viên Organization đó không + Role gì — đây chính là **resource-based authorization**, khác hẳn `[Authorize(Policy="...")]` tĩnh của Project 1 vì quyền phụ thuộc **resource cụ thể đang truy cập**, không chỉ loại hành động.

## 4. Multi-tenant — global query filter hoạt động thế nào

Mọi entity có `OrganizationId` (`App`, `ApiKey`, `WebhookEndpoint`, và gián tiếp `Build`/`InstallLink` qua `App`) phải tự động bị lọc theo Organization hiện tại của request — tránh 1 dòng code quên `WHERE OrganizationId = ...` làm lộ dữ liệu Organization khác (lỗi bảo mật nghiêm trọng nếu xảy ra ở multi-tenant thật).

Cơ chế: route luôn có `{organizationId}` (ví dụ `/api/organizations/{organizationId}/apps`) → 1 middleware/action filter đọc giá trị này, set vào 1 scoped service `ICurrentOrganizationContext` **sau khi** đã xác nhận user là thành viên Organization đó → `AppDbContext` nhận `ICurrentOrganizationContext` qua constructor injection, cấu hình `HasQueryFilter(a => a.OrganizationId == _context.OrganizationId)` trong `OnModelCreating`. Từ đó **mọi** câu query tới `App` (và các bảng con) tự động có thêm điều kiện lọc, không cần nhớ viết `.Where(...)` ở từng Service.

`IgnoreQueryFilters()` dùng khi cần nhìn xuyên Organization (ví dụ: job nền quét build cũ của **mọi** Organization ở Tuần 7) — phải dùng có chủ đích, không dùng bừa (mất hết lợi ích cách ly multi-tenant).

## 5. User stories tiêu biểu

- Là Owner, tôi tạo Organization mới, mời đồng nghiệp vào làm Developer.
- Là Developer, tôi tạo App mới trong Organization, upload build APK đầu tiên.
- Là Developer, tôi mời 1 tester (có thể chưa có tài khoản) vào test riêng App của tôi — tester đó không thấy App khác trong cùng Organization nếu không được mời riêng.
- Là CI (GitHub Actions), tôi dùng API key để tự động upload build mỗi lần merge code, không cần đăng nhập user.
- Là Tester, tôi bấm vào link cài đặt nhận qua email/Slack, tải trực tiếp build mới nhất, không cần có tài khoản Hangar.
- Là hệ thống, khi có build mới tôi tự gọi webhook đã đăng ký để báo cho Slack/Discord của team.

## 6. API endpoint sơ bộ (chi tiết hoàn thiện dần theo từng tuần)

| Method | Route | Role yêu cầu | Ghi chú |
|---|---|---|---|
| POST | `/api/organizations` | (user đã login) | Người tạo tự thành Owner |
| GET | `/api/organizations` | (user đã login) | Danh sách Organization user đang là thành viên |
| POST | `/api/organizations/{orgId}/members/invite` | Owner | Gửi email token mời join org |
| POST | `/api/organizations/{orgId}/members/accept` | (user đã login, có token) | Chấp nhận lời mời |
| GET/POST | `/api/organizations/{orgId}/apps` | Owner/Developer (GET), Owner/Developer (POST) | CRUD App, lọc theo `orgId` qua query filter |
| POST | `/api/organizations/{orgId}/apps/{appId}/testers` | Owner/Developer | Mời tester vào 1 App |
| POST | `/api/organizations/{orgId}/apps/{appId}/builds` | Owner/Developer | Upload build qua UI |
| POST | `/api/ci/builds` | ApiKey (header riêng, không phải JWT) | CI upload build |
| GET | `/api/organizations/{orgId}/apps/{appId}/builds` | Owner/Developer/Tester (chỉ app được mời) | |
| POST | `/.../builds/{buildId}/install-links` | Owner/Developer | Tạo link cài đặt |
| GET | `/install/{token}` | Công khai (gate bằng token, không cần JWT) | Tester tải build |
| POST/DELETE | `/api/organizations/{orgId}/api-keys` | Owner | |
| POST/DELETE | `/api/organizations/{orgId}/webhooks` | Owner | Tuần 9-10 |

Chi tiết nghiệp vụ Upload (streaming/presigned URL), rate limiting, background job, webhook retry sẽ bổ sung đúng vào tuần triển khai tương ứng trong `docs/1-ke-hoach-chi-tiet.md` — không viết hết ở đây để tránh thiết kế sai khi chưa thực sự code tới đó (giống cách Project 1 từng làm, chỉ chốt nghiệp vụ core trước, chi tiết kỹ thuật bổ sung dần).
