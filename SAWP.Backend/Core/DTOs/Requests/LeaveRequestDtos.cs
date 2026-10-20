using Core.Enums;

namespace Core.DTOs;

public class CreateLeaveRequestDto
{
    public int StudentId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public LeaveSession Session { get; set; } = LeaveSession.FullDay;
    public bool KeepLunchService { get; set; } = false;
    public string Reason { get; set; } = string.Empty;
    public string? DoctorNoteAttachmentUrl { get; set; }
}

public class LeaveRequestItemDto
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;

    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public int? ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;

    public int RequesterUserId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string? RequesterPhone { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public LeaveSession Session { get; set; }
    public string SessionName => Session switch
    {
        LeaveSession.Morning => "Buổi sáng",
        LeaveSession.Afternoon => "Buổi chiều",
        LeaveSession.FullDay => "Cả ngày",
        _ => "Cả ngày"
    };

    public bool KeepLunchService { get; set; }
    public bool IsLunchCancelled { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string? DoctorNoteAttachmentUrl { get; set; }

    public int TotalDays => (EndDate.Date - StartDate.Date).Days + 1;
    public bool IsEscalated => TotalDays >= 3;

    public RequestStatus Status { get; set; }
    public string StatusName => Status switch
    {
        RequestStatus.Submitted => IsEscalated ? "Chờ GV chuyển BGH" : "Chờ GVCN duyệt",
        RequestStatus.UnderReview => "Chờ BGH duyệt",
        RequestStatus.Approved => "Đã duyệt",
        RequestStatus.Rejected => "Từ chối",
        RequestStatus.Cancelled => "Đã hủy",
        _ => Status.ToString()
    };

    public DateTime SubmissionDate { get; set; }
    public int? ReviewerUserId { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public string? ReviewerNotes { get; set; }
}

public class ReviewLeaveRequestDto
{
    public bool IsApproved { get; set; }
    public bool IsForwardToSchoolBoard { get; set; } = false;
    public string? ReviewerNotes { get; set; }
}

public class SyncLeaveResultDto
{
    public int RequestId { get; set; }
    public RequestStatus Status { get; set; }
    public bool IsAttendanceSyncSuggested { get; set; }
    public List<DateTime> CancelledMealDates { get; set; } = new();
    public string MealSyncMessage { get; set; } = string.Empty;
}
