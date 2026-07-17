namespace MgSoftDev.KnowledgeHub.Entities;

/// <summary>
/// Grants view access on a page to one host permission (table DocPages_Permissions).
/// Ignored when the page has IsPublic = true. The permission is an opaque string from the
/// host's permission catalog; comparisons are case-insensitive.
/// </summary>
public class DocPagePermission : EntityBase
{
    public Guid Fk_DocPage { get; set; }

    /// <summary>Host permission name whose holders may view the page.</summary>
    public string Permission { get; set; } = null!;

    public DocPage DocPage { get; set; } = null!;
}
