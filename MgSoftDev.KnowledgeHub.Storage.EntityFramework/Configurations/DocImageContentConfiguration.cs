using MgSoftDev.KnowledgeHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MgSoftDev.KnowledgeHub.Storage.EntityFramework.Configurations;

internal sealed class DocImageContentConfiguration : IEntityTypeConfiguration<DocImageContent>
{
    private readonly KnowledgeHubEfModelOptions _options;

    public DocImageContentConfiguration(KnowledgeHubEfModelOptions options) => _options = options;

    public void Configure(EntityTypeBuilder<DocImageContent> builder)
    {
        var table = EfEntityBaseConfiguration.Apply(builder, _options, "DocImageContents");

        builder.Property(e => e.Content).IsRequired();

        builder.HasIndex(e => e.Fk_DocImage)
               .IsUnique()
               .HasDatabaseName($"UQ_{table}_Fk_DocImage");
    }
}
