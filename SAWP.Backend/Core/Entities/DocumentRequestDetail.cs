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
}
