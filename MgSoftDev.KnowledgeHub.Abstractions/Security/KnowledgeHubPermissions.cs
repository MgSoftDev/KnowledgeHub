namespace MgSoftDev.KnowledgeHub.Security;

/// <summary>
/// Reserved permission names KnowledgeHub understands. The host includes these in the
/// user's permission list (IKnowledgeHubUserContext.Permissions) when it wants to grant
/// the corresponding capability. Comparison is case-insensitive.
/// </summary>
public static class KnowledgeHubPermissions
{
    /// <summary>Sees every page and can do everything.</summary>
    public const string Admin = "KnowledgeHub.Admin";

    /// <summary>Create/edit/save drafts. In the default (coarse) mode it also publishes and manages.</summary>
    public const string Edit = "KnowledgeHub.Edit";

    /// <summary>Publish versions. Only enforced when KnowledgeHubOptions.UseFineGrainedPublish is true.</summary>
    public const string Publish = "KnowledgeHub.Publish";

    /// <summary>Manage page visibility. Only enforced when KnowledgeHubOptions.UseFineGrainedManagePermissions is true.</summary>
    public const string ManagePermissions = "KnowledgeHub.ManagePermissions";

    /// <summary>
    /// Download pages as a file. Only enforced when KnowledgeHubOptions.UseFineGrainedExport is
    /// true; by default anyone signed in can export what they can read. It never widens what a
    /// user sees: the export still goes page by page through the visibility filter.
    /// </summary>
    public const string Export = "KnowledgeHub.Export";
}
