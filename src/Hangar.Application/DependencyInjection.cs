using FluentValidation;
using Hangar.Application.Apps;
using Hangar.Application.Auth;
using Hangar.Application.Organizations;
using Microsoft.Extensions.DependencyInjection;

namespace Hangar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IAppService, AppService>();

        // Quét toàn bộ assembly Application, tự đăng ký mọi class kế thừa AbstractValidator<T>
        // (dùng IIdentityService chỉ để trỏ đúng assembly cần quét, không liên quan tới validator).
        services.AddValidatorsFromAssemblyContaining<IIdentityService>();

        return services;
    }
}
