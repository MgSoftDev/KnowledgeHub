using System.Reflection;
using MgSoftDev.KnowledgeHub.Storage.EntityFramework;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using Microsoft.Data.SqlClient;

namespace MgSoftDev.KnowledgeHub.Storage.SqlServer;

/// <summary>
/// Installation of the KnowledgeHub tables inside a host SQL Server database. The DDL is an
/// idempotent embedded script parameterized by schema/prefix — run it from your own migration
/// pipeline (GetCreateScript) or let the app apply it at startup (EnsureDatabaseObjectsAsync).
/// The database itself must already exist.
/// </summary>
public static class KnowledgeHubSqlSchema
{
    private const string ResourceName = "MgSoftDev.KnowledgeHub.Storage.SqlServer.Sql.CreateSchema.sql";

    /// <summary>The idempotent DDL script with schema/prefix applied.</summary>
    public static Returning<string> GetCreateScript(KnowledgeHubEfModelOptions? options = null) =>
        Returning<string>.Try(() =>
        {
            var opts = options ?? new KnowledgeHubEfModelOptions();
            var schema = string.IsNullOrWhiteSpace(opts.Schema) ? "dbo" : opts.Schema!;

            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found");
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd()
                .Replace("{{SCHEMA}}", schema)
                .Replace("{{PREFIX}}", opts.TablePrefix);
        }).SaveLog();

    /// <summary>Executes the DDL script against the given database (which must exist).</summary>
    public static Task<Returning> EnsureDatabaseObjectsAsync(string connectionString,
        KnowledgeHubEfModelOptions? options = null) =>
        Returning.TryTask(async () =>
        {
            var scriptR = GetCreateScript(options);
            if (!scriptR.Ok) scriptR.Throw();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = scriptR.Value!;
            await command.ExecuteNonQueryAsync();

            return Returning.Success();
        }, saveLog: true);
}
