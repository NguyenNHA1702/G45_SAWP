using Core.Configuration;
using Core.Interfaces;
using Microsoft.Extensions.Options;

namespace Infrastructure.Common;

public class SchoolClock : ISchoolClock
{
    private readonly TimeZoneInfo _schoolTimeZone;

    public SchoolClock(IOptions<SchoolOptions> options)
    {
        try
        {
            _schoolTimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback to UTC+7 if system timezone id differences occur
            _schoolTimeZone = TimeZoneInfo.CreateCustomTimeZone("SchoolTZ", TimeSpan.FromHours(7), "SchoolTZ", "SchoolTZ");
        }
    }

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _schoolTimeZone);
    public DateTime Today => Now.Date;
    public DateTime ToSchoolTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, _schoolTimeZone);
}
