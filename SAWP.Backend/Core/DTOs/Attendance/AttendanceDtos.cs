using Core.Enums;

namespace Core.DTOs;

public class DailyAttendanceEntryDto
{
    public int StudentId { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
}

public class LeaveRequestSubmitDto
{
    public int StudentId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? DoctorNoteAttachmentUrl { get; set; }
}
