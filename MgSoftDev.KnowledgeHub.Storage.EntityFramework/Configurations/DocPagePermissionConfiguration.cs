using MgSoftDev.KnowledgeHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MgSoftDev.KnowledgeHub.Storage.EntityFramework.Configurations;

internal sealed class DocPagePermissionConfiguration : IEntityTypeConfiguration<DocPagePermission>
{
    private readonly KnowledgeHubEfModelOptions _options;

    public DocPagePermissionConfiguration(KnowledgeHubEfModelOptions options) => _options = options;

    public void Configure(EntityTypeBuilder<DocPagePermission> builder)
    {
        var table = EfEntityBaseConfiguration.Apply(builder, _options, "DocPages_Permissions");
        var pages = _options.TablePrefix + "DocPages";

        builder.Property(e => e.Permission).HasMaxLength(128).IsRequired();

        builder.HasIndex(e => new { e.Fk_DocPage, e.Permission })
               .IsUnique()
               .HasDatabaseName($"UQ_{table}_Page_Permission");

        builder.HasOne(e => e.DocPage).WithMany(p => p.DocPagePermissions)
               .HasForeignKey(e => e.Fk_DocPage)
               .HasConstraintName($"FK_{table}_{pages}")
               .OnDelete(DeleteBehavior.NoAction);
    }
}
