using System.Text;
using System.Text.Json.Serialization;
using Hangar.Application;
using Hangar.Application.Auth;
using Hangar.Domain.Entities;
using Hangar.Infrastructure;
using Hangar.Infrastructure.Authorization;
using Hangar.Infrastructure.Minio;
using Hangar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Minio;
using Minio.DataModel.Args;
using Scalar.AspNetCore;
using Hangar.Api.Uploads;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// JsonStringEnumConverter — response đã trả enum dạng chuỗi (Platform.ToString(), v.v.), nếu
// không thêm converter này thì request lại phải gửi số (0/1), không khớp — rất dễ gây lỗi khi
// dùng API.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<UploadSettings>(builder.Configuration.GetSection("Upload"));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// OrganizationMemberAuthorizationHandler đọc {organizationId} từ route qua IHttpContextAccessor
// (xem ghi chú trong handler — context.Resource không đáng tin cậy giữa middleware auth và
// MVC filter auth, HttpContextAccessor là cách chắc chắn hơn).
builder.Services.AddHttpContextAccessor();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["SigningKey"]!));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Không cần MapInboundClaims = true như CRM — CRM cần nó để [Authorize(Roles=...)]/
        // User.IsInRole() khớp đúng tên claim role. Hangar không dùng claim role ở JWT nữa
        // (role phụ thuộc Organization, tra ở tầng authorization riêng — xem Ngày 4).
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true
        };
    });

// Chỉ 2 giá trị MinimumRole khả dĩ (Owner/Developer) nên đăng ký 2 policy tĩnh là đủ — khác
// CRM phải viết IAuthorizationPolicyProvider động vì permission có hàng chục tên khác nhau.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OrgDeveloper", policy =>
        policy.Requirements.Add(new OrganizationMemberRequirement(OrganizationRole.Developer)));

    options.AddPolicy("OrgOwner", policy =>
        policy.Requirements.Add(new OrganizationMemberRequirement(OrganizationRole.Owner)));

    // AppAccess: OrganizationMember HOẶC AppTester của đúng App đang truy cập (xem
    // AppAccessAuthorizationHandler) — khác 2 policy trên, policy này không gắn 1 MinimumRole cố
    // định vì "đủ điều kiện" ở đây được định nghĩa theo 2 nhánh khác nhau, không phải so sánh role.
    options.AddPolicy("AppAccess", policy =>
        policy.Requirements.Add(new AppAccessRequirement()));
});

builder.Services.AddScoped<IAuthorizationHandler, OrganizationMemberAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AppAccessAuthorizationHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var minio = scope.ServiceProvider.GetRequiredService<IMinioClient>();
    var minioSettings = scope.ServiceProvider.GetRequiredService<IOptions<MinioSettings>>().Value;
    var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(minioSettings.BucketName));
    if (!exists)
    {
        await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(minioSettings.BucketName));
    }
}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
