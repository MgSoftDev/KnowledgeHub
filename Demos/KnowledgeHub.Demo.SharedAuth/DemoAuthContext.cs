using LiteDB;

namespace KnowledgeHub.Demo.SharedAuth;

/// <summary>
/// LiteDB database of the demo HOST (users/roles). Kept in its OWN file, separate from the
/// KnowledgeHub module database — two Direct-mode LiteDatabase instances cannot share a file.
/// </summary>
public sealed class DemoAuthContext : IDisposable
{
    public LiteDatabase Database { get; }
    public ILiteCollection<DemoUser> Users { get; }
    public ILiteCollection<DemoRole> Roles { get; }

    public DemoAuthContext(string databasePath)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        var mapper = new BsonMapper();
        mapper.Entity<DemoUser>().Id(u => u.Pk, autoId: false);
        mapper.Entity<DemoRole>().Id(r => r.Pk, autoId: false);

        Database = new LiteDatabase(new ConnectionString
        {
            Filename = databasePath,
            Connection = ConnectionType.Direct
        }, mapper);

        Users = Database.GetCollection<DemoUser>("app_Users");
        Roles = Database.GetCollection<DemoRole>("app_Roles");
        Users.EnsureIndex(u => u.UserName, unique: true);
    }

    public void Dispose() => Database.Dispose();
}
