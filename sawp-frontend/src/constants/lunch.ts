// Giờ chốt suất ăn bán trú mặc định hiển thị trên giao diện (Quy tắc địa phương Tiểu học Tân Lập A: 08h15).
// Giữ khớp với School:LunchCutoffTime trong SAWP.Backend/API/appsettings.json.
export const LUNCH_CUTOFF_LABEL = '08h15';
export const LUNCH_CUTOFF_DEFAULT_TIME = '08:15';

/** Định dạng ngày thống nhất DD-MM-YYYY (tránh lệch múi giờ với chuỗi YYYY-MM-DD). */
export function formatDateDDMMYYYY(input: string | Date | null | undefined): string {
  if (!input) return '';
  if (typeof input === 'string') {
    const match = input.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (match) {
      return `${match[3]}-${match[2]}-${match[1]}`;
    }
  }
  const d = input instanceof Date ? input : new Date(input);
  if (Number.isNaN(d.getTime())) return String(input);
  const dd = String(d.getDate()).padStart(2, '0');
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const yyyy = d.getFullYear();
  return `${dd}-${mm}-${yyyy}`;
}

const WEEKDAY_SHORT_VI = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];

/** Định dạng Thứ + DD-MM-YYYY, ví dụ: T2, 22-11-2026 */
export function formatWeekdayDateDDMMYYYY(input: string | Date | null | undefined): string {
  if (!input) return '';
  let d: Date;
  if (typeof input === 'string') {
    const match = input.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (match) {
      d = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
    } else {
      d = new Date(input);
    }
  } else {
    d = input;
  }
  if (Number.isNaN(d.getTime())) return formatDateDDMMYYYY(input);
  return `${WEEKDAY_SHORT_VI[d.getDay()]}, ${formatDateDDMMYYYY(d)}`;
}
