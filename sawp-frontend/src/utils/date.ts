const pad = (n: number) => String(n).padStart(2, '0');

/** Date -> "yyyy-MM-dd" theo giờ máy (không dùng toISOString vì bị lệch UTC) */
export const toIsoDate = (d: Date): string =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;

export const todayIso = (): string => toIsoDate(new Date());

export const toLocalDateIso = (dateInput: string | Date | null | undefined): string => {
  if (!dateInput) return '';
  if (typeof dateInput === 'string') {
    const trimmed = dateInput.trim();
    if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed) || /^\d{4}-\d{2}-\d{2}T00:00:00(?:\.0+)?$/.test(trimmed)) {
      return trimmed.slice(0, 10);
    }
  }
  const d = typeof dateInput === 'string' ? new Date(dateInput) : dateInput;
  return isNaN(d.getTime()) ? '' : toIsoDate(d);
};

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
  new Date(ensureUtcSuffix(iso)).toLocaleString('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    dateStyle: 'short',
    timeStyle: 'short',
  });

/** Hiển thị HH:mm theo múi giờ Việt Nam (UTC+7) */
export const formatViTime = (iso: string): string => {
  if (!iso) return '';
  const d = new Date(ensureUtcSuffix(iso));
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleTimeString('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });
};

/** Tính ngày Thứ Hai của tuần kế tiếp (dạng YYYY-MM-DD theo giờ máy) */
export const getNextMondayIso = (baseDate: Date = new Date()): string => {
  const d = new Date(baseDate);
  const day = d.getDay(); // 0 is Sunday, 1 is Monday, ..., 6 is Saturday
  const diff = day === 0 ? 1 : 8 - day;
  d.setDate(d.getDate() + diff);
  return toIsoDate(d);
};

/** Chuẩn hóa hiển thị DD/MM/YYYY cho mọi ngày tháng (giữ nguyên date-only, chuyển UTC sang giờ máy nếu có giờ/múi giờ) */
export const formatDDMMYYYY = (dateInput: string | Date | null | undefined): string => {
  if (!dateInput) return 'N/A';
  try {
    if (typeof dateInput === 'string') {
      const trimmed = dateInput.trim();
      // Chỉ cắt trực tiếp khi là ngày thuần YYYY-MM-DD hoặc mốc 00:00:00 không kèm múi giờ
      if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed) || /^\d{4}-\d{2}-\d{2}T00:00:00(?:\.0+)?$/.test(trimmed)) {
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
    const day = pad(d.getDate());
    const month = pad(d.getMonth() + 1);
    const year = d.getFullYear();
    return `${day}/${month}/${year}`;
  } catch {
    return String(dateInput);
  }
};

/** Chuẩn hóa hiển thị HH:mm - DD/MM/YYYY theo giờ địa phương */
export const formatDDMMYYYYHHMM = (dateInput: string | Date | null | undefined): string => {
  if (!dateInput) return 'N/A';
  try {
    const d = typeof dateInput === 'string' ? new Date(dateInput.trim()) : dateInput;
    if (isNaN(d.getTime())) {
      return formatDDMMYYYY(dateInput);
    }
    return `${pad(d.getHours())}:${pad(d.getMinutes())} · ${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
  } catch {
    return formatDDMMYYYY(dateInput);
  }
};

export const monthRange = (year: number, month1to12: number): { from: string; to: string } => {
  const from = new Date(year, month1to12 - 1, 1);
  const to = new Date(year, month1to12, 0);
  return { from: toIsoDate(from), to: toIsoDate(to) };
};
