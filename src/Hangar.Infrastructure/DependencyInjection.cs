using Hangar.Application.AppTesters;
using Hangar.Application.Auth;
using Hangar.Application.Common.Interfaces;
using Hangar.Infrastructure.Email;
using Hangar.Infrastructure.Identity;
using Hangar.Infrastructure.Minio;
using Hangar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;

namespace Hangar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' chưa được cấu hình.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ICurrentOrganizationContext, CurrentOrganizationContext>();

        // AddIdentityCore — KHÔNG phải AddIdentity<TUser,TRole>() như CRM — không có
        // RoleManager, không có AddRoles<T>(). Khớp với AppDbContext : IdentityUserContext.
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAppTesterService, AppTesterService>();

        services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.Configure<MinioSettings>(configuration.GetSection("Minio"));
        services.AddSingleton<IMinioClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MinioSettings>>().Value;
            return new MinioClient()
                .WithEndpoint(settings.Endpoint)
                .WithCredentials(settings.AccessKey, settings.SecretKey)
                .Build();
        });

        services.AddScoped<IFileStorageService, MinioFileStorageService>();

        return services;
    }
}
