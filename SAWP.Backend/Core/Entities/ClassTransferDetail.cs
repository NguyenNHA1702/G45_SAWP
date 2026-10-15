namespace Core.Entities;

public class ClassTransferDetail
{
    public int Id { get; set; }
    public int AdministrativeRequestId { get; set; }
    public virtual AdministrativeRequest AdministrativeRequest { get; set; } = null!;

    public int FromClassId { get; set; }
    public virtual Class FromClass { get; set; } = null!;

    public int ToClassId { get; set; }
    public virtual Class ToClass { get; set; } = null!;

    public string TransferReason { get; set; } = string.Empty;
}
