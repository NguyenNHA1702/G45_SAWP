/**
 * Chuyển đổi mã/tên mối quan hệ phụ huynh sang tiếng Việt chuẩn
 */
export function formatRelationship(rel?: string | number | null): string {
  if (!rel) return 'Phụ huynh';
  const val = String(rel).trim();
  switch (val.toLowerCase()) {
    case 'father':
    case '1':
      return 'Bố (Cha)';
    case 'mother':
    case '2':
      return 'Mẹ';
    case 'guardian':
    case '3':
      return 'Người giám hộ';
    case 'other':
    case '4':
      return 'Người thân khác';
    default:
      return val;
  }
}
