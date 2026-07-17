using LiteDB;
using MgSoftDev.KnowledgeHub.Entities;

namespace MgSoftDev.KnowledgeHub.Storage.LiteDb;

/// <summary>
/// Singleton owner of the LiteDatabase instance (direct mode, one process), the collection
/// handles and the process-wide write lock. Entities are mapped with a DEDICATED BsonMapper
/// (client-generated Guid Pk, navigations ignored) so the host's global mapper is untouched.
/// </summary>
public sealed class LiteDbKnowledgeHubContext : IDisposable
{
    public LiteDatabase Database { get; }

    /// <summary>
    /// Serializes composed write operations (insert version + links, publish, permissions).
    /// LiteDB transactions are per-thread and must not cross awaits, so writes run
    /// synchronously while holding this lock.
    /// </summary>
    public object WriteLock { get; } = new();

    public ILiteCollection<DocPage> Pages { get; }
    public ILiteCollection<DocPageVersion> Versions { get; }
    public ILiteCollection<DocImage> Images { get; }
    public ILiteCollection<DocImageContent> ImageContents { get; }
    public ILiteCollection<DocPageDocImage> PageImages { get; }
    public ILiteCollection<DocPagePermission> PagePermissions { get; }

    public LiteDbKnowledgeHubContext(LiteDbKnowledgeHubOptions options)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(options.DatabasePath));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        var mapper = BuildMapper();
        Database = new LiteDatabase(new ConnectionString
        {
            Filename = options.DatabasePath,
            Connection = ConnectionType.Direct
        }, mapper);

        var prefix = options.CollectionPrefix;
        Pages = Database.GetCollection<DocPage>($"{prefix}DocPages");
        Versions = Database.GetCollection<DocPageVersion>($"{prefix}DocPageVersions");
        Images = Database.GetCollection<DocImage>($"{prefix}DocImages");
        ImageContents = Database.GetCollection<DocImageContent>($"{prefix}DocImageContents");
        PageImages = Database.GetCollection<DocPageDocImage>($"{prefix}DocPages_DocImages");
        PagePermissions = Database.GetCollection<DocPagePermission>($"{prefix}DocPages_Permissions");

        EnsureIndexes();
    }

    private static BsonMapper BuildMapper()
    {
        var mapper = new BsonMapper { EnumAsInteger = true };

        mapper.Entity<DocPage>()
            .Id(p => p.Pk, autoId: false)
            .Ignore(p => p.Parent)
            .Ignore(p => p.Children)
            .Ignore(p => p.PublishedVersion)
            .Ignore(p => p.Versions)
            .Ignore(p => p.DocPagePermissions)
            .Ignore(p => p.DocPageDocImages);

        mapper.Entity<DocPageVersion>()
            .Id(v => v.Pk, autoId: false)
            .Ignore(v => v.DocPage);

        mapper.Entity<DocImage>()
            .Id(i => i.Pk, autoId: false)
            .Ignore(i => i.Content)
            .Ignore(i => i.DocPageDocImages);

        mapper.Entity<DocImageContent>()
            .Id(c => c.Pk, autoId: false)
            .Ignore(c => c.DocImage);

        mapper.Entity<DocPageDocImage>()
            .Id(l => l.Pk, autoId: false)
            .Ignore(l => l.DocPage)
            .Ignore(l => l.DocImage);

        mapper.Entity<DocPagePermission>()
            .Id(p => p.Pk, autoId: false)
            .Ignore(p => p.DocPage);

        return mapper;
    }

    private void EnsureIndexes()
    {
        Pages.EnsureIndex(p => p.Slug, unique: true);
        Pages.EnsureIndex(p => p.Fk_DocPageParent);
        Versions.EnsureIndex(v => v.Fk_DocPage);
        Images.EnsureIndex(i => i.ContentHash, unique: true);
        ImageContents.EnsureIndex(c => c.Fk_DocImage);
        PageImages.EnsureIndex(l => l.Fk_DocPage);
        PagePermissions.EnsureIndex(p => p.Fk_DocPage);
    }

    public void Dispose() => Database.Dispose();
}
