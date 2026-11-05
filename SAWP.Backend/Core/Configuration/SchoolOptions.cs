namespace Core.Configuration;

public class SchoolOptions
{
    public const string SectionName = "School";
    public string TimeZoneId { get; set; } = "SE Asia Standard Time";
    // Giờ chốt suất ăn bán trú trong ngày (giờ trường). Sau mốc này không tự hủy / khôi phục suất ăn.
    public string LunchCutoffTime { get; set; } = "08:15";

    public TimeSpan LunchCutoff => TimeSpan.TryParse(LunchCutoffTime, out var t) ? t : new TimeSpan(8, 15, 0);
}
