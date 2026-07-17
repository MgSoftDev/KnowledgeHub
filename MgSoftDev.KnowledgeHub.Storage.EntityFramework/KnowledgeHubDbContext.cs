using MgSoftDev.KnowledgeHub.Entities;
using MgSoftDev.KnowledgeHub.Storage.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;

namespace MgSoftDev.KnowledgeHub.Storage.EntityFramework;

/// <summary>
/// EF Core context of the KnowledgeHub module. Provider-neutral: no engine-specific column
/// types or SQL defaults — every value (Pk, timestamps, audit) is generated client-side by
/// the core services. Schema/prefix come from <see cref="KnowledgeHubEfModelOptions"/>
/// (one configuration per process; the EF model is cached per context type).
/// </summary>
public class KnowledgeHubDbContext : DbContext
{
    private readonly KnowledgeHubEfModelOptions _modelOptions;

    public KnowledgeHubDbContext(DbContextOptions<KnowledgeHubDbContext> options,
        KnowledgeHubEfModelOptions modelOptions) : base(options)
    {
        _modelOptions = modelOptions;
    }

    public DbSet<DocPage> Pages => Set<DocPage>();
    public DbSet<DocPageVersion> Versions => Set<DocPageVersion>();
    public DbSet<DocImage> Images => Set<DocImage>();
    public DbSet<DocImageContent> ImageContents => Set<DocImageContent>();
    public DbSet<DocPageDocImage> PageImages => Set<DocPageDocImage>();
    public DbSet<DocPagePermission> PagePermissions => Set<DocPagePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DocPageConfiguration(_modelOptions));
        modelBuilder.ApplyConfiguration(new DocPageVersionConfiguration(_modelOptions));
        modelBuilder.ApplyConfiguration(new DocImageConfiguration(_modelOptions));
        modelBuilder.ApplyConfiguration(new DocImageContentConfiguration(_modelOptions));
        modelBuilder.ApplyConfiguration(new DocPageDocImageConfiguration(_modelOptions));
        modelBuilder.ApplyConfiguration(new DocPagePermissionConfiguration(_modelOptions));
        base.OnModelCreating(modelBuilder);
    }
}
