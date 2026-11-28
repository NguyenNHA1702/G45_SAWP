using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Core.Common;

public static class StudentValidationHelper
{
    private static readonly string[] StrictDateFormats =
    {
        "dd/MM/yyyy",
        "d/M/yyyy",
        "dd-MM-yyyy",
        "d-M-yyyy",
        "yyyy-MM-dd",
        "yyyy/MM/dd"
    };

    private static readonly Regex StudentCodeRegex = new(@"^[A-Z0-9_-]{3,30}$", RegexOptions.Compiled);
    private static readonly Regex UsernameRegex = new(@"^[A-Za-z0-9._-]{3,50}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^0\d{9}$", RegexOptions.Compiled);
    private static readonly Regex IdentityCardRegex = new(@"^\d{9}(\d{3})?$", RegexOptions.Compiled);
    private static readonly Regex ValidPersonNameRegex = new(@"^[\p{L}\s'\-.]+$", RegexOptions.Compiled);
    private static readonly Regex ContainsLetterRegex = new(@"\p{L}", RegexOptions.Compiled);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex SectionSuffixRegex = new(@"^[A-Za-z]$", RegexOptions.Compiled);

    private static readonly HashSet<string> ValidRelationships = new(StringComparer.OrdinalIgnoreCase)
    {
        "bố", "cha", "father",
        "mẹ", "mother",
        "người giám hộ", "phụ huynh", "guardian",
        "ông", "bà"
    };

    public static bool TryParseStrictDate(string? raw, out DateTime parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var trimmed = raw.Trim();
        return DateTime.TryParseExact(
            trimmed,
            StrictDateFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out parsed);
    }

    public static bool IsValidStudentCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        return StudentCodeRegex.IsMatch(code.Trim().ToUpperInvariant());
    }

    public static string? ValidateFullName(string? fullName, string fieldLabel = "họ và tên học sinh")
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return $"Thiếu {fieldLabel}.";

        var cleaned = Regex.Replace(fullName.Trim(), @"\s+", " ");
        if (cleaned.Length < 2 || cleaned.Length > 100)
            return $"Độ dài {fieldLabel} phải từ 2 đến 100 ký tự.";

        if (cleaned.Contains('<') || cleaned.Contains('>'))
            return $"Trường {fieldLabel} không được chứa thẻ HTML.";

        if (!ValidPersonNameRegex.IsMatch(cleaned) || !ContainsLetterRegex.IsMatch(cleaned))
            return $"Trường {fieldLabel} chỉ được chứa chữ cái, khoảng trắng và dấu (' - .), không được chứa chữ số hoặc ký tự đặc biệt.";

        return null;
    }

    public static string? ValidateClassNameForGrade(string? className, int grade)
    {
        if (string.IsNullOrWhiteSpace(className))
            return "Vui lòng nhập tên lớp học.";

        var trimmed = className.Trim();
        if (trimmed.Contains('<') || trimmed.Contains('>') || HtmlTagRegex.IsMatch(trimmed))
            return "Tên lớp học không được chứa thẻ HTML hoặc ký tự đặc biệt.";

        if (!Regex.IsMatch(trimmed, $"^{grade}[A-Za-z]$"))
            return $"Tên lớp '{trimmed}' không hợp lệ với Khối {grade}. Quy tắc đặt tên: Khối ({grade}) + 1 chữ cái phân lớp (ví dụ: {grade}A, {grade}B).";

        return null;
    }

    public static bool IsValidSectionSuffix(string? suffix)
        => !string.IsNullOrWhiteSpace(suffix) && SectionSuffixRegex.IsMatch(suffix.Trim());

    public static bool ContainsHtmlTags(string? input)
        => !string.IsNullOrWhiteSpace(input) && (input.Contains('<') || input.Contains('>') || HtmlTagRegex.IsMatch(input));

    public static bool TryNormalizeGender(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var trimmed = raw.Trim();
        if (trimmed.Equals("Nam", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("Male", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Nam";
            return true;
        }
        if (trimmed.Equals("Nữ", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("Nu", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("Female", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Nữ";
            return true;
        }
        return false;
    }

    public static string? ValidateBirthDateAndAge(DateTime dob, int? referenceYear = null, bool allowAgeException = false)
    {
        if (dob == default || dob.Year < 1900)
            return "Ngày sinh không hợp lệ.";

        var today = DateTime.UtcNow.Date;
        if (dob.Date >= today.AddDays(1))
            return "Ngày sinh không hợp lệ (không thể là ngày trong tương lai).";

        var refYear = (referenceYear.HasValue && referenceYear.Value >= 2000) ? referenceYear.Value : today.Year;
        var age = refYear - dob.Year;
        if (age < 5)
            return $"Độ tuổi học sinh tiểu học không hợp lệ ({age} tuổi). Tuổi hợp lệ phải từ 5 đến 15 tuổi.";

        if (age > 15 && !allowAgeException)
            return $"Độ tuổi học sinh tiểu học không hợp lệ ({age} tuổi). Tuổi hợp lệ phải từ 5 đến 15 tuổi. Nếu là trường hợp ngoại lệ quá tuổi, vui lòng xác nhận ngoại lệ.";

        return null;
    }

    public static string? ValidateAgeForGrade(DateTime dob, int grade, int? schoolYearStartYear = null, bool isSpecialNeeds = false, bool allowAgeException = false)
    {
        var baseError = ValidateBirthDateAndAge(dob, schoolYearStartYear, allowAgeException);
        if (baseError != null) return baseError;

        if (grade >= 1 && grade <= 5)
        {
            var refYear = (schoolYearStartYear.HasValue && schoolYearStartYear.Value >= 2000) ? schoolYearStartYear.Value : DateTime.UtcNow.Year;
            var age = refYear - dob.Year;
            var minAge = Math.Max(5, grade + 4);
            var maxAge = isSpecialNeeds ? 15 : Math.Min(15, grade + 9);
            if (age < minAge)
            {
                return $"Độ tuổi của học sinh ({age} tuổi) không phù hợp với Khối {grade} (yêu cầu từ {minAge} đến {maxAge} tuổi).";
            }

            if (age > maxAge && !(allowAgeException && age > 15))
            {
                return $"Độ tuổi của học sinh ({age} tuổi) không phù hợp với Khối {grade} (yêu cầu từ {minAge} đến {maxAge} tuổi).";
            }
        }

        return null;
    }

    public static bool IsOverPrimaryAge(DateTime dob, int? referenceYear = null)
    {
        if (dob == default || dob.Year < 1900) return false;
        var refYear = (referenceYear.HasValue && referenceYear.Value >= 2000) ? referenceYear.Value : DateTime.UtcNow.Year;
        return (refYear - dob.Year) > 15;
    }

    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return true;
        return PhoneRegex.IsMatch(phone.Trim());
    }

    public static string? ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "Tên đăng nhập là bắt buộc.";

        var trimmed = username.Trim();
        if (trimmed.Any(char.IsWhiteSpace))
            return "Tên đăng nhập không được chứa khoảng trắng.";

        if (ContainsHtmlTags(trimmed) || !UsernameRegex.IsMatch(trimmed))
            return "Tên đăng nhập chỉ được gồm chữ cái không dấu, chữ số và ký tự '.', '_', '-' (từ 3 đến 50 ký tự).";

        return null;
    }

    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var trimmed = email.Trim();
        if (trimmed.Length > 256 || trimmed.Any(char.IsWhiteSpace) || ContainsHtmlTags(trimmed))
            return false;
        if (!MailAddress.TryCreate(trimmed, out var parsed) || parsed.Address != trimmed)
            return false;
        var host = parsed.Host;
        return host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.');
    }

    public static string NormalizeWhitespace(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ");

    public static bool IsValidIdentityCard(string? cccd)
    {
        if (string.IsNullOrWhiteSpace(cccd)) return true;
        var compact = Regex.Replace(cccd.Trim(), @"[\s\-]", "");
        return IdentityCardRegex.IsMatch(compact);
    }

    public static bool IsValidRelationship(string? relationship)
    {
        if (string.IsNullOrWhiteSpace(relationship)) return true;
        return ValidRelationships.Contains(relationship.Trim());
    }
}
