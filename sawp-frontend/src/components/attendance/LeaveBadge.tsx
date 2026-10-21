import type { LeaveIndicator } from '../../types/attendance.types';
import { formatViDate } from '../../utils/date';

export function LeaveBadge({ leave }: { leave: LeaveIndicator | null }) {
  if (!leave) return <span className="badge badge--muted">—</span>;
  const approved = leave.status === 'Approved';
  const tooltip = `${leave.requestNumber}: ${formatViDate(leave.startDate)} → ${formatViDate(leave.endDate)}\n${leave.reason}`;
  return (
    <span className={`badge ${approved ? 'badge--violet' : 'badge--amber'}`} title={tooltip}>
      {approved ? 'Đơn đã duyệt' : 'Đơn chờ duyệt'}
    </span>
  );
}
