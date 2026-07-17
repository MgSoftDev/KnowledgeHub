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

    /// <summary>The store-level visibility filter for this user.</summary>
    public static Store.VisibilityFilter ToVisibilityFilter(this IKnowledgeHubUserContext user) =>
        user.IsAdmin() ? Store.VisibilityFilter.Admin : new(false, user.Permissions);
}
