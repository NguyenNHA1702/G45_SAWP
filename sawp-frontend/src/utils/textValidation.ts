const HTML_OR_SCRIPT_REGEX =
  /[<>]|&(lt|gt);|&#\d+;|&#x[0-9a-f]+;|%3[ce]|\b(?:javascript|vbscript)\s*:|\bon[a-z]{2,}\s*=/i;

export function normalizeWhitespace(input: string): string {
  return input.trim().replace(/\s+/g, ' ');
}

export function containsUnsafeHtmlOrScript(input: string): boolean {
  if (!input || !input.trim()) return false;
  return HTML_OR_SCRIPT_REGEX.test(input);
}

export function isMeaningfulText(input: string, minLetters = 3): boolean {
  const normalized = normalizeWhitespace(input);
  if (!normalized) return false;

  let letterCount = 0;
  let digitCount = 0;
  let nonSpaceCount = 0;
  const distinctLetters = new Set<string>();
  const distinctNonSpace = new Set<string>();

  for (const ch of normalized) {
    if (/\s/.test(ch)) continue;
    nonSpaceCount++;
    distinctNonSpace.add(ch.toLowerCase());
    if (/\p{L}/u.test(ch)) {
      letterCount++;
      distinctLetters.add(ch.toLowerCase());
    } else if (/\d/.test(ch)) {
      digitCount++;
    }
  }

  if (nonSpaceCount === 0 || letterCount < minLetters) return false;
  if (distinctNonSpace.size < 2 || distinctLetters.size < 2) return false;
  if ((letterCount + digitCount) / nonSpaceCount < 0.5) return false;

  return true;
}

export function validateFreeText(
  input: string,
  fieldLabel: string,
  minLength = 5,
  maxLength = 1000
): string | null {
  const normalized = normalizeWhitespace(input);
  if (!normalized) {
    return `Vui lòng nhập ${fieldLabel}.`;
  }
  const capitalized = fieldLabel.charAt(0).toUpperCase() + fieldLabel.slice(1);
  if (containsUnsafeHtmlOrScript(normalized)) {
    return `${capitalized} không được chứa thẻ HTML, ký tự '<', '>' hoặc mã kịch bản.`;
  }
  if (normalized.length < minLength || normalized.length > maxLength) {
    return `${capitalized} phải có độ dài từ ${minLength} đến ${maxLength} ký tự.`;
  }
  if (!isMeaningfulText(normalized)) {
    return `${capitalized} không hợp lệ (vui lòng nhập nội dung rõ ràng, có nghĩa, không chỉ gồm số, ký tự lặp hoặc ký tự đặc biệt).`;
  }
  return null;
}
