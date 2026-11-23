namespace Core.Enums;

/// <summary>State of a user account (priv ERD row 1, BR-01, BR-63). Only Active accounts can sign in.</summary>
public enum AccountStatus
{
    /// <summary>Created without a usable password; becomes Active when the holder sets one through the activation link.</summary>
    PendingActivation = 1,
    Active = 2,
    Deactivated = 3
}
