using Core.Enums;

namespace Core.Entities;

public class GuardianLinkDetail
{
    public int Id { get; set; }
    public int AdministrativeRequestId { get; set; }
    public virtual AdministrativeRequest AdministrativeRequest { get; set; } = null!;

    public string StudentCode { get; set; } = string.Empty;
    public DateTime? StudentDateOfBirth { get; set; }
    public RelationshipTypes Relationship { get; set; } = RelationshipTypes.Guardian;
    public string? GuardianPhone { get; set; }
    public string? GuardianIdentityCard { get; set; }
    public string? ProofDocumentUrl { get; set; }
    public bool AutoVerificationPassed { get; set; } = false;
    public string? VerificationDetails { get; set; }
}
