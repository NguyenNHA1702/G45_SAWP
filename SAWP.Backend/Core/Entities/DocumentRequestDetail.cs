namespace Core.Entities;

public class DocumentRequestDetail
{
    public int Id { get; set; }
    public int AdministrativeRequestId { get; set; }
    public virtual AdministrativeRequest AdministrativeRequest { get; set; } = null!;

    public string DocumentType { get; set; } = string.Empty;
    public int NumberOfCopies { get; set; } = 1;
    public string Purpose { get; set; } = string.Empty;
    public DateTime? IssuedDate { get; set; }

    // Workflow draft, endorsement & issuance
    public string? StaffEndorsement { get; set; }
    public string? DocumentContentDraft { get; set; }
    public string? IssuedDocumentUrl { get; set; }
    public string? CertificateNumber { get; set; }
    public string? DigitalSignatureInfo { get; set; }
}
