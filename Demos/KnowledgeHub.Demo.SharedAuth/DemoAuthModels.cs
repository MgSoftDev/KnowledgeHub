namespace KnowledgeHub.Demo.SharedAuth;

/// <summary>
/// Demo HOST user. This lives in the host application (not in the KnowledgeHub library) to
/// simulate a real system that already has its own users/roles. Roles are embedded as a pk
/// list — idiomatic LiteDB for host-side data.
/// </summary>
public class DemoUser
{
    public Guid Pk { get; set; }
    public string UserName { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public List<Guid> RoleIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

/// <summary>Demo HOST role.</summary>
public class DemoRole
{
    public Guid Pk { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>The signed-in host user, produced by the login flow.</summary>
public sealed record DemoUserDto(Guid Pk, string UserName, string FullName, IReadOnlyList<string> Roles);

public sealed class DemoRoleDto
{
    public Guid Pk { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

/// <summary>A user row for the demo administration grid.</summary>
public sealed class DemoUserAdminDto
{
    public Guid Pk { get; set; }
    public string UserName { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public bool IsActive { get; set; }
    public List<Guid> RoleIds { get; set; } = new();
    public string RolesText { get; set; } = string.Empty;
}
