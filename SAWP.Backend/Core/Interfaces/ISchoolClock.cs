namespace Core.Interfaces;

public interface ISchoolClock
{
    DateTime Now { get; }
    DateTime Today { get; }
    DateTime ToSchoolTime(DateTime utc);
}
