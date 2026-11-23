namespace Core.Common;

public static class SchoolCalendarHelper
{
    public static bool IsPublicHoliday(DateTime date) =>
        (date.Month == 1 && date.Day == 1)
        || (date.Month == 4 && date.Day == 30)
        || (date.Month == 5 && date.Day == 1)
        || (date.Month == 9 && date.Day == 2);

    public static bool IsDefaultSchoolDay(DateTime date) =>
        date.DayOfWeek != DayOfWeek.Saturday
        && date.DayOfWeek != DayOfWeek.Sunday
        && !IsPublicHoliday(date);

    public static bool IsSchoolDay(DateTime date, IReadOnlyDictionary<DateTime, bool> calendarMap)
    {
        if (calendarMap.TryGetValue(date.Date, out var overrideFlag))
        {
            return overrideFlag;
        }

        return IsDefaultSchoolDay(date);
    }
}
