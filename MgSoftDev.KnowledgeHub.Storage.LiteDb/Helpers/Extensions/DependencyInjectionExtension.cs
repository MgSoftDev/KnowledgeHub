using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace MgSoftDev.KnowledgeHub.Storage.LiteDb;

public static class DependencyInjectionExtension
{
    /// <summary>
    /// Registers the LiteDB storage provider. The LiteDatabase instance is a singleton
    /// (direct mode, single process); the store itself is scoped like the core services.
    /// </summary>
    public static IServiceCollection AddKnowledgeHubLiteDbStore(this IServiceCollection services,
        string databasePath, Action<LiteDbKnowledgeHubOptions>? configure = null)
    {
        var options = new LiteDbKnowledgeHubOptions { DatabasePath = databasePath };
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<LiteDbKnowledgeHubContext>();
        services.AddScoped<IKnowledgeHubStore, LiteDbKnowledgeHubStore>();
        return services;
    }
}
