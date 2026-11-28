const USERNAME_REGEX = /^[A-Za-z0-9._-]{3,50}$/;
const PERSON_NAME_REGEX = /^[\p{L}\s'\-.]+$/u;
const CONTAINS_LETTER_REGEX = /\p{L}/u;
const PHONE_REGEX = /^0\d{9}$/;
const EMAIL_REGEX = /^[^\s@<>]+@[^\s@<>]+\.[^\s@<>]+$/;
const HTML_TAG_REGEX = /<[^>]*>/;

export function containsHtmlTags(value: string): boolean {
  return value.includes('<') || value.includes('>') || HTML_TAG_REGEX.test(value);
}

export function validateUsername(username: string): string | null {
  const trimmed = username.trim();
  if (!trimmed) return 'Vui lòng nhập tên đăng nhập.';
  if (/\s/.test(trimmed)) return 'Tên đăng nhập không được chứa khoảng trắng.';
  if (containsHtmlTags(trimmed) || !USERNAME_REGEX.test(trimmed)) {
    return "Tên đăng nhập chỉ được gồm chữ cái không dấu, chữ số và ký tự '.', '_', '-' (từ 3 đến 50 ký tự).";
  }
  return null;
}

export function validatePersonFullName(fullName: string): string | null {
  const cleaned = fullName.trim().replace(/\s+/g, ' ');
  if (!cleaned) return 'Vui lòng nhập họ và tên.';
  if (cleaned.length > 100) return 'Độ dài họ và tên tối đa 100 ký tự.';
  if (containsHtmlTags(cleaned)) return 'Họ và tên không được chứa thẻ HTML.';
  if (!PERSON_NAME_REGEX.test(cleaned) || !CONTAINS_LETTER_REGEX.test(cleaned)) {
    return "Họ và tên chỉ được chứa chữ cái, khoảng trắng và dấu (' - .), không được chứa chữ số hoặc ký tự đặc biệt.";
  }
  return null;
}

export function validateEmailAddress(email: string, required = true): string | null {
  const trimmed = email.trim();
  if (!trimmed) return required ? 'Vui lòng nhập địa chỉ email.' : null;
  if (trimmed.length > 256 || containsHtmlTags(trimmed) || !EMAIL_REGEX.test(trimmed)) {
    return 'Email không hợp lệ.';
  }
  return null;
}

export function validatePhoneNumber(phone: string): string | null {
  const trimmed = phone.trim();
  if (!trimmed) return null;
  if (!PHONE_REGEX.test(trimmed)) {
    return 'Số điện thoại không hợp lệ (cần gồm 10 chữ số và bắt đầu bằng số 0).';
  }
  return null;
}
