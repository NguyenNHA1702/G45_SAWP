import type { DishCategory } from '../types/lunch.types';

export interface AllergenOption {
  code: string;
  label: string;
  shortLabel: string;
  keywords: string[];
}

export const ALLERGEN_OPTIONS: AllergenOption[] = [
  { code: 'seafood', label: 'Hải sản (tôm, cua, cá, mực)', shortLabel: 'Hải sản', keywords: ['hải sản', 'tôm', 'cua', 'cá', 'mực', 'ghẹ', 'nghêu', 'sò'] },
  { code: 'dairy', label: 'Sữa & chế phẩm từ sữa', shortLabel: 'Sữa', keywords: ['sữa', 'phô mai', 'bơ sữa', 'sữa chua', 'flan', 'kem'] },
  { code: 'egg', label: 'Trứng', shortLabel: 'Trứng', keywords: ['trứng'] },
  { code: 'peanut', label: 'Đậu phộng / Hạt', shortLabel: 'Đậu phộng', keywords: ['đậu phộng', 'lạc', 'hạt điều', 'hạnh nhân', 'óc chó'] },
  { code: 'soy', label: 'Đậu nành / Đậu phụ', shortLabel: 'Đậu nành', keywords: ['đậu nành', 'đậu phụ', 'tàu hũ', 'xì dầu', 'nước tương'] },
  { code: 'gluten', label: 'Gluten / Lúa mì', shortLabel: 'Gluten', keywords: ['gluten', 'lúa mì', 'bột mì', 'mì sợi', 'bánh mì'] },
];

export function getAllergenShortLabel(code: string): string {
  return ALLERGEN_OPTIONS.find((x) => x.code.toLowerCase() === code.toLowerCase())?.shortLabel ?? code;
}

export function getAllergenFullLabel(code: string): string {
  return ALLERGEN_OPTIONS.find((x) => x.code.toLowerCase() === code.toLowerCase())?.label ?? code;
}

const LETTER_OR_DIGIT_RE = /[\p{L}\p{N}]/u;

const LEADING_ALLERGY_PREFIXES = [
  'bé bị dị ứng',
  'cháu bị dị ứng',
  'con bị dị ứng',
  'bị dị ứng',
  'dị ứng nặng',
  'dị ứng nhẹ',
  'dị ứng',
  'di ung',
  'kiêng ăn',
  'kiêng',
  'kieng',
  'không ăn được',
  'khong an duoc',
  'không ăn',
  'khong an',
  'tránh ăn',
  'tránh',
  'đừng cho ăn',
  'đừng ăn',
  'lưu ý',
  'nhờ bếp',
  'nhờ cô',
  'ghi chú',
  'bé bị',
  'cháu bị',
  'con bị',
  'bé',
  'cháu',
  'bị',
  'với',
];

const EXPLICIT_ALLERGY_MARKERS = [
  'dị ứng',
  'di ung',
  'kiêng',
  'kieng',
  'không ăn',
  'khong an',
  'tránh',
  'allergy',
  'allergic',
];

export function containsWholePhrase(text?: string | null, phrase?: string | null): boolean {
  if (!text || !phrase) return false;
  const hay = text.toLowerCase();
  const needle = phrase.toLowerCase().trim();
  if (!needle) return false;

  let start = 0;
  while (start < hay.length) {
    const idx = hay.indexOf(needle, start);
    if (idx < 0) return false;

    const leftOk = idx === 0 || !LETTER_OR_DIGIT_RE.test(hay[idx - 1]);
    const afterIdx = idx + needle.length;
    const rightOk = afterIdx >= hay.length || !LETTER_OR_DIGIT_RE.test(hay[afterIdx]);
    if (leftOk && rightOk) return true;

    start = idx + 1;
  }
  return false;
}

function extractCustomFoodTerms(trustedNotes?: string | null): string[] {
  if (!trustedNotes || !trustedNotes.trim()) return [];

  const rawSegments = trustedNotes
    .split(/[,;\n\r/&|]+/)
    .flatMap((seg) => seg.split(/\s+(?:và|hay|hoặc)\s+/i))
    .map((s) => s.trim())
    .filter(Boolean);

  const terms: string[] = [];
  for (const raw of rawSegments) {
    let cleaned = raw.replace(/^[\s.:;\-()]+|[\s.:;\-()]+$/g, '');
    let stripped = true;
    while (stripped && cleaned.length > 0) {
      stripped = false;
      const lower = cleaned.toLowerCase();
      for (const prefix of LEADING_ALLERGY_PREFIXES) {
        if (lower.startsWith(prefix)) {
          const nextIdx = prefix.length;
          if (nextIdx === cleaned.length || !LETTER_OR_DIGIT_RE.test(cleaned[nextIdx])) {
            cleaned = cleaned.slice(nextIdx).replace(/^[\s.:;\-()]+|[\s.:;\-()]+$/g, '');
            stripped = true;
            break;
          }
        }
      }
    }

    if (cleaned.length < 2) continue;
    const coveredByStandard = ALLERGEN_OPTIONS.some((opt) =>
      opt.keywords.some((kw) => kw.toLowerCase() === cleaned.toLowerCase())
    );
    if (coveredByStandard) continue;

    if (!terms.some((t) => t.toLowerCase() === cleaned.toLowerCase())) {
      terms.push(cleaned);
    }
  }
  return terms;
}

export interface LiveDishConflictResult {
  conflicts: string[];
  conflictingDishNames: string[];
}

export function detectLiveDishConflicts(
  studentAllergenTags: string[] | undefined,
  studentAllergyNotes: string | null | undefined,
  dishes: Array<{ name: string; allergenTags?: string[] }>,
  contextualWarning?: string | null
): LiveDishConflictResult {
  if (!dishes || dishes.length === 0) {
    return { conflicts: [], conflictingDishNames: [] };
  }

  const tagSet = new Set<string>(
    (studentAllergenTags ?? [])
      .map((t) => t.trim().toLowerCase())
      .filter((t) => ALLERGEN_OPTIONS.some((opt) => opt.code === t))
  );

  const cleanNotes = (studentAllergyNotes ?? '').trim();
  if (cleanNotes) {
    for (const opt of ALLERGEN_OPTIONS) {
      if (opt.keywords.some((kw) => containsWholePhrase(cleanNotes, kw))) {
        tagSet.add(opt.code);
      }
    }
  }

  const cleanContext = (contextualWarning ?? '').trim();
  if (cleanContext && EXPLICIT_ALLERGY_MARKERS.some((m) => cleanContext.toLowerCase().includes(m))) {
    for (const opt of ALLERGEN_OPTIONS) {
      if (opt.keywords.some((kw) => containsWholePhrase(cleanContext, kw))) {
        tagSet.add(opt.code);
      }
    }
  }

  const conflicts: string[] = [];
  const conflictingNames = new Set<string>();
  const warnedByGroup = new Set<string>();

  for (const opt of ALLERGEN_OPTIONS) {
    if (!tagSet.has(opt.code)) continue;
    const matched = dishes
      .filter(
        (d) =>
          (d.allergenTags ?? []).some((t) => t.toLowerCase() === opt.code) ||
          opt.keywords.some((kw) => containsWholePhrase(d.name, kw))
      )
      .map((d) => d.name);
    const uniqueMatched = Array.from(new Set(matched));
    if (uniqueMatched.length > 0) {
      conflicts.push(`${opt.label}: ${uniqueMatched.join(', ')}`);
      for (const name of uniqueMatched) {
        conflictingNames.add(name.toLowerCase());
        warnedByGroup.add(`${opt.code}:${name.toLowerCase()}`);
      }
    }
  }

  if (cleanNotes) {
    const customTerms = extractCustomFoodTerms(cleanNotes);
    for (const term of customTerms) {
      const matched = dishes
        .filter((d) => containsWholePhrase(d.name, term))
        .filter(
          (d) =>
            !ALLERGEN_OPTIONS.some(
              (opt) =>
                tagSet.has(opt.code) &&
                opt.keywords.some((kw) => containsWholePhrase(term, kw)) &&
                warnedByGroup.has(`${opt.code}:${d.name.toLowerCase()}`)
            )
        )
        .map((d) => d.name);
      const uniqueMatched = Array.from(new Set(matched));
      if (uniqueMatched.length > 0) {
        conflicts.push(`Ghi chú dị ứng (${term}): ${uniqueMatched.join(', ')}`);
        for (const name of uniqueMatched) {
          conflictingNames.add(name.toLowerCase());
        }
      }
    }
  }

  return {
    conflicts,
    conflictingDishNames: Array.from(conflictingNames),
  };
}

export const DISH_CATEGORY_OPTIONS: { value: DishCategory; label: string; emoji: string }[] = [
  { value: 1, label: 'Tinh bột (Cơm/Cháo)', emoji: '🍚' },
  { value: 2, label: 'Món mặn chính', emoji: '🍗' },
  { value: 3, label: 'Món rau / Món phụ', emoji: '🥬' },
  { value: 4, label: 'Canh', emoji: '🍲' },
  { value: 5, label: 'Tráng miệng', emoji: '🍌' },
];

export function normalizeDishCategory(cat: DishCategory | string): number {
  if (typeof cat === 'number') return cat;
  switch (cat) {
    case 'Starch':
      return 1;
    case 'MainDish':
      return 2;
    case 'SideDish':
      return 3;
    case 'Soup':
      return 4;
    case 'Dessert':
      return 5;
    default:
      return Number(cat) || 2;
  }
}

export function getDishCategoryInfo(cat: DishCategory | string) {
  const num = normalizeDishCategory(cat);
  return DISH_CATEGORY_OPTIONS.find((c) => c.value === num) ?? DISH_CATEGORY_OPTIONS[1];
}

export function normalizeMealTypeNumber(mt?: string | number | null): number {
  if (mt === 2 || mt === '2' || mt === 'VegetarianLunch') return 2;
  if (mt === 3 || mt === '3' || mt === 'SpecialDiet') return 3;
  return 1;
}
