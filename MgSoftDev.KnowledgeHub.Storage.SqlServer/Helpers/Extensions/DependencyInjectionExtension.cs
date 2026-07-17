using MgSoftDev.KnowledgeHub.Storage.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MgSoftDev.KnowledgeHub.Storage.SqlServer;

public static class DependencyInjectionExtension
{
    /// <summary>
    /// Registers the SQL Server storage provider. Tables live in the schema/prefix configured
    /// via <paramref name="configure"/> (default schema "kh"). Install the tables with
    /// <see cref="KnowledgeHubSqlSchema.EnsureDatabaseObjectsAsync"/> or by running
    /// <see cref="KnowledgeHubSqlSchema.GetCreateScript"/> in your own migration pipeline.
    /// </summary>
    public static IServiceCollection AddKnowledgeHubSqlServerStore(this IServiceCollection services,
        string connectionString, Action<KnowledgeHubEfModelOptions>? configure = null)
    {
        var modelOptions = new KnowledgeHubEfModelOptions();
        configure?.Invoke(modelOptions);

        return services.AddKnowledgeHubEfStore(modelOptions, o => o.UseSqlServer(connectionString));
    }
}
