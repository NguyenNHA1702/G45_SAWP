using Core.Entities;
using Core.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>
/// Nguồn sự thật duy nhất cho lịch ngày học / ngày nghỉ / ngày học bù của trường.
/// </summary>
public class SchoolCalendarService : ISchoolCalendarService
{
    private readonly IUnitOfWork _unitOfWork;

    public SchoolCalendarService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> IsSchoolDayAsync(DateTime date, CancellationToken ct = default)
    {
        var targetDate = date.Date;
        var map = await GetCalendarMapInRangeAsync(targetDate, targetDate, ct);
        if (map.TryGetValue(targetDate, out var isSchoolDay))
        {
            return isSchoolDay;
        }

        return targetDate.DayOfWeek != DayOfWeek.Saturday && targetDate.DayOfWeek != DayOfWeek.Sunday;
    }

    public async Task<HashSet<DateTime>> GetSchoolDaysInRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var start = fromDate.Date;
        var end = toDate.Date;
        var result = new HashSet<DateTime>();
        if (end < start) return result;

        var overrides = await GetCalendarMapInRangeAsync(start, end, ct);

        for (var cur = start; cur <= end; cur = cur.AddDays(1))
        {
            if (overrides.TryGetValue(cur, out var isSchoolDay))
            {
                if (isSchoolDay) result.Add(cur);
            }
            else if (cur.DayOfWeek != DayOfWeek.Saturday && cur.DayOfWeek != DayOfWeek.Sunday)
            {
                result.Add(cur);
            }
        }

        return result;
    }

    public async Task<Dictionary<DateTime, bool>> GetCalendarMapInRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var start = fromDate.Date;
        var end = toDate.Date;
        var map = new Dictionary<DateTime, bool>();
        if (end < start) return map;

        var calRepo = _unitOfWork.Repository<SchoolCalendarDay>();
        if (calRepo == null) return map;

        var days = (await calRepo.FindAsync(d => d.Date >= start && d.Date <= end, ct)).ToList();
        if (days.Count == 0) return map;

        // Ưu tiên bản ghi thuộc năm học chứa khoảng ngày hoặc năm học đang Active
        int? preferredYearId = null;
        var yearRepo = _unitOfWork.Repository<SchoolYear>();
        if (yearRepo != null)
        {
            var years = (await yearRepo.GetAllAsync(ct)).ToList();
            if (years.Count > 0)
            {
                var matchingYear = years.FirstOrDefault(y => y.IsActive && y.StartDate.Date <= end && y.EndDate.Date >= start)
                    ?? years.FirstOrDefault(y => y.StartDate.Date <= end && y.EndDate.Date >= start)
                    ?? years.OrderByDescending(y => y.IsActive).ThenByDescending(y => y.StartDate).FirstOrDefault();
                preferredYearId = matchingYear?.Id;
            }
        }

        var filtered = preferredYearId.HasValue
            ? days.Where(d => d.SchoolYearId == preferredYearId.Value)
            : days;

        foreach (var d in filtered)
        {
            map[d.Date.Date] = d.IsSchoolDay;
        }

        return map;
    }
}
