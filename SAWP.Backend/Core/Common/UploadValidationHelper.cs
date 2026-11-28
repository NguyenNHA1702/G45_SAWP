using System.Text;

namespace Core.Common;

/// <summary>
/// Shared upload filename sanitization, length checks, and file signature (magic bytes) validation.
/// </summary>
public static class UploadValidationHelper
{
    public const int MaxFileNameLength = 150;
    public const int MaxAttachmentUrlLength = 500;

    /// <summary>
    /// Extracts the leaf filename and replaces path traversal / unsafe characters so saved disk names stay safe.
    /// </summary>
    public static string SanitizeFileName(string? rawFileName, int maxLength = MaxFileNameLength)
    {
        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            return "file";
        }

        // Handle both Windows and Unix separators cleanly regardless of host OS
        var normalized = rawFileName.Replace('\\', '/');
        var leaf = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "file";
        leaf = Path.GetFileName(leaf).Trim();

        var rawExt = Path.GetExtension(leaf);
        var stem = Path.GetFileNameWithoutExtension(leaf);

        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(stem.Length);
        foreach (var ch in stem)
        {
            if (char.IsControl(ch)
                || invalidChars.Contains(ch)
                || ch is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' or ';' or '#' or '%' or '&' or '\'' or ' ')
            {
                sb.Append('_');
            }
            else
            {
                sb.Append(ch);
            }
        }

        var cleanStem = sb.ToString().Replace("..", "_", StringComparison.Ordinal).Trim('.', '_', ' ');
        if (string.IsNullOrWhiteSpace(cleanStem))
        {
            cleanStem = "file";
        }

        // Clean extension characters while preserving extension case
        var cleanExt = new string(rawExt.Where(c => c == '.' || char.IsLetterOrDigit(c)).ToArray());
        var maxStemLen = Math.Max(8, maxLength - cleanExt.Length);
        if (cleanStem.Length > maxStemLen)
        {
            cleanStem = cleanStem[..maxStemLen];
        }

        return cleanStem + cleanExt;
    }

    public static bool IsSafeFileNameLength(string? rawFileName, int maxLength = MaxFileNameLength)
    {
        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            return false;
        }

        var normalized = rawFileName.Replace('\\', '/');
        var leaf = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        return leaf.Length > 0 && leaf.Length <= maxLength;
    }

    /// <summary>
    /// Validates extension, size, filename length, and binary magic bytes (rejects disguised text/PHP/HTML payloads).
    /// Rewinds seekable streams back to offset 0 before returning.
    /// </summary>
    public static async Task<string?> ValidateFileStreamAsync(
        Stream stream,
        string? rawFileName,
        IReadOnlyCollection<string> allowedExtensions,
        long maxBytes,
        long? declaredLength = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveLength = declaredLength ?? (stream.CanSeek ? stream.Length : 0);
        if (effectiveLength <= 0)
        {
            return "Vui lòng chọn tệp tin cần tải lên.";
        }

        if (effectiveLength > maxBytes)
        {
            var maxMb = Math.Max(1, maxBytes / (1024 * 1024));
            return $"Kích thước tệp không được vượt quá {maxMb}MB.";
        }

        if (!IsSafeFileNameLength(rawFileName))
        {
            return $"Tên tệp tin không hợp lệ hoặc vượt quá {MaxFileNameLength} ký tự.";
        }

        var sanitized = SanitizeFileName(rawFileName);
        var ext = Path.GetExtension(sanitized).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            return $"Định dạng tệp không được hỗ trợ. Chỉ chấp nhận: {string.Join(", ", allowedExtensions)}.";
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var prefix = new byte[Math.Min(512, (int)Math.Min(512, Math.Max(16, effectiveLength)))];
        var bytesRead = await stream.ReadAsync(prefix.AsMemory(0, prefix.Length), cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        if (bytesRead < 4)
        {
            return "Tệp rỗng hoặc không hợp lệ.";
        }

        var asciiPrefix = Encoding.ASCII.GetString(prefix, 0, bytesRead);
        if (asciiPrefix.Contains("<?php", StringComparison.OrdinalIgnoreCase)
            || asciiPrefix.Contains("<?=", StringComparison.OrdinalIgnoreCase)
            || asciiPrefix.Contains("<script", StringComparison.OrdinalIgnoreCase))
        {
            return "Nội dung tệp chứa mã không hợp lệ.";
        }

        bool validMagic = ext switch
        {
            ".pdf" => prefix[0] == 0x25 && prefix[1] == 0x50 && prefix[2] == 0x44 && prefix[3] == 0x46, // %PDF
            ".png" => bytesRead >= 8
                      && prefix[0] == 0x89 && prefix[1] == 0x50 && prefix[2] == 0x4E && prefix[3] == 0x47
                      && prefix[4] == 0x0D && prefix[5] == 0x0A && prefix[6] == 0x1A && prefix[7] == 0x0A,
            ".jpg" or ".jpeg" => prefix[0] == 0xFF && prefix[1] == 0xD8 && prefix[2] == 0xFF,
            ".xlsx" => prefix[0] == 0x50 && prefix[1] == 0x4B && prefix[2] == 0x03 && prefix[3] == 0x04, // ZIP PK
            _ => false
        };

        if (!validMagic)
        {
            return "Nội dung tệp không khớp với phần mở rộng định dạng hoặc tệp đã bị hư hỏng.";
        }

        return null;
    }
}
