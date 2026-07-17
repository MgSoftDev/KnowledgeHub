using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// The bridge between the host application's identity system and KnowledgeHub. The HOST
/// implements and registers this contract with whatever lifetime fits its hosting model
/// (singleton in WPF, scoped-per-circuit in Blazor Server, scoped-per-request in an API).
/// KnowledgeHub has no user tables of its own.
/// </summary>
public interface IKnowledgeHubUserContext
{
    bool IsAuthenticated { get; }

    /// <summary>
    /// Stable user key. Stored in RowUserCreate/RowUserUpdate and shown as the author of a
    /// version.
    /// </summary>
    string UserName { get; }

    /// <summary>Friendly name shown in the UI header.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Effective permissions of the signed-in user, including the reserved KnowledgeHub.*
    /// names (see <see cref="KnowledgeHubPermissions"/>) when granted. Page visibility is
    /// resolved against this list (case-insensitive).
    /// </summary>
    IReadOnlyList<string> Permissions { get; }

    /// <summary>
    /// The host's permission catalog, used by the per-page visibility picker. Typically the
    /// host's roles or permission definitions projected to (Name, DisplayName).
    /// </summary>
    Task<ReturningList<PermissionInfo>> GetPermissionCatalogAsync();
}
