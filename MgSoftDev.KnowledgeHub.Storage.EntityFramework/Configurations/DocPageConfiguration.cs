using MgSoftDev.KnowledgeHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MgSoftDev.KnowledgeHub.Storage.EntityFramework.Configurations;

internal sealed class DocPageConfiguration : IEntityTypeConfiguration<DocPage>
{
    private readonly KnowledgeHubEfModelOptions _options;

    public DocPageConfiguration(KnowledgeHubEfModelOptions options) => _options = options;

    public void Configure(EntityTypeBuilder<DocPage> builder)
    {
        var table = EfEntityBaseConfiguration.Apply(builder, _options, "DocPages");

        builder.Property(e => e.Slug).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();

        builder.HasIndex(e => e.Fk_DocPageParent).HasDatabaseName($"IX_{table}_Fk_DocPageParent");
        builder.HasIndex(e => e.Slug).IsUnique().HasDatabaseName($"UQ_{table}_Slug");

        // Self reference: parent page.
        builder.HasOne(e => e.Parent).WithMany(p => p.Children)
               .HasForeignKey(e => e.Fk_DocPageParent)
               .HasConstraintName($"FK_{table}_DocPageParent")
               .OnDelete(DeleteBehavior.NoAction);

        // Pointer to the currently published version (circular dependency; NoAction).
        builder.HasOne(e => e.PublishedVersion).WithMany()
               .HasForeignKey(e => e.Fk_DocPageVersionPublished)
               .HasConstraintName($"FK_{table}_DocPageVersionPublished")
               .OnDelete(DeleteBehavior.NoAction);
    }
}
