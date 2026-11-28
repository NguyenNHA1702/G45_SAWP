namespace Core.Entities;

/// <summary>
/// UC-54: Fine-grained capability permission definition for RBAC matrix configuration.
/// </summary>
public class Permission
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }

    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>
/// UC-54: Permission assignment per Role.
/// </summary>
public class RolePermission
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public virtual Role Role { get; set; } = null!;

    public int PermissionId { get; set; }
    public virtual Permission Permission { get; set; } = null!;

    public bool IsGranted { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// UC-56: Configurable reference list items (DocumentType, Relationship, Allergen, etc.).
/// </summary>
public class ReferenceItem
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// UC-57: Configurable notification text templates for system events.
/// </summary>
public class NotificationTemplate
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string TitleTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
