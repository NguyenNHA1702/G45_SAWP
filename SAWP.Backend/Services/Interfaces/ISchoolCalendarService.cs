namespace Services.Interfaces;

/// <summary>
/// Nguồn sự thật duy nhất xác định ngày học, ngày nghỉ lễ và ngày học bù theo lịch nhà trường.
/// </summary>
public interface ISchoolCalendarService
{
    /// <summary>
    /// Kiểm tra một ngày cụ thể có phải là ngày học hay không.
    /// Ưu tiên SchoolCalendarDay của năm học phù hợp; nếu chưa cấu hình thì T2–T6 là ngày học, T7–CN là ngày nghỉ.
    /// </summary>
    Task<bool> IsSchoolDayAsync(DateTime date, CancellationToken ct = default);

    /// <summary>
    /// Trả về tập hợp các ngày học thực tế trong khoảng [fromDate, toDate].
    /// </summary>
    Task<HashSet<DateTime>> GetSchoolDaysInRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);

    /// <summary>
    /// Trả về bản đồ cấu hình ghi đè (ngày -> IsSchoolDay) trong khoảng [fromDate, toDate].
    /// </summary>
    Task<Dictionary<DateTime, bool>> GetCalendarMapInRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}
