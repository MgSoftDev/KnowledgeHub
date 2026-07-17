using MgSoftDev.KnowledgeHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MgSoftDev.KnowledgeHub.Storage.EntityFramework.Configurations;

/// <summary>
/// Parts shared by every table: physical name (prefix + schema), the client-generated Pk and
/// the audit columns. No SQL defaults here — the core services always send explicit values,
/// so the model stays provider-neutral. The install script may still carry DB defaults as a
/// safety net for manual inserts.
/// </summary>
internal static class EfEntityBaseConfiguration
{
    /// <summary>Applies the common mapping and returns the physical (prefixed) table name.</summary>
    public static string Apply<T>(EntityTypeBuilder<T> builder, KnowledgeHubEfModelOptions options, string table)
        where T : EntityBase
    {
        var physical = options.TablePrefix + table;
        var schema = string.IsNullOrWhiteSpace(options.Schema) ? null : options.Schema;
        builder.ToTable(physical, schema);

        builder.HasKey(e => e.Pk).HasName($"PK_{physical}");
        builder.Property(e => e.Pk).ValueGeneratedNever();

        builder.Property(e => e.RowUserCreate).HasMaxLength(64);
        builder.Property(e => e.RowUserUpdate).HasMaxLength(64);

        return physical;
    }
}
