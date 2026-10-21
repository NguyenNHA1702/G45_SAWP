namespace Core.Constants;

public static class NotificationTemplates
{
    public const string AttendanceAbsentType  = "ATTENDANCE_ABSENT";
    public const string AttendanceAbsentTitle = "Thông báo vắng học";
    // {0}=Tên HS, {1}=Lớp, {2}=dd/MM/yyyy
    public const string AttendanceAbsentContent = "Học sinh {0} (lớp {1}) vắng mặt không phép ngày {2}. Vui lòng liên hệ GVCN nếu có thắc mắc.";
    public static string AbsentKey(int studentId, DateTime date) => $"ATT_ABSENT:{studentId}:{date:yyyyMMdd}";
}
