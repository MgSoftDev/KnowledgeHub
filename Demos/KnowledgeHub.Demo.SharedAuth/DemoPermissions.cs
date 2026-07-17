using MgSoftDev.KnowledgeHub.Security;

namespace KnowledgeHub.Demo.SharedAuth;

/// <summary>
/// THE integration point between the demo host's role model and KnowledgeHub's permission
/// strings: every role is exposed as "Role.{Name}" (used for page visibility), and the
/// well-known KnowledgeHub.* capabilities are granted from the Admin/Editor roles.
/// </summary>
public static class DemoPermissions
{
    public const string RolePrefix = "Role.";

    public const string AdminRole = "Admin";
    public const string EditorRole = "Editor";

    /// <summary>Effective KnowledgeHub permissions for a set of host roles.</summary>
    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roleNames)
    {
        var permissions = new List<string>();
        foreach (var role in roleNames)
        {
            permissions.Add(RolePrefix + role);

            if (string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase))
                permissions.Add(KnowledgeHubPermissions.Admin);
            if (string.Equals(role, EditorRole, StringComparison.OrdinalIgnoreCase))
                permissions.Add(KnowledgeHubPermissions.Edit);
        }
        return permissions;
    }

    /// <summary>Catalog entry (visibility picker) for one host role.</summary>
    public static PermissionInfo ToCatalogEntry(DemoRoleDto role) =>
        new(RolePrefix + role.Name, role.Name);
}
