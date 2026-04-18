namespace CMS.Domain.Entities;

// A simple read model for support staff. Full user management lives in Keycloak —
// I only store the details the app needs to populate the assignment dropdown.
public sealed class SupportPerson
{
    public Guid Id { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}
