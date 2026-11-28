using System.Text.RegularExpressions;

namespace Core.Common;

/// <summary>
/// Shared validation helpers for free-text fields (reasons, purposes, descriptions, notes)
/// to block HTML/script payloads and meaningless/gibberish inputs.
/// </summary>
public static class TextValidationHelper
{
    private static readonly Regex HtmlTagOrAngleRegex = new(@"[<>]|<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex EncodedHtmlEntityRegex = new(@"&(lt|gt);|&#\d+;|&#x[0-9a-f]+;|%3[ce]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ScriptInjectionRegex = new(@"(?:\b(?:javascript|vbscript)\s*:|\bon[a-z]{2,}\s*=)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static string NormalizeWhitespace(string? input)
        => string.IsNullOrWhiteSpace(input) ? string.Empty : WhitespaceRegex.Replace(input.Trim(), " ");

    /// <summary>
    /// Returns true if the text contains raw HTML tags, angle brackets, encoded HTML entities, or script/event-handler patterns.
    /// </summary>
    public static bool ContainsUnsafeHtmlOrScript(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        return HtmlTagOrAngleRegex.IsMatch(input)
            || EncodedHtmlEntityRegex.IsMatch(input)
            || ScriptInjectionRegex.IsMatch(input);
    }

    /// <summary>
    /// Returns true if the text appears to be meaningful natural language rather than digits, symbols, or repeated characters.
    /// </summary>
    public static bool IsMeaningfulText(string? input, int minLetters = 3)
    {
        var normalized = NormalizeWhitespace(input);
        if (string.IsNullOrEmpty(normalized)) return false;

        int letterCount = 0;
        int digitCount = 0;
        int nonSpaceCount = 0;
        var distinctLetters = new HashSet<char>();
        var distinctNonSpace = new HashSet<char>();

        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch)) continue;
            nonSpaceCount++;
            distinctNonSpace.Add(char.ToLowerInvariant(ch));
            if (char.IsLetter(ch))
            {
                letterCount++;
                distinctLetters.Add(char.ToLowerInvariant(ch));
            }
            else if (char.IsDigit(ch))
            {
                digitCount++;
            }
        }

        if (nonSpaceCount == 0 || letterCount < minLetters) return false;
        if (distinctNonSpace.Count < 2 || distinctLetters.Count < 2) return false;
        if ((double)(letterCount + digitCount) / nonSpaceCount < 0.50) return false;

        return true;
    }

    /// <summary>
    /// Validates a free-text field for HTML/script injection, length, and meaningfulness.
    /// Returns null if valid, or a Vietnamese error message if invalid.
    /// </summary>
    public static string? ValidateFreeText(
        string? input,
        string fieldLabel,
        int minLength = 5,
        int maxLength = 1000,
        bool checkMeaningful = true)
    {
        var normalized = NormalizeWhitespace(input);
        if (string.IsNullOrEmpty(normalized))
        {
            return $"Vui lòng nhập {fieldLabel}.";
        }

        if (ContainsUnsafeHtmlOrScript(normalized))
        {
            return $"{char.ToUpperInvariant(fieldLabel[0])}{fieldLabel[1..]} không được chứa thẻ HTML, ký tự '<', '>' hoặc mã kịch bản.";
        }

        if (normalized.Length < minLength || normalized.Length > maxLength)
        {
            return $"{char.ToUpperInvariant(fieldLabel[0])}{fieldLabel[1..]} phải có độ dài từ {minLength} đến {maxLength} ký tự.";
        }

        if (checkMeaningful && !IsMeaningfulText(normalized))
        {
            return $"{char.ToUpperInvariant(fieldLabel[0])}{fieldLabel[1..]} không hợp lệ (vui lòng nhập nội dung rõ ràng, có nghĩa, không chỉ gồm số, ký tự lặp hoặc ký tự đặc biệt).";
        }

        return null;
    }
}
