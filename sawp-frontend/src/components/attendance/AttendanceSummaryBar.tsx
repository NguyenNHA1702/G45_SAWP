import type { AttendanceSummary } from '../../types/attendance.types';
import './AttendanceSummaryBar.css';

export function AttendanceSummaryBar({ summary }: { summary: AttendanceSummary }) {
  const items = [
    { key: 'total', label: 'Sĩ số', value: summary.total, tone: '' },
    { key: 'present', label: 'Có mặt', value: summary.present, tone: 'chip--present' },
    { key: 'late', label: 'Đi muộn', value: summary.late, tone: 'chip--late' },
    { key: 'excused', label: 'Vắng có phép', value: summary.excusedAbsence, tone: 'chip--excused' },
    { key: 'unexcused', label: 'Vắng không phép', value: summary.unexcusedAbsence, tone: 'chip--unexcused' },
    { key: 'notMarked', label: 'Chưa điểm danh', value: summary.notMarked, tone: '' },
  ];
  return (
    <div className="summary-bar" aria-live="polite">
      {items.map((i) => (
        <div key={i.key} className={`chip pop-in ${i.tone}`}>
          <span className="chip__value">{i.value}</span>
          <span className="chip__label">{i.label}</span>
        </div>
      ))}
    </div>
  );
}
