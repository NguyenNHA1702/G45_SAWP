import { Check, Clock, FileCheck, UserX } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { AttendanceStatus } from '../../types/attendance.types';

interface Option {
  value: AttendanceStatus;
  label: string;
  tone: string;
  icon: LucideIcon;
}

const OPTIONS: Option[] = [
  { value: AttendanceStatus.Present, label: 'Có mặt', tone: 'present', icon: Check },
  { value: AttendanceStatus.Late, label: 'Muộn', tone: 'late', icon: Clock },
  { value: AttendanceStatus.ExcusedAbsence, label: 'Vắng CP', tone: 'excused', icon: FileCheck },
  { value: AttendanceStatus.UnexcusedAbsence, label: 'Vắng KP', tone: 'unexcused', icon: UserX },
];

interface Props {
  label: string;
  value: AttendanceStatus | null;
  excusedAllowed: boolean;
  disabled?: boolean;
  onChange: (value: AttendanceStatus) => void;
}

export function AttendanceStatusToggle({ label, value, excusedAllowed, disabled = false, onChange }: Props) {
  return (
    <div className="status-toggle" role="radiogroup" aria-label={label}>
      {OPTIONS.map((o) => {
        const isExcused = o.value === AttendanceStatus.ExcusedAbsence;
        const off = disabled || (isExcused && !excusedAllowed);
        const Icon = o.icon;
        return (
          <button
            key={o.value}
            type="button"
            role="radio"
            aria-checked={value === o.value}
            className={`status-toggle__btn status-toggle__btn--${o.tone}`}
            disabled={off}
            title={isExcused && !excusedAllowed ? 'Cần đơn xin nghỉ đã được duyệt' : o.label}
            onClick={() => onChange(o.value)}
          >
            <Icon size={16} aria-hidden="true" />
            <span>{o.label}</span>
          </button>
        );
      })}
    </div>
  );
}
