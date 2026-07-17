using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MgSoftDev.KnowledgeHub.Storage.EntityFramework;

public static class DependencyInjectionExtension
{
    /// <summary>
    /// Registers the EF-based store with a caller-supplied provider configuration. Engine
    /// packages (SqlServer, future PostgreSQL) call this; hosts normally use those instead.
    /// </summary>
    public static IServiceCollection AddKnowledgeHubEfStore(this IServiceCollection services,
        KnowledgeHubEfModelOptions modelOptions, Action<DbContextOptionsBuilder> configureProvider)
    {
        services.AddSingleton(modelOptions);
        services.AddDbContextFactory<KnowledgeHubDbContext>(configureProvider);
        services.AddScoped<IKnowledgeHubStore, EfKnowledgeHubStore>();
        return services;
    }
}
