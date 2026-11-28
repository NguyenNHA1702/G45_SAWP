namespace Core.Enums;

/// <summary>How school staff contacted the family about an unexcused absence (OVR-10, UC-23 A1).</summary>
public enum ContactChannel
{
    Phone = 1,
    Zalo = 2,
    InPerson = 3,
    Other = 4
}

/// <summary>Whether the family was reached (OVR-10, UC-23 A1).</summary>
public enum ContactOutcome
{
    Reached = 1,
    NotReached = 2
}
