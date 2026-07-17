namespace MgSoftDev.KnowledgeHub.Entities;

/// <summary>
/// Base class holding the primary key and the audit columns every KnowledgeHub table carries.
/// Property names match the physical column names one-to-one. All values (Pk, timestamps and
/// the audit user) are generated client-side by the core services so every storage engine
/// behaves identically — no server-side defaults are required.
/// </summary>
public abstract class EntityBase
{
    /// <summary>Primary key. Generated client-side with <see cref="Guid.CreateVersion7()"/>.</summary>
    public Guid Pk { get; set; }

    /// <summary>Soft-delete flag. Active rows are true; deleted rows are false.</summary>
    public bool RowIsActive { get; set; } = true;

    /// <summary>Creation timestamp, assigned client-side by the core services.</summary>
    public DateTime RowCreateDate { get; set; }

    /// <summary>Last update timestamp, assigned client-side by the core services.</summary>
    public DateTime RowUpdateDate { get; set; }

    /// <summary>UserName (from the host user context) of the creator. Nullable.</summary>
    public string? RowUserCreate { get; set; }

    /// <summary>UserName (from the host user context) of the last editor. Nullable.</summary>
    public string? RowUserUpdate { get; set; }
}
