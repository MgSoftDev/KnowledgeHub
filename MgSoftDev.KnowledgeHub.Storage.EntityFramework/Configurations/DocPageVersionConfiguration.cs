using MgSoftDev.KnowledgeHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MgSoftDev.KnowledgeHub.Storage.EntityFramework.Configurations;

internal sealed class DocPageVersionConfiguration : IEntityTypeConfiguration<DocPageVersion>
{
    private readonly KnowledgeHubEfModelOptions _options;

    public DocPageVersionConfiguration(KnowledgeHubEfModelOptions options) => _options = options;

    public void Configure(EntityTypeBuilder<DocPageVersion> builder)
    {
        var table = EfEntityBaseConfiguration.Apply(builder, _options, "DocPageVersions");
        var pages = _options.TablePrefix + "DocPages";

        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
        builder.Property(e => e.ContentHtml).IsRequired();
        builder.Property(e => e.ChangeNote).HasMaxLength(500);

        builder.HasIndex(e => e.Fk_DocPage).HasDatabaseName($"IX_{table}_Fk_DocPage");
        builder.HasIndex(e => new { e.Fk_DocPage, e.VersionNumber })
               .IsUnique()
               .HasDatabaseName($"UQ_{table}_Page_Version");

        builder.HasOne(e => e.DocPage).WithMany(p => p.Versions)
               .HasForeignKey(e => e.Fk_DocPage)
               .HasConstraintName($"FK_{table}_{pages}")
               .OnDelete(DeleteBehavior.NoAction);
    }
}
