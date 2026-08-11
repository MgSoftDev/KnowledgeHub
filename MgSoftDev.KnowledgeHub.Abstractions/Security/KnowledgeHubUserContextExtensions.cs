using MgSoftDev.KnowledgeHub.Contracts;

namespace MgSoftDev.KnowledgeHub.Security;

/// <summary>Capability checks derived from the reserved KnowledgeHub.* permissions.</summary>
public static class KnowledgeHubUserContextExtensions
{
    public static bool HasPermission(this IKnowledgeHubUserContext user, string permission) =>
        user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    /// <summary>Admin sees everything and can do everything.</summary>
    public static bool IsAdmin(this IKnowledgeHubUserContext user) =>
        user.HasPermission(KnowledgeHubPermissions.Admin);

    /// <summary>Admin or Edit: create/edit/save drafts (and, in coarse mode, publish/manage).</summary>
    public static bool CanEdit(this IKnowledgeHubUserContext user) =>
        user.IsAdmin() || user.HasPermission(KnowledgeHubPermissions.Edit);

    /// <summary>
    /// Coarse mode (default): anyone who can edit can publish. Fine-grained mode
    /// (options.UseFineGrainedPublish): requires Admin or the Publish permission.
    /// </summary>
    public static bool CanPublish(this IKnowledgeHubUserContext user, KnowledgeHubOptions options) =>
        options.UseFineGrainedPublish
            ? user.IsAdmin() || user.HasPermission(KnowledgeHubPermissions.Publish)
            : user.CanEdit();

    /// <summary>
    /// Coarse mode (default): anyone who can edit can manage visibility. Fine-grained mode
    /// (options.UseFineGrainedManagePermissions): requires Admin or the ManagePermissions permission.
    /// </summary>
    public static bool CanManagePermissions(this IKnowledgeHubUserContext user, KnowledgeHubOptions options) =>
        options.UseFineGrainedManagePermissions
            ? user.IsAdmin() || user.HasPermission(KnowledgeHubPermissions.ManagePermissions)
            : user.CanEdit();

    /// <summary>
    /// Coarse mode (default): anyone signed in can export what they are allowed to read.
    /// Fine-grained mode (options.UseFineGrainedExport): requires Admin or the Export permission.
    ///
    /// Note the asymmetry with <see cref="CanPublish"/> and <see cref="CanManagePermissions"/>,
    /// which fall back to <see cref="CanEdit"/>: exporting is NOT an editing capability. Reading a
    /// page and taking it away as a PDF are the same act, so a plain reader must be able to do it
    /// unless the host deliberately restricts it. Which page ends up in the file is decided by
    /// visibility, page by page — never by this check.
    /// </summary>
    public static bool CanExport(this IKnowledgeHubUserContext user, KnowledgeHubOptions options) =>
        options.UseFineGrainedExport
            ? user.IsAdmin() || user.HasPermission(KnowledgeHubPermissions.Export)
            : user.IsAuthenticated;

    /// <summary>The store-level visibility filter for this user.</summary>
    public static Store.VisibilityFilter ToVisibilityFilter(this IKnowledgeHubUserContext user) =>
        user.IsAdmin() ? Store.VisibilityFilter.Admin : new(false, user.Permissions);
}
