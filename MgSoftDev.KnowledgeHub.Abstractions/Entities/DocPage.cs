namespace MgSoftDev.KnowledgeHub.Entities;

/// <summary>
/// A documentation page: a node in the hierarchical tree plus a pointer to the version
/// that is currently published (visible to readers).
/// </summary>
public class DocPage : EntityBase
{
    /// <summary>Parent page (self reference). NULL for a root page.</summary>
    public Guid? Fk_DocPageParent { get; set; }

    /// <summary>The version currently published. NULL while the page has never been published.</summary>
    public Guid? Fk_DocPageVersionPublished { get; set; }

    public string Slug { get; set; } = null!;
    public string Title { get; set; } = null!;
    public int SortOrder { get; set; }

    /// <summary>Optional Material Symbols icon name shown next to the title (e.g. "menu_book").</summary>
    public string? Icon { get; set; }

    /// <summary>Optional CSS color for the icon (e.g. "#f59e0b"). Null inherits the theme color.</summary>
    public string? IconColor { get; set; }

    /// <summary>When true, the page is visible to everyone and DocPages_Permissions is ignored.</summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// When true, PDF exports skip this page. Structural like <see cref="Icon"/> — it belongs to the
    /// node, not to a version, so marking a page does not create a new version of it. Stated in the
    /// negative so the default (false) keeps every page exporting, as it always did.
    /// </summary>
    public bool ExcludeFromPdf { get; set; }

    /// <summary>
    /// When true, the page's <c>{{ … }}</c> placeholders are filled with live data every time it is
    /// displayed. Off by default, and deliberately opt-in per page: braces are ordinary content in
    /// any page documenting Angular, Vue or Handlebars, and processing those would silently blank
    /// the examples. Setting it requires
    /// <see cref="Security.KnowledgeHubPermissions.Templates"/>.
    /// </summary>
    public bool UsesTemplates { get; set; }

    public DocPage? Parent { get; set; }
    public ICollection<DocPage> Children { get; set; } = new List<DocPage>();

    /// <summary>The published version pointed to by <see cref="Fk_DocPageVersionPublished"/>.</summary>
    public DocPageVersion? PublishedVersion { get; set; }

    /// <summary>Full version history of this page.</summary>
    public ICollection<DocPageVersion> Versions { get; set; } = new List<DocPageVersion>();

    public ICollection<DocPagePermission> DocPagePermissions { get; set; } = new List<DocPagePermission>();
    public ICollection<DocPageDocImage> DocPageDocImages { get; set; } = new List<DocPageDocImage>();
}
