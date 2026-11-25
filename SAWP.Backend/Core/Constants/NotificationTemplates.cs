namespace Core.Constants;

public static class NotificationTemplates
{
    public const string AttendanceAbsentType  = "ATTENDANCE_ABSENT";
    public const string AttendanceAbsentTitle = "Thông báo vắng học";
    // {0}=Tên HS, {1}=Lớp, {2}=dd/MM/yyyy
    public const string AttendanceAbsentContent = "Học sinh {0} (lớp {1}) vắng mặt không phép ngày {2}. Vui lòng liên hệ GVCN nếu có thắc mắc.";
    public static string AbsentKey(int studentId, DateTime date) => $"ATT_ABSENT:{studentId}:{date:yyyyMMdd}";
    // OVR-01: one absence notice per attendance record (a day can have a morning and an afternoon record)
    public static string AbsenceNoticeKey(int attendanceRecordId) => $"ATT_ABSENCE_NOTICE:{attendanceRecordId}";

    public const string AttendanceAbsentRetractType = "ATTENDANCE_ABSENT_RETRACT";
    public const string AttendanceAbsentRetractTitle = "Đính chính thông báo vắng học";
    // {0}=Tên HS, {1}=Lớp, {2}=dd/MM/yyyy, {3}=Trạng thái mới
    public const string AttendanceAbsentRetractContent = "Thông báo vắng mặt của học sinh {0} (lớp {1}) ngày {2} đã được đính chính thành: {3}.";
    public static string AbsenceNoticeRetractKey(int attendanceRecordId, DateTime utcNow) => $"ATT_ABSENCE_RETRACT:{attendanceRecordId}:{utcNow:yyyyMMddHHmmssfff}";
}
