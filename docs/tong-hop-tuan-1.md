# Tổng hợp Tuần 1 (Tuần 4 theo `dotnet-roadmap.md`) — Dựng nền + multi-tenant

> Code trực tiếp cho toàn bộ Ngày 1-7, verify bằng test thật (curl + DB + MailHog), không chỉ đọc code. File này tổng hợp lại để đọc review — chi tiết từng ngày (kèm nguyên văn code mẫu khi cần) nằm ở `docs/1-ke-hoach-chi-tiet.md`.

## 1. Bối cảnh — vì sao Hangar khác CRM ngay từ auth

CRM (Project 1): 1 user có đúng 1 role cố định (Admin/Manager/Staff), áp dụng toàn hệ thống. Hangar: vai trò (Owner/Developer) gắn theo **từng Organization** — 1 user có thể là Owner ở Organization A, chỉ Developer ở Organization B (giống Slack: admin ở workspace này, member thường ở workspace khác). Hệ quả kéo theo:

- **Không dùng ASP.NET Identity Role** (`AspNetRoles`/`AspNetUserRoles`) — vai trò không phải thuộc tính cố định của user nữa, nằm trong 1 entity riêng (`OrganizationMember`).
- **JWT không nhúng claim `role`** — quyền phụ thuộc Organization đang truy cập, JWT phát lúc login không biết trước request sau sẽ động tới Organization nào. JWT chỉ còn làm nhiệm vụ *authentication* ("đây là ai"), việc *authorization* ("họ có quyền gì ở đây") tách hẳn ra, tra DB theo `{organizationId}` trong route mỗi request.

## 2. File mới theo từng ngày

### Ngày 1 — Port auth (đơn giản hóa)
- `Hangar.Infrastructure/Identity/ApplicationUser.cs`, `RefreshToken.cs` (+ Configuration)
- `Hangar.Application/Auth/*` (interfaces, DTOs, validators) — không có field `Role`/`Roles` ở đâu cả
- `Hangar.Infrastructure/Identity/IdentityService.cs`, `JwtTokenService.cs`
- `Hangar.Infrastructure/Persistence/AppDbContext.cs` — kế thừa `IdentityUserContext<ApplicationUser, int>` (**không phải** `IdentityDbContext<TUser,TRole,TKey>` như CRM)
- `Hangar.Api/Controllers/Auth/AuthController.cs`, `Program.cs` (AddAuthentication/AddJwtBearer/AddAuthorization)

### Ngày 2 — Organization + OrganizationMember
- `Hangar.Domain/Entities/Organization.cs`, `OrganizationMember.cs`, `OrganizationRole.cs` (enum `Owner`/`Developer`)
- `Hangar.Application/Common/Interfaces/IApplicationDbContext.cs` (mới — pattern giống CRM)
- `Hangar.Application/Organizations/*` (Service, DTOs, Result, Validator, Mapping)
- `Hangar.Api/Controllers/Organizations/OrganizationsController.cs` — `POST/GET /api/organizations`

### Ngày 3 — App + global query filter
- `Hangar.Domain/Entities/App.cs`, `AppPlatform.cs`
- `Hangar.Application/Common/Interfaces/ICurrentOrganizationContext.cs` + `Hangar.Infrastructure/Persistence/CurrentOrganizationContext.cs`
- `AppDbContext` cập nhật: nhận `ICurrentOrganizationContext` qua constructor, `HasQueryFilter` cho `App`

### Ngày 4 — Resource-based authorization
- `Hangar.Infrastructure/Authorization/OrganizationMemberRequirement.cs`, `OrganizationMemberAuthorizationHandler.cs`
- `Program.cs`: `AddHttpContextAccessor()`, 2 policy tĩnh (`OrgDeveloper`, `OrgOwner`)

### Ngày 5 — CRUD App
- `Hangar.Application/Apps/*` (Service, DTOs, Result, Validators, Mapping)
- `Hangar.Api/Controllers/Apps/AppsController.cs`

### Ngày 6 — AppTester
- `Hangar.Domain/Entities/AppTester.cs`
- `Hangar.Application/AppTesters/*` (interface + DTOs — chỉ primitive, không đụng `ApplicationUser`)
- `Hangar.Infrastructure/Identity/AppTesterService.cs` (implementation thật — sống ở Infrastructure vì cần JOIN `ApplicationUser`)
- `Hangar.Infrastructure/Authorization/AppAccessRequirement.cs`, `AppAccessAuthorizationHandler.cs`
- `Hangar.Api/Controllers/Apps/TestersController.cs`

### Ngày 7 — Mời thành viên qua email
- `docker-compose.yml` — service `mailhog`
- `Hangar.Application/Common/Interfaces/IEmailSender.cs` + `Hangar.Infrastructure/Email/SmtpEmailSender.cs`, `SmtpSettings.cs`
- `Hangar.Domain/Entities/OrganizationInvite.cs`
- `OrganizationService` mở rộng: `InviteMemberAsync`, `AcceptInviteAsync`
- `OrganizationsController` mở rộng: `POST /api/organizations/{id}/members/invite`, `POST /api/organizations/invites/accept`

## 3. Quyết định kỹ thuật đáng nhớ

| Quyết định | Vì sao |
|---|---|
| `AddIdentityCore<TUser>()` + `IdentityUserContext<TUser,TKey>` thay `AddIdentity<TUser,TRole>()` + `IdentityDbContext<TUser,TRole,TKey>` | Không có role toàn cục — dùng API lõi của Identity, không kéo theo `RoleManager`/bảng Role không bao giờ dùng tới |
| Chỉ 2 policy tĩnh (`OrgDeveloper`, `OrgOwner`), không viết `IAuthorizationPolicyProvider` động như CRM | Chỉ có 2 giá trị `MinimumRole` khả dĩ — policy provider động (CRM Ngày 13) chỉ cần thiết khi có hàng chục permission name khác nhau |
| Global query filter đặt trực tiếp trong `AppDbContext.OnModelCreating`, không đặt trong `AppConfiguration` | `ApplyConfigurationsFromAssembly()` dùng `Activator.CreateInstance`, không qua DI — `IEntityTypeConfiguration<App>` không nhận được `ICurrentOrganizationContext` qua constructor |
| `OrganizationMemberAuthorizationHandler` đọc route qua `IHttpContextAccessor`, không qua `context.Resource` | `context.Resource` không nhất quán giữa middleware-based auth và MVC-filter-based auth — `IHttpContextAccessor` chắc chắn hơn |
| `AppTester`/`OrganizationMember` chỉ có scalar FK (`AppId`/`UserId`), không navigation property | `AppTester.App` (nếu có navigation "required" tới entity có global query filter) gây warning EF Core thật (xem mục 4) — không dùng navigation ở đâu nên bỏ hẳn |
| `AppTesterService` sống ở `Hangar.Infrastructure.Identity`, không ở `Hangar.Application` dùng `IApplicationDbContext` | Cần JOIN với `ApplicationUser` (lấy Email/FullName) — `IApplicationDbContext` (ở Application) không được phép biết tới `ApplicationUser` (Infrastructure); giống lý do `UserManagementService` sống ở Infrastructure trong CRM |
| Token mời (`OrganizationInvite`) **không** trả về trong response API, chỉ gửi qua email | Người gọi API mời (Owner) không phải người được phép dùng token — khác `ApiKey`/`RefreshToken` (trả 1 lần cho đúng người tạo ra nó) |
| `POST /api/organizations/invites/accept` không có `{organizationId}` trong route | Token tự xác định Organization (`TokenHash` unique toàn cục) — người được mời chỉ cần biết token từ email, không cần biết Id Organization |

## 4. Bug thật phát hiện khi code (không phải lỗi lý thuyết, lỗi thật khi build/test)

1. **Enum serialize JSON lệch chiều** — response trả enum dạng chuỗi (`Platform.ToString()`), nhưng request mặc định của System.Text.Json yêu cầu số (`0`/`1`). Gửi `"platform":"Android"` → `400`. Sửa bằng `JsonStringEnumConverter` toàn cục ở `Program.cs`. Phát hiện ngay khi test Create App đầu tiên.
2. **EF Core model-validation warning thật** lúc tạo migration cho `AppTester`: *"Entity 'App' has a global query filter defined and is the required end of a relationship with the entity 'AppTester'..."*. Nguyên nhân: khai báo navigation `AppTester.App` (required) trỏ tới 1 entity có global query filter — EF cảnh báo `Include` có thể cho kết quả sai khi `App` bị filter ra ngoài. Không dùng navigation này ở đâu trong code → bỏ hẳn, chỉ giữ scalar FK `AppId` (giống cách `OrganizationMember.UserId` đã làm từ đầu). Migration tạo lại sạch, không còn warning.

## 5. Test thật đã chạy (curl + DB trực tiếp + MailHog API)

- Đăng ký/đăng nhập, `GET /api/auth/me` → xác nhận JWT **không có claim `role`**.
- Tạo Organization → đúng 1 `OrganizationMember` Role=Owner; trùng Slug → `400`.
- User ngoài (không phải thành viên) gọi API Organization khác → `403`.
- Tạo Organization thứ 2 (chủ khác) → `GetAll Apps` trả **rỗng**, không lộ App của Organization đầu — xác nhận **đồng thời** resource-based authorization (Ngày 4) và global query filter (Ngày 3) hoạt động đúng.
- Tạo App, PackageId trùng trong cùng Organization → `400`; trùng ở Organization khác → vẫn tạo được (đúng đa tenant).
- Mời tester vào 1 App cụ thể → tester `GET` đúng App đó → `200`; tester `GET` toàn bộ danh sách App của Organization → vẫn `403` (Tester không duyệt được cả Organization, chỉ đúng App được mời).
- Mời thành viên mới qua email (`newdev@hangar.local`, **chưa có tài khoản**) → verify mail thật trong MailHog qua REST API (`/api/v2/messages`), decode lấy đúng token → đăng ký tài khoản mới → accept bằng token thật → trở thành `Developer` của Organization → accept lại lần 2 → chặn đúng "đã accept trước đó".

Build `dotnet build` xuyên suốt cả tuần: **0 Warning / 0 Error**.

## 6. Trạng thái hiện tại

- Postgres (`hangar-postgres`, cổng `5436`) và MailHog (`hangar-mailhog`, cổng `1026`/`8026`) đang chạy qua `docker compose`.
- 4 migration đã apply: `InitialCreate`, `AddApp`, `AddAppTesterAndInvite` (đã xóa 1 bản bị lỗi và tạo lại sau khi sửa warning EF).
- Chưa commit git (repo đã `git init`, chưa có commit nào) — đợi bạn đọc/duyệt xong.
- Tiếp theo theo kế hoạch: **Tuần 5 — Upload và lưu trữ** (MinIO, streaming, presigned URL, đọc metadata APK/IPA).
