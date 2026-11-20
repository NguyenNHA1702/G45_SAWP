using System.Text.RegularExpressions;

namespace Core.Constants;

// Rule for every new or changed password: at least 8 characters with an upper-case letter,
// a lower-case letter, a digit and a special character.
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static bool IsValid(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength) return false;
        return password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]");
    }
}
