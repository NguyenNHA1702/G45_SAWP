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

export const formatViDateTime = (iso: string): string =>
  new Date(iso).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });

export const monthRange = (year: number, month1to12: number): { from: string; to: string } => {
  const from = new Date(year, month1to12 - 1, 1);
  const to = new Date(year, month1to12, 0);
  return { from: toIsoDate(from), to: toIsoDate(to) };
};
