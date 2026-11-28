namespace Core.Configuration;

public class SchoolOptions
{
    public const string SectionName = "School";
    public string TimeZoneId { get; set; } = "SE Asia Standard Time";
    // Giờ chốt suất ăn bán trú trong ngày (giờ trường). Sau mốc này không tự hủy / khôi phục suất ăn.
    public string LunchCutoffTime { get; set; } = "08:15";

    /// <summary>Giờ chốt thu hồi đơn xin nghỉ buổi Chiều trong ngày (giờ trường).</summary>
    public string AfternoonCutoffTime { get; set; } = "13:30";

    /// <summary>Số ngày học tối đa GVCN được duyệt trực tiếp; vượt quá (&gt;) ngưỡng này phải chuyển BGH.</summary>
    public int LeaveEscalationThresholdDays { get; set; } = 3;

    /// <summary>Giới hạn tối đa số ngày học trong một đơn xin nghỉ.</summary>
    public int MaxLeaveSchoolDays { get; set; } = 30;

    /// <summary>Giới hạn tối đa khoảng ngày lịch từ StartDate đến EndDate trong một đơn xin nghỉ.</summary>
    public int MaxLeaveCalendarSpanDays { get; set; } = 60;

    /// <summary>Ngày bắt đầu kỳ đăng ký bán trú định kỳ hàng tháng (mặc định ngày 25).</summary>
    public int LunchMonthlyWindowStartDay { get; set; } = 25;

    /// <summary>Ngày kết thúc kỳ đăng ký bán trú định kỳ hàng tháng (mặc định ngày 28).</summary>
    public int LunchMonthlyWindowEndDay { get; set; } = 28;

    public TimeSpan LunchCutoff => TimeSpan.TryParse(LunchCutoffTime, out var t) ? t : new TimeSpan(8, 15, 0);
    public TimeSpan AfternoonCutoff => TimeSpan.TryParse(AfternoonCutoffTime, out var t) ? t : new TimeSpan(13, 30, 0);
}
