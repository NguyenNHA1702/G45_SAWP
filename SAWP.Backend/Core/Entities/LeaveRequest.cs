using Core.Enums;

namespace Core.Entities;

public class LeaveRequest
{
    public int Id { get; set; }
    public int AdministrativeRequestId { get; set; }
    public virtual AdministrativeRequest AdministrativeRequest { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public LeaveSession Session { get; set; } = LeaveSession.FullDay;
    public bool KeepLunchService { get; set; } = false;
    public bool IsLunchCancelled { get; set; } = false;
    public string Reason { get; set; } = string.Empty;
    public string? DoctorNoteAttachmentUrl { get; set; }
}
