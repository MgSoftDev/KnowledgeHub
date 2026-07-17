using MgSoftDev.KnowledgeHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MgSoftDev.KnowledgeHub.Storage.EntityFramework.Configurations;

internal sealed class DocPageDocImageConfiguration : IEntityTypeConfiguration<DocPageDocImage>
{
    private readonly KnowledgeHubEfModelOptions _options;

    public DocPageDocImageConfiguration(KnowledgeHubEfModelOptions options) => _options = options;

    public void Configure(EntityTypeBuilder<DocPageDocImage> builder)
    {
        var table = EfEntityBaseConfiguration.Apply(builder, _options, "DocPages_DocImages");
        var pages = _options.TablePrefix + "DocPages";
        var images = _options.TablePrefix + "DocImages";

        builder.HasIndex(e => new { e.Fk_DocPage, e.Fk_DocImage })
               .IsUnique()
               .HasDatabaseName($"UQ_{table}_Page_Image");

        builder.HasOne(e => e.DocPage).WithMany(p => p.DocPageDocImages)
               .HasForeignKey(e => e.Fk_DocPage)
               .HasConstraintName($"FK_{table}_{pages}")
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.DocImage).WithMany(i => i.DocPageDocImages)
               .HasForeignKey(e => e.Fk_DocImage)
               .HasConstraintName($"FK_{table}_{images}")
               .OnDelete(DeleteBehavior.NoAction);
    }
}
