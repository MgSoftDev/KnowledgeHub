using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeHub.Demo.SharedAuth;

public static class DependencyInjectionExtension
{
    /// <summary>Registers the demo host's own auth stack (users/roles in a dedicated LiteDB file).</summary>
    public static IServiceCollection AddDemoAuth(this IServiceCollection services, string databasePath)
    {
        services.AddSingleton(new DemoAuthContext(databasePath));
        services.AddSingleton<IPasswordHasher<DemoUser>, PasswordHasher<DemoUser>>();
        services.AddSingleton<DemoAuthService>();
        services.AddSingleton<DemoAdminService>();
        services.AddSingleton<DemoUserSeeder>();
        return services;
    }
}
