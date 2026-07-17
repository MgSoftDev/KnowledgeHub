using MgSoftDev.KnowledgeHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MgSoftDev.KnowledgeHub.Storage.EntityFramework.Configurations;

internal sealed class DocImageConfiguration : IEntityTypeConfiguration<DocImage>
{
    private readonly KnowledgeHubEfModelOptions _options;

    public DocImageConfiguration(KnowledgeHubEfModelOptions options) => _options = options;

    public void Configure(EntityTypeBuilder<DocImage> builder)
    {
        var table = EfEntityBaseConfiguration.Apply(builder, _options, "DocImages");
        var contents = _options.TablePrefix + "DocImageContents";

        builder.Property(e => e.FileName).HasMaxLength(300).IsRequired();
        builder.Property(e => e.ContentHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(100).IsRequired();

        builder.HasIndex(e => e.ContentHash).HasDatabaseName($"IX_{table}_ContentHash");

        // 1:1 with DocImageContents (the binary lives there).
        builder.HasOne(e => e.Content).WithOne(c => c.DocImage)
               .HasForeignKey<DocImageContent>(c => c.Fk_DocImage)
               .HasConstraintName($"FK_{contents}_{table}")
               .OnDelete(DeleteBehavior.NoAction);
    }
}
