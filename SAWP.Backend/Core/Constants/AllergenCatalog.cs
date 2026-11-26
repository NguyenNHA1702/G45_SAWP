using Core.Entities;

namespace Core.Constants;

public sealed record AllergenOption(string Code, string Label, IReadOnlyList<string> Keywords);

public static class AllergenCatalog
{
    public static readonly IReadOnlyList<AllergenOption> All = new List<AllergenOption>
    {
        new("seafood", "Hải sản (tôm, cua, cá, mực)", new[] { "seafood", "hải sản", "tôm", "cua", "cá", "mực", "ghẹ", "nghêu", "sò" }),
        new("dairy", "Sữa & chế phẩm từ sữa", new[] { "dairy", "sữa", "phô mai", "bơ sữa", "sữa chua", "flan", "kem" }),
        new("egg", "Trứng", new[] { "egg", "trứng" }),
        new("peanut", "Đậu phộng / Hạt", new[] { "peanut", "đậu phộng", "lạc", "hạt điều", "hạnh nhân", "óc chó" }),
        new("soy", "Đậu nành / Đậu phụ", new[] { "soy", "đậu nành", "đậu phụ", "tàu hũ", "xì dầu", "nước tương" }),
        new("gluten", "Gluten / Lúa mì", new[] { "gluten", "lúa mì", "bột mì", "mì sợi", "bánh mì" })
    };

    private static readonly Dictionary<string, AllergenOption> ByCode = All
        .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

    public static string? GetLabel(string code) =>
        ByCode.TryGetValue(code.Trim(), out var option) ? option.Label : null;

    public static List<string> ParseTags(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => ByCode.ContainsKey(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? NormalizeTagsCsv(IEnumerable<string>? tags)
    {
        if (tags == null) return null;
        var valid = tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => ByCode.ContainsKey(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return valid.Count == 0 ? null : string.Join(",", valid);
    }

    private static readonly string[] ExplicitAllergyMarkers =
    {
        "dị ứng", "di ung", "kiêng", "kieng", "không ăn", "khong an", "tránh", "allergy", "allergic"
    };

    private static readonly string[] LeadingNotePrefixes =
    {
        "bé bị dị ứng", "cháu bị dị ứng", "con bị dị ứng", "bị dị ứng", "dị ứng nặng", "dị ứng nhẹ", "dị ứng",
        "di ung", "kiêng ăn", "kiêng", "kieng", "không ăn được", "khong an duoc", "không ăn", "khong an",
        "tránh ăn", "tránh", "đừng cho ăn", "đừng ăn", "lưu ý", "nhờ bếp", "nhờ cô", "ghi chú",
        "bé bị", "cháu bị", "con bị", "bé", "cháu", "bị", "với"
    };

    private static bool ContainsWholePhrase(string? text, string? phrase)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase)) return false;

        var start = 0;
        while (start < text.Length)
        {
            var idx = text.IndexOf(phrase, start, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            bool leftOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            int afterIdx = idx + phrase.Length;
            bool rightOk = afterIdx >= text.Length || !char.IsLetterOrDigit(text[afterIdx]);
            if (leftOk && rightOk) return true;

            start = idx + 1;
        }

        return false;
    }

    private static List<string> ExtractCustomFoodTerms(string? trustedAllergyNotes)
    {
        if (string.IsNullOrWhiteSpace(trustedAllergyNotes)) return new List<string>();

        var rawSegments = trustedAllergyNotes
            .Split(new[] { ',', ';', '\n', '\r', '/', '&', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(seg => seg.Split(new[] { " và ", " hay ", " hoặc " }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var terms = new List<string>();
        foreach (var rawSeg in rawSegments)
        {
            var cleaned = rawSeg.Trim(' ', '.', ':', '-', '(', ')');
            bool stripped;
            do
            {
                stripped = false;
                foreach (var prefix in LeadingNotePrefixes)
                {
                    if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        int nextIdx = prefix.Length;
                        if (nextIdx == cleaned.Length || !char.IsLetterOrDigit(cleaned[nextIdx]))
                        {
                            cleaned = cleaned[nextIdx..].Trim(' ', '.', ':', '-', '(', ')');
                            stripped = true;
                            break;
                        }
                    }
                }
            } while (stripped && cleaned.Length > 0);

            if (cleaned.Length < 2) continue;

            // Bỏ qua nếu cụm từ đã nằm trong bộ từ khóa của 6 nhóm dị ứng chuẩn (tránh báo trùng)
            bool coveredByStandardGroup = All.Any(opt =>
                opt.Keywords.Any(kw => ContainsWholePhrase(cleaned, kw) && cleaned.Equals(kw, StringComparison.OrdinalIgnoreCase)));
            if (coveredByStandardGroup) continue;

            if (!terms.Contains(cleaned, StringComparer.OrdinalIgnoreCase))
            {
                terms.Add(cleaned);
            }
        }

        return terms;
    }

    /// <summary>
    /// Chỉ trích xuất phần ghi chú thực sự nói về dị ứng / kiêng khem (bỏ phần liệt kê tên món ăn trước dấu '|').
    /// Tránh báo nhầm khi Remarks chỉ ghi tên món ăn như "Đậu phụ sốt cà chua | Ghi chú: Suất ăn chay thanh đạm".
    /// </summary>
    public static string? ExtractExplicitAllergyNote(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return null;
        var notePart = rawText.Contains('|')
            ? rawText.Split('|', 2)[1]
            : rawText;

        var cleaned = notePart.Trim();
        if (string.IsNullOrEmpty(cleaned)) return null;

        return ExplicitAllergyMarkers.Any(m => cleaned.Contains(m, StringComparison.OrdinalIgnoreCase))
            ? cleaned
            : null;
    }

    public static string? FormatAllergySummary(
        IEnumerable<string>? studentAllergenTags,
        string? trustedAllergyNotes,
        string? contextualNote = null)
    {
        var tagLabels = (studentAllergenTags ?? Enumerable.Empty<string>())
            .Select(GetLabel)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var cleanTrusted = string.IsNullOrWhiteSpace(trustedAllergyNotes) ? null : trustedAllergyNotes.Trim();
        var cleanContext = ExtractExplicitAllergyNote(contextualNote);

        var parts = new List<string>();
        if (tagLabels.Count > 0)
        {
            parts.Add(string.Join(", ", tagLabels));
        }

        if (!string.IsNullOrEmpty(cleanTrusted))
        {
            parts.Add($"Ghi chú: {cleanTrusted}");
        }
        else if (!string.IsNullOrEmpty(cleanContext))
        {
            parts.Add(cleanContext);
        }

        return parts.Count > 0 ? string.Join(" • ", parts) : null;
    }

    /// <summary>
    /// So khớp danh sách món ăn trong ngày với tag dị ứng, ô ghi chú dị ứng chuyên biệt của phụ huynh (trustedAllergyNotes),
    /// và ghi chú ngữ cảnh lịch sử nếu có (contextualRemarks).
    /// </summary>
    public static List<string> DetectConflicts(
        IEnumerable<string>? studentAllergenTags,
        string? trustedAllergyNotes,
        IEnumerable<Dish> servedDishes,
        string? contextualRemarks = null)
    {
        var dishList = servedDishes.Where(d => d != null).ToList();
        if (dishList.Count == 0) return new List<string>();

        var studentTagSet = (studentAllergenTags ?? Enumerable.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => ByCode.ContainsKey(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1. Ô ghi chú dị ứng chuyên biệt của phụ huynh (StudentMealProfile.AllergyNotes):
        //    Tin cậy trực tiếp không bắt buộc phải gõ tiền tố "dị ứng" / "kiêng".
        var cleanTrustedNote = string.IsNullOrWhiteSpace(trustedAllergyNotes) ? null : trustedAllergyNotes.Trim();
        var inferredFromTrusted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(cleanTrustedNote))
        {
            foreach (var opt in All)
            {
                if (opt.Keywords.Any(kw => ContainsWholePhrase(cleanTrustedNote, kw)))
                {
                    studentTagSet.Add(opt.Code);
                    inferredFromTrusted.Add(opt.Code);
                }
            }
        }

        // 2. Văn bản ngữ cảnh (LunchRegistration.Remarks / Student.SpecialNeedsNotes):
        //    Chỉ quét khi thực sự có từ khóa dị ứng / kiêng khem để tránh báo nhầm tên món ăn chay.
        var explicitNote = ExtractExplicitAllergyNote(contextualRemarks);
        if (!string.IsNullOrEmpty(explicitNote))
        {
            foreach (var opt in All)
            {
                if (opt.Keywords.Any(kw => ContainsWholePhrase(explicitNote, kw)))
                {
                    studentTagSet.Add(opt.Code);
                }
            }
        }

        var conflicts = new List<string>();
        var alreadyWarnedDishesByGroup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in studentTagSet)
        {
            if (!ByCode.TryGetValue(tag, out var opt)) continue;

            var matchedDishes = dishList
                .Where(d =>
                {
                    var dishTags = ParseTags(d.AllergenTags);
                    if (dishTags.Contains(tag, StringComparer.OrdinalIgnoreCase)) return true;
                    return opt.Keywords.Any(kw => ContainsWholePhrase(d.Name, kw));
                })
                .Select(d => d.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matchedDishes.Count > 0)
            {
                conflicts.Add($"{opt.Label}: {string.Join(", ", matchedDishes)}");
                foreach (var name in matchedDishes)
                {
                    alreadyWarnedDishesByGroup.Add($"{tag}:{name}");
                }
            }
        }

        // 3. Bắt thêm từ khóa nguyên liệu/món ăn tự do trong ghi chú dị ứng (VD: "su su", "cà chua", "thịt bò")
        if (!string.IsNullOrEmpty(cleanTrustedNote))
        {
            var customTerms = ExtractCustomFoodTerms(cleanTrustedNote);
            foreach (var term in customTerms)
            {
                var matchedDishes = dishList
                    .Where(d => ContainsWholePhrase(d.Name, term))
                    .Where(d => !All.Any(opt =>
                        studentTagSet.Contains(opt.Code)
                        && opt.Keywords.Any(kw => ContainsWholePhrase(term, kw))
                        && alreadyWarnedDishesByGroup.Contains($"{opt.Code}:{d.Name}")))
                    .Select(d => d.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (matchedDishes.Count > 0)
                {
                    conflicts.Add($"Ghi chú dị ứng ({term}): {string.Join(", ", matchedDishes)}");
                }
            }
        }

        return conflicts;
    }
}
