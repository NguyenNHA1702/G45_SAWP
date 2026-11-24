namespace Core.Enums;

/// <summary>What an emailed single-use link is for (BR-04, BR-63, BR-69).</summary>
public enum VerificationTokenPurpose
{
    Activation = 1,
    PasswordRecovery = 2,
    EmailChange = 3
}
