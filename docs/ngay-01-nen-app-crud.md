# Ngày 1 — Nền: Postgres + entity App + CRUD App

> **Mục tiêu:** có bảng `Apps` trong Postgres và bộ API thêm/xem/sửa/xóa App, mọi lỗi đều trả về cùng một dạng `ProblemDetails`. Từ Ngày 3 mỗi Build phải gắn vào một App, nên App cần có trước.
>
> **Bắt đầu từ:** solution trống sau khi làm lại (4 project, `Program.cs` chỉ có OpenAPI). Phần lớn kỹ thuật đã làm ở CRM, phần lý thuyết tập trung vào những chỗ Hangar khác. Thời gian: khoảng 1,5–2 giờ.
>
> Toàn bộ code và kết quả trong bài đã được chạy thật trên một bản sao (DB riêng) trước khi viết.

## ✅ Xong ngày khi

- [ ] `dotnet build` → `0 Warning(s)`, `0 Error(s)`
- [ ] `\dt` trong Postgres có `Apps` và `__EFMigrationsHistory`
- [ ] `POST /api/apps` hợp lệ → `201`, có header `Location`, `platform` trả về là chuỗi `"Android"` chứ không phải số
- [ ] PackageId sai dạng → `400` có `errors.PackageId`; trùng PackageId → `409`; `GET /api/apps/999` → `404`. Cả 3 cùng một dạng JSON `ProblemDetails`
- [ ] Mở http://localhost:5032/scalar, field `platform` hiện danh sách `Android` / `iOS`
- [ ] Làm xong 2 bài Thử phá

---

## 1. Lý thuyết

### 1.1 App là gì, vì sao làm đầu tiên

App là một ứng dụng mobile, ví dụ "Hangar Demo" chạy Android, package `com.hangar.demo`. Mọi Build (file APK/IPA) từ Ngày 3 đều thuộc về một App. Đến Ngày 6, Hangar đọc package nằm trong chính file APK rồi so với `App.PackageId` để chặn upload nhầm app.

Vì thế `PackageId` là **danh tính** của App, kéo theo 2 quyết định:

- **Unique toàn hệ thống.** Bản cũ unique trong từng Organization, giờ không còn Organization. Nếu có 2 App cùng `com.hangar.demo` thì không biết build mới thuộc App nào.
- **Không cho sửa sau khi tạo.** Sửa PackageId thì các build cũ bỗng thành "build của app khác". Vì vậy `PUT` chỉ đổi được `Name`. `Platform` cũng vậy: một file `.apk` không thể thuộc App iOS.

### 1.2 Một request đi qua những đâu

```
POST /api/apps   {"name":"Hangar Demo","platform":"Android","packageId":"com.hangar.demo"}
   │
   ▼
[Api] UseExceptionHandler ──── exception nào lọt ra ngoài cũng thành 500 ProblemDetails
   │
   ▼
[Api] AppsController    ① đọc JSON → CreateAppRequest   (sai kiểu → 400, chưa vào action)
   │                    ② FluentValidation               (rỗng, sai dạng → 400)
   ▼
[Application] AppService ③ PackageId đã có chưa?         (có → 409)
   │                       tạo App, SaveChangesAsync
   │   chỉ biết interface IApplicationDbContext
   ▼
[Infrastructure] AppDbContext (EF Core + Npgsql) ── INSERT INTO "Apps" ...
   │
   ▼
PostgreSQL               ④ unique index IX_Apps_PackageId  (chốt chặn cuối cùng)
```

Hướng phụ thuộc giữa 4 project: `Api → Infrastructure → Application → Domain` (Api cũng tham chiếu thẳng Application).

Application **không** tham chiếu Infrastructure, nên `AppService` không biết `AppDbContext` hay Npgsql tồn tại. Nó chỉ biết interface `IApplicationDbContext`, do chính Application định nghĩa, còn Infrastructure implement interface đó. Đây là đảo ngược phụ thuộc (dependency inversion): logic nghiệp vụ không bị buộc vào chi tiết lưu trữ.

Interface vẫn dùng kiểu `DbSet` của EF Core. Ta chấp nhận đánh đổi này như ở CRM, để viết LINQ thẳng trong Service mà không phải bọc thêm một lớp repository.

### 1.3 EF Core cần 4 thứ

| Thứ | File hôm nay | Vai trò |
|---|---|---|
| Entity | `App.cs` | class C# tương ứng với 1 dòng trong bảng |
| DbContext | `AppDbContext.cs` | phiên làm việc với DB: query, theo dõi thay đổi, `SaveChangesAsync` |
| Configuration | `AppConfiguration.cs` | luật của bảng: độ dài, index, kiểu cột. Tách khỏi entity để Domain không dính chi tiết DB |
| Migration | `Migrations/*_InitialCreate.cs` | file C# sinh tự động, mô tả cách đưa DB từ trạng thái cũ sang mới. `MigrateAsync()` lúc khởi động sẽ chạy các migration chưa chạy |

### 1.4 Bốn lớp kiểm tra dữ liệu, mỗi lớp bắt một loại lỗi

| Lớp | Ở đâu | Bắt lỗi gì | Ví dụ |
|---|---|---|---|
| ① Đọc JSON | MVC, trước khi vào action | sai kiểu, thiếu field bắt buộc | `"platform":"Windows"` |
| ② FluentValidation | Validator | sai định dạng/độ dài — không cần hỏi DB | `com.my-app` cho Android |
| ③ Kiểm tra trong Service | `AppService` | luật phải hỏi DB mới biết | PackageId đã có người dùng |
| ④ Ràng buộc DB | unique index | những gì lọt qua ③ | 2 request cùng lúc (Thử phá 2) |

Lớp ③ có để trả lời lỗi dễ hiểu (`409` kèm thông báo). Lớp ④ có để dữ liệu không bao giờ sai. Thiếu ④ thì ③ có lỗ hổng khi 2 request tới cùng lúc.

### 1.5 ProblemDetails: mọi lỗi cùng một dạng

`ProblemDetails` là định dạng JSON chuẩn cho lỗi HTTP (RFC 9457). Nhờ nó, client (frontend, script CI) chỉ cần đọc một kiểu: `status`, `title`, `detail`, và `errors` khi lỗi validate. Hôm nay có 4 cách sinh ra nó:

| Status | Code sinh ra |
|---|---|
| 400 | `ValidationProblem(ModelState)` sau khi chép lỗi FluentValidation vào `ModelState` |
| 404 | `NotFound()`, được `[ApiController]` tự đổi thành ProblemDetails |
| 409 | `Problem(statusCode: 409, ...)` |
| 500 | `GlobalExceptionHandler` |

### 1.6 Hai chi tiết nhỏ dễ sai

- **Enum bắt đầu từ 1.** Client quên gửi `platform` thì `AppPlatform` (là kiểu giá trị) nhận giá trị mặc định `0`. Nếu `Android = 0`, App bị tạo thành Android mà không ai hay biết. Bắt đầu từ 1 để `0` không hợp lệ, khi đó `IsInEnum()` sẽ báo lỗi (Thử phá 1).
- **Enum dạng chuỗi phải cấu hình ở 3 chỗ:**
  - JSON request/response của controller: `AddJsonOptions`.
  - Tài liệu OpenAPI/Scalar: `ConfigureHttpJsonOptions`. Trình sinh OpenAPI đọc cấu hình này chứ không đọc của MVC. Thiếu dòng này, tài liệu ghi `platform` là `integer` dù API thật trả `"Android"`.
  - Cột DB: `HasConversion<string>()`. Xem bảng thấy chữ `Android` thay vì số `1`, và thêm hay đổi thứ tự enum sau này không làm sai dữ liệu cũ.

---

## 2. Thực hành

### Bước 1 — Cài package

```powershell
cd F:\Study\dotnet-projects\Pharse2\Hangar
dotnet add src/Hangar.Application package Microsoft.EntityFrameworkCore
dotnet add src/Hangar.Application package FluentValidation
dotnet add src/Hangar.Application package FluentValidation.DependencyInjectionExtensions
dotnet add src/Hangar.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Hangar.Api package Microsoft.EntityFrameworkCore.Design
dotnet add src/Hangar.Api package Scalar.AspNetCore
dotnet tool update --global dotnet-ef
```

| Package | Cài vào | Vì sao ở project này |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | Application | `IApplicationDbContext` dùng kiểu `DbSet`, và `AppService` viết LINQ (`AnyAsync`, `ToListAsync`) |
| `FluentValidation` + `...DependencyInjectionExtensions` | Application | validator là luật nghiệp vụ nên nằm ở Application; gói thứ 2 cung cấp `AddValidatorsFromAssembly` |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Infrastructure | driver Postgres — chi tiết lưu trữ, chỉ Infrastructure biết |
| `Microsoft.EntityFrameworkCore.Design` | Api | lệnh `dotnet ef` chạy thông qua startup project (nơi có connection string), nên gói này phải nằm ở Api |
| `Scalar.AspNetCore` | Api | giao diện web để đọc tài liệu và gọi thử API |

**💡 Vì sao update `dotnet-ef`:** máy bạn đang có tool `10.0.12`, còn package EF mới nhất là `10.0.14`. Không update thì lệnh vẫn chạy, nhưng lần nào cũng in cảnh báo `tools version '10.0.12' is older than that of the runtime '10.0.14'`.

### Bước 2 — Domain: 2 file

`File: src/Hangar.Domain/Entities/AppPlatform.cs` (file mới)

```csharp
namespace Hangar.Domain.Entities;

public enum AppPlatform
{
    Android = 1,
    iOS = 2
}
```

**Là gì:** 2 nền tảng mà Hangar hỗ trợ.
**Vì sao cần:** quyết định App nhận file `.apk` hay `.ipa` (Ngày 6), và dùng regex nào để kiểm PackageId. Bắt đầu từ `1` để giá trị mặc định `0` của request thiếu field bị coi là sai (mục 1.6).

`File: src/Hangar.Domain/Entities/App.cs` (file mới)

```csharp
namespace Hangar.Domain.Entities;

public class App
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public AppPlatform Platform { get; set; }

    public required string PackageId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
```

**Là gì:** entity, tương ứng 1 dòng trong bảng `Apps`. Domain không tham chiếu thư viện nào nên chỉ là class C# thuần.
**Vì sao cần:** đây là dữ liệu gốc mà mọi tầng khác xoay quanh.
- `required` làm C# bắt buộc gán `Name`/`PackageId` khi viết `new App { ... }` (quên thì lỗi compile `CS9035`), và EF hiểu đây là cột `NOT NULL`.
- Đuôi `Utc` nhắc rằng giờ luôn lưu theo UTC (xem Lỗi hay gặp).
- Chưa có danh sách `Builds` vì entity Build ra đời ở Ngày 3.

### Bước 3 — Application: 11 file

`File: src/Hangar.Application/Common/Interfaces/IApplicationDbContext.cs` (file mới)

```csharp
using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hangar.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<App> Apps { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

**Là gì:** "hợp đồng" Application cần từ DB: một bảng `Apps` để query và một lệnh lưu.
**Vì sao cần:** Application không được tham chiếu Infrastructure, vì Infrastructure đã tham chiếu Application và tham chiếu ngược lại sẽ thành vòng. Nên Application tự định nghĩa thứ nó cần, để Infrastructure đáp ứng (mục 1.2). Chỉ có `{ get; }` vì Service không bao giờ được gán lại cả một `DbSet`.

`File: src/Hangar.Application/Apps/Dtos/AppDto.cs` (file mới)

```csharp
using Hangar.Domain.Entities;

namespace Hangar.Application.Apps.Dtos;

public record AppDto(int Id, string Name, AppPlatform Platform, string PackageId, DateTime CreatedAtUtc);
```

**Là gì:** hình dạng dữ liệu App mà API trả về cho client.
**Vì sao cần:** tách khỏi entity, để sau này entity có thêm field nội bộ (danh sách Builds ở Ngày 3) thì response không tự phình ra, và client không phụ thuộc cấu trúc bảng. Dùng `record` vì DTO chỉ chở dữ liệu, không có hành vi.

`File: src/Hangar.Application/Apps/Dtos/CreateAppRequest.cs` (file mới)

```csharp
using Hangar.Domain.Entities;

namespace Hangar.Application.Apps.Dtos;

public record CreateAppRequest(string Name, AppPlatform Platform, string PackageId);
```

**Là gì:** dữ liệu client gửi lên khi tạo App.
**Vì sao cần:** chỉ nhận đúng 3 field client được phép quyết định. `Id` và `CreatedAtUtc` do server sinh, client không gửi được.

`File: src/Hangar.Application/Apps/Dtos/UpdateAppRequest.cs` (file mới)

```csharp
namespace Hangar.Application.Apps.Dtos;

public record UpdateAppRequest(string Name);
```

**Là gì:** dữ liệu khi sửa App, chỉ có `Name`.
**Vì sao cần:** đây chính là chỗ thể hiện quyết định "PackageId và Platform là danh tính, không cho sửa" (mục 1.1). Client có gửi thêm `packageId` thì field đó cũng bị bỏ qua, vì request không có chỗ nhận nó.

`File: src/Hangar.Application/Apps/AppMappings.cs` (file mới)

```csharp
using Hangar.Application.Apps.Dtos;
using Hangar.Domain.Entities;

namespace Hangar.Application.Apps;

public static class AppMappings
{
    public static AppDto ToDto(this App app) =>
        new(app.Id, app.Name, app.Platform, app.PackageId, app.CreatedAtUtc);
}
```

**Là gì:** extension method chuyển entity sang DTO.
**Vì sao cần:** cả 4 method của Service đều trả `AppDto`. Gom việc chuyển đổi về một chỗ, khi DTO thêm field thì chỉ sửa ở đây. Viết tay thay vì dùng AutoMapper vì chỉ có 5 field, đọc là hiểu ngay.

`File: src/Hangar.Application/Apps/Validators/CreateAppRequestValidator.cs` (file mới)

```csharp
using System.Text.RegularExpressions;
using FluentValidation;
using Hangar.Application.Apps.Dtos;
using Hangar.Domain.Entities;

namespace Hangar.Application.Apps.Validators;

public class CreateAppRequestValidator : AbstractValidator<CreateAppRequest>
{
    private static readonly Regex AndroidPackage = new(@"^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$");
    private static readonly Regex IosBundleId = new(@"^[a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)+$");

    public CreateAppRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Platform)
            .IsInEnum()
            .WithMessage("Platform phải là Android hoặc iOS.");

        RuleFor(x => x.PackageId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(200)
            .Must(HaveValidFormat)
            .WithMessage(x => x.Platform == AppPlatform.iOS
                ? "Bundle ID iOS có dạng com.company.app: chỉ gồm chữ, số, dấu - và dấu chấm."
                : "PackageId Android có dạng com.company.app: chỉ gồm chữ, số, dấu _, mỗi phần bắt đầu bằng chữ cái.");
    }

    private static bool HaveValidFormat(CreateAppRequest request, string packageId) => request.Platform switch
    {
        AppPlatform.Android => AndroidPackage.IsMatch(packageId),
        AppPlatform.iOS => IosBundleId.IsMatch(packageId),
        _ => true
    };
}
```

**Là gì:** luật kiểm tra lớp ② (mục 1.4), những gì kiểm được mà không cần hỏi DB.
**Vì sao cần:**
- **Luật PackageId khác nhau theo nền tảng.** Android không cho dấu `-` và mỗi phần phải bắt đầu bằng chữ cái. iOS lại cho dấu `-`. Vì vậy `com.my-app.ios` hợp lệ với iOS nhưng `com.my-app` sai với Android.
- `Cascade(CascadeMode.Stop)`: rỗng thì chỉ báo "rỗng", không báo thêm "sai định dạng".
- `_ => true` trong `switch`: Platform sai đã có rule `IsInEnum` báo riêng, không báo trùng 2 lần.
- `static readonly Regex`: tạo đối tượng regex 1 lần dùng mãi, không tạo lại mỗi request.

`File: src/Hangar.Application/Apps/Validators/UpdateAppRequestValidator.cs` (file mới)

```csharp
using FluentValidation;
using Hangar.Application.Apps.Dtos;

namespace Hangar.Application.Apps.Validators;

public class UpdateAppRequestValidator : AbstractValidator<UpdateAppRequest>
{
    public UpdateAppRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);
    }
}
```

**Là gì:** luật cho request sửa App.
**Vì sao cần:** `MaximumLength(200)` khớp với cột `character varying(200)` ở Bước 4. Không chặn từ đây thì tên dài 300 ký tự sẽ để DB báo lỗi, client nhận `500` thay vì `400`.

`File: src/Hangar.Application/Apps/CreateAppResult.cs` (file mới)

```csharp
using Hangar.Application.Apps.Dtos;

namespace Hangar.Application.Apps;

public record CreateAppResult(bool Succeeded, AppDto? App, string? Error);
```

**Là gì:** kết quả của thao tác tạo App: thành công kèm App, hoặc thất bại kèm lý do.
**Vì sao cần:** trùng PackageId là tình huống **bình thường** của nghiệp vụ chứ không phải sự cố, nên Service trả kết quả rõ ràng thay vì `throw`. Controller đọc kết quả rồi chọn `201` hay `409`. Exception để dành cho lỗi thật (mất kết nối DB...), những lỗi đó rơi vào `500`.

`File: src/Hangar.Application/Apps/IAppService.cs` (file mới)

```csharp
using Hangar.Application.Apps.Dtos;

namespace Hangar.Application.Apps;

public interface IAppService
{
    Task<IReadOnlyList<AppDto>> GetAllAsync();
    Task<AppDto?> GetByIdAsync(int id);
    Task<CreateAppResult> CreateAsync(CreateAppRequest request);
    Task<AppDto?> UpdateAsync(int id, UpdateAppRequest request);
    Task<bool> DeleteAsync(int id);
}
```

**Là gì:** danh sách use case của App.
**Vì sao cần:** controller phụ thuộc interface chứ không phụ thuộc class cụ thể. Đọc file này là biết App làm được gì mà không cần xem code bên trong.
- `null` nghĩa là không tìm thấy (controller trả `404`).
- `bool` của `DeleteAsync` nghĩa là có xóa được hay không.

`File: src/Hangar.Application/Apps/AppService.cs` (file mới)

```csharp
using Hangar.Application.Apps.Dtos;
using Hangar.Application.Common.Interfaces;
using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hangar.Application.Apps;

public class AppService : IAppService
{
    private readonly IApplicationDbContext _context;

    public AppService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AppDto>> GetAllAsync()
    {
        var apps = await _context.Apps
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync();

        return apps.Select(a => a.ToDto()).ToList();
    }

    public async Task<AppDto?> GetByIdAsync(int id)
    {
        var app = await _context.Apps
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        return app?.ToDto();
    }

    public async Task<CreateAppResult> CreateAsync(CreateAppRequest request)
    {
        var packageTaken = await _context.Apps.AnyAsync(a => a.PackageId == request.PackageId);
        if (packageTaken)
        {
            return new CreateAppResult(false, null, $"PackageId '{request.PackageId}' đã được đăng ký cho App khác.");
        }

        var app = new App
        {
            Name = request.Name,
            Platform = request.Platform,
            PackageId = request.PackageId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Apps.Add(app);
        await _context.SaveChangesAsync();

        return new CreateAppResult(true, app.ToDto(), null);
    }

    public async Task<AppDto?> UpdateAsync(int id, UpdateAppRequest request)
    {
        var app = await _context.Apps.FirstOrDefaultAsync(a => a.Id == id);
        if (app is null)
        {
            return null;
        }

        app.Name = request.Name;
        await _context.SaveChangesAsync();

        return app.ToDto();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var app = await _context.Apps.FirstOrDefaultAsync(a => a.Id == id);
        if (app is null)
        {
            return false;
        }

        _context.Apps.Remove(app);
        await _context.SaveChangesAsync();

        return true;
    }
}
```

**Là gì:** logic nghiệp vụ của App, tức lớp ③ ở mục 1.4.
**Vì sao cần:**
- `AnyAsync` kiểm tra trùng trước khi tạo, để trả `409` với lời giải thích rõ ràng. Nó **chưa đủ** khi 2 request tới cùng lúc (Thử phá 2).
- `AsNoTracking()` ở 2 hàm đọc: chỉ đọc rồi trả về, không sửa gì, nên bảo EF khỏi tốn công theo dõi thay đổi.
- `UpdateAsync`/`DeleteAsync` **không** dùng `AsNoTracking`, vì EF cần theo dõi entity thì `SaveChangesAsync` mới biết phải `UPDATE`/`DELETE` dòng nào.
- `Id` có giá trị ngay sau `SaveChangesAsync`, vì Postgres sinh Id rồi EF ghi ngược lại vào object `app`.

`File: src/Hangar.Application/DependencyInjection.cs` (file mới)

```csharp
using FluentValidation;
using Hangar.Application.Apps;
using Microsoft.Extensions.DependencyInjection;

namespace Hangar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAppService, AppService>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
```

**Là gì:** nơi Application tự khai báo các service của mình với DI container.
**Vì sao cần:**
- `Program.cs` chỉ cần gọi `AddApplication()`, không phải biết bên trong có những class nào.
- `Scoped` nghĩa là mỗi request một instance, cùng vòng đời với DbContext mà nó dùng.
- `AddValidatorsFromAssembly` tự tìm và đăng ký mọi validator trong project. Ngày 3 thêm validator mới thì không phải sửa file này.

### Bước 4 — Infrastructure: 3 file

`File: src/Hangar.Infrastructure/Persistence/AppDbContext.cs` (file mới)

```csharp
using Hangar.Application.Common.Interfaces;
using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hangar.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<App> Apps => Set<App>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

**Là gì:** DbContext thật của EF Core, đồng thời implement `IApplicationDbContext`.
**Vì sao cần:**
- Đây là cầu nối giữa interface ở Application và Postgres.
- `SaveChangesAsync` không cần viết lại, vì lớp cha `DbContext` đã có method đúng chữ ký mà interface yêu cầu.
- `ApplyConfigurationsFromAssembly` tự nạp mọi class `IEntityTypeConfiguration` trong project. Thêm bảng Build ở Ngày 3 chỉ cần thêm file config, không phải sửa ở đây.

`File: src/Hangar.Infrastructure/Persistence/Configurations/AppConfiguration.cs` (file mới)

```csharp
using Hangar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hangar.Infrastructure.Persistence.Configurations;

public class AppConfiguration : IEntityTypeConfiguration<App>
{
    public void Configure(EntityTypeBuilder<App> builder)
    {
        builder.Property(a => a.Name)
            .HasMaxLength(200);

        builder.Property(a => a.PackageId)
            .HasMaxLength(200);

        builder.Property(a => a.Platform)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(a => a.PackageId)
            .IsUnique();
    }
}
```

**Là gì:** luật của bảng `Apps` ở phía DB.
**Vì sao cần:**
- `HasMaxLength(200)` giúp cột có kiểu `character varying(200)` thay vì `text` không giới hạn, và khớp với validator.
- `HasConversion<string>()` lưu `Android`/`iOS` dạng chữ (mục 1.6).
- `HasIndex(...).IsUnique()` là lớp ④ (mục 1.4): Postgres từ chối dòng trùng, dù code có sơ hở.
- Không cần `IsRequired()`, vì `required string` không-nullable ở entity đã đủ để EF tạo cột `NOT NULL`.

`File: src/Hangar.Infrastructure/DependencyInjection.cs` (file mới)

```csharp
using Hangar.Application.Common.Interfaces;
using Hangar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hangar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Chưa cấu hình ConnectionStrings:Default.");
        }

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        return services;
    }
}
```

**Là gì:** nơi Infrastructure khai báo DbContext với DI container.
**Vì sao cần:**
- Dòng `AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>())`: khi `AppService` xin `IApplicationDbContext` thì nhận **đúng instance** `AppDbContext` của request đó, không tạo cái thứ hai.
- Kiểm tra `IsNullOrWhiteSpace` chứ không chỉ kiểm `null`, vì `appsettings.json` để `"Default": ""`. Chuỗi rỗng không phải `null`, không kiểm thì app vẫn khởi động bình thường rồi tới lúc truy cập DB mới hỏng. Kiểm ở đây thì app dừng ngay lúc khởi động, kèm câu lỗi rõ ràng.

### Bước 5 — Api: 4 file

`File: src/Hangar.Api/Middleware/GlobalExceptionHandler.cs` (file mới — giống CRM)

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hangar.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        IHostEnvironment environment,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Đã có lỗi không mong muốn.",
                Detail = _environment.IsDevelopment()
                    ? exception.Message
                    : "Vui lòng thử lại sau."
            }
        });
    }
}
```

**Là gì:** chỗ bắt mọi exception không ai bắt, biến nó thành `500` dạng ProblemDetails.
**Vì sao cần:**
- Thiếu nó, client nhận một trang lỗi hoặc response rỗng, sai định dạng chung.
- Ghi log đầy đủ stack trace cho người sửa lỗi.
- Chỉ lộ `exception.Message` ở Development. Message thật có thể chứa tên bảng hoặc chi tiết kết nối, không được lộ ra ngoài ở Production.

`File: src/Hangar.Api/Controllers/AppsController.cs` (file mới)

```csharp
using FluentValidation;
using FluentValidation.Results;
using Hangar.Application.Apps;
using Hangar.Application.Apps.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Hangar.Api.Controllers;

[ApiController]
[Route("api/apps")]
public class AppsController : ControllerBase
{
    private readonly IAppService _appService;
    private readonly IValidator<CreateAppRequest> _createValidator;
    private readonly IValidator<UpdateAppRequest> _updateValidator;

    public AppsController(
        IAppService appService,
        IValidator<CreateAppRequest> createValidator,
        IValidator<UpdateAppRequest> updateValidator)
    {
        _appService = appService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var apps = await _appService.GetAllAsync();
        return Ok(apps);
    }

    [HttpGet("{appId:int}")]
    public async Task<IActionResult> GetById(int appId)
    {
        var app = await _appService.GetByIdAsync(appId);
        return app is null ? NotFound() : Ok(app);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAppRequest request)
    {
        var validation = await _createValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationFailed(validation);
        }

        var result = await _appService.CreateAsync(request);
        if (!result.Succeeded)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "PackageId đã tồn tại.", detail: result.Error);
        }

        return CreatedAtAction(nameof(GetById), new { appId = result.App!.Id }, result.App);
    }

    [HttpPut("{appId:int}")]
    public async Task<IActionResult> Update(int appId, UpdateAppRequest request)
    {
        var validation = await _updateValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return ValidationFailed(validation);
        }

        var app = await _appService.UpdateAsync(appId, request);
        return app is null ? NotFound() : Ok(app);
    }

    [HttpDelete("{appId:int}")]
    public async Task<IActionResult> Delete(int appId)
    {
        var deleted = await _appService.DeleteAsync(appId);
        return deleted ? NoContent() : NotFound();
    }

    private IActionResult ValidationFailed(ValidationResult validation)
    {
        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}
```

**Là gì:** cửa vào HTTP của App. Nhận request, gọi validator và Service, rồi chọn status code.
**Vì sao cần:**
- `[ApiController]`:
  - tự đọc body JSON vào `CreateAppRequest` (không cần `[FromBody]`);
  - tự trả `400` khi JSON sai kiểu (lớp ①);
  - tự đổi `NotFound()` thành ProblemDetails.
- `ValidationFailed` chép lỗi FluentValidation vào `ModelState` rồi gọi `ValidationProblem`. Nhờ vậy lỗi lớp ② có **cùng dạng** với lỗi lớp ① mà MVC tự sinh, client chỉ đọc một kiểu.
  - Bản cũ trả `BadRequest(validation.Errors)`, ra một mảng JSON khác hẳn định dạng chung.
- `CreatedAtAction` trả `201` kèm header `Location` trỏ tới App mới.
  - Đặt tên action là `GetById`, **không** `GetByIdAsync`, xem Lỗi hay gặp.
- `{appId:int}` chặn `/api/apps/abc` ngay ở tầng routing, ra `404`.
- Controller không chứa luật nghiệp vụ nào, chỉ chuyển kết quả thành status code.

`File: src/Hangar.Api/Program.cs` (sửa file đã có — thay toàn bộ)

```csharp
using System.Text.Json.Serialization;
using Hangar.Api.Middleware;
using Hangar.Application;
using Hangar.Infrastructure;
using Hangar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();

app.Run();
```

**Là gì:** composition root, nơi ghép mọi tầng lại với nhau và dựng pipeline xử lý request.
**Vì sao cần, từng khối:**
- **2 dòng `JsonStringEnumConverter`:** dòng đầu cho JSON thật của controller, dòng thứ hai cho tài liệu OpenAPI/Scalar (mục 1.6).
- **`AddProblemDetails` + `AddExceptionHandler` + `UseExceptionHandler`:** bật cơ chế lỗi `500` dạng ProblemDetails. `UseExceptionHandler` đặt **trước** `MapControllers` để bọc được mọi thứ phía sau.
- **Khối `MigrateAsync`:** mỗi lần khởi động, tự chạy các migration chưa chạy, nên không cần gõ `dotnet ef database update`. Cần `CreateScope()` vì DbContext là service theo request (scoped), mà lúc này chưa có request nào.
- **`MapOpenApi` + `MapScalarApiReference` chỉ ở Development:** không công khai tài liệu API ra ngoài khi chạy thật.
- **Không có `UseHttpsRedirection`:** profile `http` không có cổng HTTPS, giữ dòng này chỉ sinh cảnh báo trong log.

`File: src/Hangar.Api/Hangar.Api.http` (sửa file đã có — thay toàn bộ, tùy chọn)

```http
@Hangar.Api_HostAddress = http://localhost:5032

### Danh sách App
GET {{Hangar.Api_HostAddress}}/api/apps

### Tạo App Android
POST {{Hangar.Api_HostAddress}}/api/apps
Content-Type: application/json

{
  "name": "Hangar Demo",
  "platform": "Android",
  "packageId": "com.hangar.demo"
}

### PackageId sai dạng (Android không cho dấu -)
POST {{Hangar.Api_HostAddress}}/api/apps
Content-Type: application/json

{
  "name": "Sai",
  "platform": "Android",
  "packageId": "com.my-app"
}

### Xem 1 App
GET {{Hangar.Api_HostAddress}}/api/apps/1

### Đổi tên
PUT {{Hangar.Api_HostAddress}}/api/apps/1
Content-Type: application/json

{
  "name": "Hangar Demo 2"
}

### Xóa
DELETE {{Hangar.Api_HostAddress}}/api/apps/1
```

**Là gì:** các request mẫu để bấm gửi ngay trong editor. VS Code cần extension **REST Client**; Visual Studio và Rider có sẵn tính năng này.
**Vì sao cần:** gọi lại một request chỉ mất một cú bấm, không phải gõ lại JSON mỗi lần như trên Scalar.

### Bước 6 — Migration, chạy, kiểm chứng

**Làm:**

```powershell
cd F:\Study\dotnet-projects\Pharse2\Hangar
docker compose up -d
dotnet build
dotnet ef migrations add InitialCreate --project src/Hangar.Infrastructure --startup-project src/Hangar.Api
```

Kết quả: `Done. To undo this action, use 'ef migrations remove'`, và có 3 file mới trong `src/Hangar.Infrastructure/Migrations/`. Mở file `*_InitialCreate.cs` để đối chiếu: có `CreateTable("Apps")`, cột `Platform` kiểu `character varying(20)`, và `CreateIndex("IX_Apps_PackageId", ... unique: true)`.

**💡 Vì sao 2 tham số:** `--project` chỉ nơi chứa DbContext, migration sinh ra sẽ nằm ở đó. `--startup-project` chỉ project chạy được, có connection string và package `Design`. `dotnet ef` không cần bạn set biến môi trường gì, vì nó mặc định chạy ở Development nên đọc được `appsettings.Development.json`.

**Chạy:**

```powershell
cd src/Hangar.Api
dotnet run
```

Mở cửa sổ terminal khác, kiểm tra bảng:

```powershell
docker exec hangar-postgres psql -U hangar_user -d hangar_db -c "\dt"
```

```
 public | Apps                  | table | hangar_user
 public | __EFMigrationsHistory | table | hangar_user
```

**Chạy thử** bằng file `.http`, Scalar (http://localhost:5032/scalar) hoặc `curl` trong **Git Bash**. Dưới đây là kết quả thật:

```bash
curl -i -H "Content-Type: application/json" \
  -d '{"name":"Hangar Demo","platform":"Android","packageId":"com.hangar.demo"}' \
  http://localhost:5032/api/apps
```

```
HTTP/1.1 201 Created
Location: http://localhost:5032/api/apps/1
{"id":1,"name":"Hangar Demo","platform":"Android","packageId":"com.hangar.demo","createdAtUtc":"2026-11-09T15:17:21.660488Z"}
```

Gửi lại đúng lệnh trên một lần nữa:

```
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.10","title":"PackageId đã tồn tại.","status":409,"detail":"PackageId 'com.hangar.demo' đã được đăng ký cho App khác.","traceId":"00-97d3..."}
```

PackageId có dấu `-` cho Android:

```bash
curl -s -H "Content-Type: application/json" \
  -d '{"name":"X","platform":"Android","packageId":"com.my-app"}' \
  http://localhost:5032/api/apps
```

```
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"PackageId":["PackageId Android có dạng com.company.app: chỉ gồm chữ, số, dấu _, mỗi phần bắt đầu bằng chữ cái."]},"traceId":"00-e809..."}
```

Đổi `"platform":"iOS"` với `"packageId":"com.my-app.ios"` thì ra `201`, vì iOS cho phép dấu `-`.

```bash
curl -s http://localhost:5032/api/apps/999
```

```
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,"traceId":"00-94ac..."}
```

`PUT` đổi tên → `200`; `PUT` với `"name":""` → `400` `'Name' must not be empty.`; `DELETE` → `204`, `DELETE` lần 2 → `404`.

**💡 Vì sao:** 400, 404, 409 đều có `type`, `title`, `status`, `traceId`. Đó là định dạng chung ở mục 1.5. Cái `traceId` giúp tìm đúng dòng log của request bị lỗi.

### Bước 7 — Commit

Phần dọn project hôm trước (xóa code cũ) vẫn chưa commit. Nên commit riêng phần đó trước, rồi mới commit Ngày 1:

```powershell
git add -A
git commit -m "Làm lại từ đầu: dọn code auth/org, plan mới"
# ... làm Ngày 1 ...
git add -A
git commit -m "Ngày 1: entity App + CRUD App"
```

---

## 3. Thử phá

### Thử 1 — Enum bắt đầu từ 0

**Làm:** trong `AppPlatform.cs`, xóa `= 1` và `= 2`. Restart app rồi gửi request **không có** field `platform`:

```bash
curl -s -H "Content-Type: application/json" \
  -d '{"name":"Quen platform","packageId":"com.quen.platform"}' \
  http://localhost:5032/api/apps
```

**Dự đoán trước:** `400` hay `201`?

<details>
<summary>Kết quả thật</summary>

```
{"id":...,"name":"Quen platform","platform":"Android","packageId":"com.quen.platform",...}   [201]
```

App được tạo thành **Android**, dù client không hề nói nó là Android. `AppPlatform` là kiểu giá trị, field thiếu thì nhận giá trị mặc định `0`. Khi `Android = 0` thì `0` là một giá trị hợp lệ, nên `IsInEnum()` cho qua.

Với `Android = 1`, cùng request đó trả `400` với `"Platform":["Platform phải là Android hoặc iOS."]`.

Nhớ hoàn tác lại `= 1`, `= 2`. Cột DB lưu dạng chuỗi nên không cần tạo migration mới.
</details>

### Thử 2 — 20 request cùng tạo một PackageId

**Làm:** trong `AppService.CreateAsync`, thêm 1 dòng **ngay sau** khối `if (packageTaken) { ... }`:

```csharp
await Task.Delay(500);
```

Restart app, rồi bắn 20 request cùng lúc (Git Bash):

```bash
seq 20 | xargs -P 20 -I{} curl -s -o /dev/null -w "%{http_code}\n" \
  -H "Content-Type: application/json" \
  -d '{"name":"Race","platform":"Android","packageId":"com.race.test"}' \
  http://localhost:5032/api/apps | sort | uniq -c
```

**Dự đoán trước:** bao nhiêu `201`? Trong DB có bao nhiêu dòng `com.race.test`?

<details>
<summary>Kết quả thật</summary>

```
      1 201
     19 500
```

DB có **đúng 1 dòng**. Log có `23505: duplicate key value violates unique constraint "IX_Apps_PackageId"`.

**Chuyện gì đã xảy ra:**
1. Cả 20 request đều chạy `AnyAsync` trong lúc chưa request nào kịp `INSERT`, nên cả 20 đều thấy "chưa trùng".
2. `Task.Delay` chỉ nới rộng khoảng hở đó, để lần nào chạy cũng thấy được. Không có nó, lỗi này vẫn xảy ra nhưng chỉ thỉnh thoảng: ở lần thử đầu tiên ngay sau khi khởi động, 14/20 request bị `500`.
3. Unique index (lớp ④) giữ dữ liệu đúng: chỉ 1 dòng được ghi.
4. Nhưng 19 client còn lại nhận `500` "lỗi hệ thống", trong khi thực chất là `409` "đã có người dùng PackageId này".

**Sửa:** bắt lỗi khi lưu, hỏi lại DB xem có đúng là vừa bị ai đó chiếm PackageId không. Trong `CreateAsync`, thay 2 dòng `_context.Apps.Add(app); await _context.SaveChangesAsync();` bằng:

```csharp
_context.Apps.Add(app);
try
{
    await _context.SaveChangesAsync();
}
catch (DbUpdateException)
{
    var takenByOther = await _context.Apps.AnyAsync(a => a.PackageId == request.PackageId);
    if (!takenByOther)
    {
        throw;
    }

    return new CreateAppResult(false, null, $"PackageId '{request.PackageId}' đã được đăng ký cho App khác.");
}
```

Chạy lại lệnh bắn 20 request (đổi packageId thành `com.race.test2`):

```
      1 201
     19 409
```

**Vì sao không bắt thẳng `PostgresException` với mã `23505`:** kiểu đó nằm trong Npgsql, mà Application không được biết Npgsql (mục 1.2). Hỏi lại DB bằng `AnyAsync` cho cùng kết quả mà vẫn giữ đúng ranh giới giữa các tầng. Còn `throw;` để những lỗi lưu khác (mất kết nối...) vẫn đi tiếp thành `500`, không bị nuốt mất. Không viết `catch ... when (await ...)` được, vì C# không cho `await` trong `when` (lỗi `CS7094`).

Sau khi sửa: **xóa `Task.Delay`, giữ lại try/catch.** Log vẫn có dòng `fail: Microsoft.EntityFrameworkCore.Database.Command` cho các INSERT bị từ chối. Đó là EF ghi lại câu SQL thất bại, không phải lỗi chưa xử lý.
</details>

---

## 4. Tự kiểm tra

<details><summary>1. Vì sao <code>AppService</code> nhận <code>IApplicationDbContext</code> mà không nhận thẳng <code>AppDbContext</code>?</summary>

`AppDbContext` nằm ở Infrastructure, mà Infrastructure đã tham chiếu Application. Nếu Application tham chiếu ngược lại thì thành vòng, .NET không cho. Application tự định nghĩa interface mình cần, Infrastructure implement nó, rồi DI ghép 2 bên lại lúc chạy.
</details>

<details><summary>2. Gửi JSON thiếu hẳn field <code>name</code>, rồi gửi <code>"name":""</code>. Hai câu lỗi khác nhau, vì sao?</summary>

Thiếu field thì bị lớp ① chặn. Với `[ApiController]`, MVC coi `string` không-nullable là bắt buộc, nên báo `The Name field is required.`. Có field nhưng rỗng thì qua được lớp ①, đến lượt FluentValidation (lớp ②) báo `'Name' must not be empty.`.

Cả hai đều cùng dạng ProblemDetails nên client không bị ảnh hưởng. Nếu muốn mọi lỗi kiểu này chỉ do FluentValidation báo, có tùy chọn `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` trong `AddControllers(...)`.
</details>

<details><summary>3. Vì sao <code>PUT</code> không cho đổi <code>PackageId</code>?</summary>

PackageId là danh tính của App. Ngày 6, mỗi file APK upload lên được so package với `App.PackageId`. Đổi PackageId thì mọi build cũ thành build của một app khác, và upload tiếp bản mới của app cũ sẽ bị chặn. Muốn đổi package thì tạo App mới.
</details>

<details><summary>4. Đã có <code>AnyAsync</code> kiểm tra trùng, sao còn cần unique index?</summary>

`AnyAsync` và `INSERT` là 2 bước riêng, giữa chúng có khoảng hở để request khác chen vào (Thử phá 2). Chỉ DB mới kiểm tra và ghi trong cùng một bước. `AnyAsync` để trả lời đẹp trong trường hợp thường gặp, unique index để dữ liệu không bao giờ sai.
</details>

<details><summary>5. Xóa dòng <code>ConfigureHttpJsonOptions</code> thì API còn trả <code>"Android"</code> không? Scalar hiển thị gì?</summary>

API vẫn trả `"Android"`, vì controller dùng cấu hình của `AddJsonOptions`. Nhưng tài liệu OpenAPI mô tả `AppPlatform` là `{"type":"integer"}` (đã chạy thử), nên Scalar gợi ý gửi số. Có cả dòng này thì tài liệu là `{"enum":["Android","iOS"]}`.
</details>

---

## Lỗi hay gặp

| Triệu chứng | Nguyên nhân | Sửa |
|---|---|---|
| `POST` trả `500` `No route matches the supplied values.`, nhưng App **vẫn được tạo** trong DB | Đặt tên action `GetByIdAsync` rồi dùng `nameof(GetByIdAsync)`: MVC tự cắt đuôi `Async` khỏi tên action nên không tìm thấy route | Đặt tên action không có `Async` (`GetById`) |
| `POST` trả `500` `An error occurred while saving the entity changes` | Dùng `DateTime.Now` (giờ địa phương). Npgsql chỉ nhận giờ UTC cho cột `timestamp with time zone` | Dùng `DateTime.UtcNow` |
| `dotnet ef` in `tools version '10.0.12' is older than that of the runtime '10.0.14'` | Tool `dotnet-ef` cũ hơn package | `dotnet tool update --global dotnet-ef` |
| App dừng ngay lúc khởi động với `Chưa cấu hình ConnectionStrings:Default.` | Không chạy ở Development nên không đọc `appsettings.Development.json` | Chạy `dotnet run` trong `src/Hangar.Api` (profile `http` đã đặt sẵn Development) |
| App lỗi lúc khởi động, không kết nối được cổng `5436` | Container Postgres chưa chạy | `docker compose up -d` ở thư mục gốc Hangar |
