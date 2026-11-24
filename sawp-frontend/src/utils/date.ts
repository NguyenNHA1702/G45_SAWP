const pad = (n: number) => String(n).padStart(2, '0');

/** Date -> "yyyy-MM-dd" theo giờ máy (không dùng toISOString vì bị lệch UTC) */
export const toIsoDate = (d: Date): string =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

export const todayIso = (): string => toIsoDate(new Date());

/** "2026-10-19" -> "19/10/2026" */
export const formatViDate = (iso: string): string => {
  const [y, m, d] = iso.slice(0, 10).split('-');
  return `${d}/${m}/${y}`;
};

const ensureUtcSuffix = (iso: string): string => {
  const trimmed = iso.trim();
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(trimmed) && !/[Zz]|[+-]\d{2}:?\d{2}$/.test(trimmed)) {
    return `${trimmed}Z`;
  }
  return trimmed;
};

export const formatViDateTime = (iso: string): string =>
  new Date(ensureUtcSuffix(iso)).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });

/** Tính ngày Thứ Hai của tuần kế tiếp (dạng YYYY-MM-DD theo giờ máy) */
export const getNextMondayIso = (baseDate: Date = new Date()): string => {
  const d = new Date(baseDate);
  const day = d.getDay(); // 0 is Sunday, 1 is Monday, ..., 6 is Saturday
  const diff = day === 0 ? 1 : 8 - day;
  d.setDate(d.getDate() + diff);
  return toIsoDate(d);
};

/** Chuẩn hóa hiển thị DD/MM/YYYY cho mọi ngày tháng */
export const formatDDMMYYYY = (dateInput: string | Date | null | undefined): string => {
  if (!dateInput) return 'N/A';
  try {
    if (typeof dateInput === 'string') {
      const trimmed = dateInput.trim();
      // Nếu chuỗi dạng YYYY-MM-DD...
      if (/^\d{4}-\d{2}-\d{2}/.test(trimmed)) {
        const parts = trimmed.slice(0, 10).split('-');
        return `${parts[2]}/${parts[1]}/${parts[0]}`;
      }
      // Nếu chuỗi dạng DD/MM/YYYY sẵn
      if (/^\d{2}\/\d{2}\/\d{4}$/.test(trimmed)) {
        return trimmed;
      }
    }
    const d = typeof dateInput === 'string' ? new Date(dateInput) : dateInput;
    if (isNaN(d.getTime())) {
      return String(dateInput);
    }
    const day = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year = d.getFullYear();
    return `${day}/${month}/${year}`;
  } catch {
    return String(dateInput);
  }
};

export const monthRange = (year: number, month1to12: number): { from: string; to: string } => {
  const from = new Date(year, month1to12 - 1, 1);
  const to = new Date(year, month1to12, 0);
  return { from: toIsoDate(from), to: toIsoDate(to) };
};
