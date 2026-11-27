namespace Core.Constants;

/// <summary>
/// School-year states (UC-13, BR-14, BR-72): a new year starts as Draft, becomes Active when activated
/// (only one at a time), and the previously active year becomes Archived.
/// </summary>
public static class SchoolYearStatuses
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Archived = "Archived";
}
