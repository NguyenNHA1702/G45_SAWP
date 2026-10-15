namespace Core.Entities;

public class LeaveRequest
{
    public int Id { get; set; }
    public int AdministrativeRequestId { get; set; }
    public virtual AdministrativeRequest AdministrativeRequest { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? DoctorNoteAttachmentUrl { get; set; }
}
