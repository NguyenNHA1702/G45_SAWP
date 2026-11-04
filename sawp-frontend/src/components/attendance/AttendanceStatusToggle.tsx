import { Check, Clock, FileCheck, UserX } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { AttendanceStatus } from '../../types/attendance.types';

interface Option {
  value: AttendanceStatus;
  label: string;
  tone: 'present' | 'late' | 'excused' | 'unexcused';
  icon: LucideIcon;
}

const OPTIONS: Option[] = [
  { value: AttendanceStatus.Present, label: 'Có mặt', tone: 'present', icon: Check },
  { value: AttendanceStatus.Late, label: 'Đi muộn', tone: 'late', icon: Clock },
  { value: AttendanceStatus.ExcusedAbsence, label: 'Vắng có phép', tone: 'excused', icon: FileCheck },
  { value: AttendanceStatus.UnexcusedAbsence, label: 'Vắng K.phép', tone: 'unexcused', icon: UserX },
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
    <div
      className="status-toggle"
      role="radiogroup"
      aria-label={label}
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        background: '#F1F5F9',
        padding: 3,
        borderRadius: 'var(--radius-full)',
        border: '2px solid var(--color-fg)',
        gap: 3,
        userSelect: 'none',
      }}
    >
      {OPTIONS.map((o) => {
        const isExcused = o.value === AttendanceStatus.ExcusedAbsence;
        const off = disabled || (isExcused && !excusedAllowed);
        const Icon = o.icon;
        const isSelected = value === o.value;

        let bg = 'transparent';
        let color = '#475569';
        let border = '1.5px solid transparent';
        let shadow = 'none';

        if (isSelected) {
          border = '1.5px solid var(--color-fg)';
          shadow = '2px 2px 0 0 var(--color-fg)';
          if (o.tone === 'present') {
            bg = '#34D399';
            color = '#064E3B';
          } else if (o.tone === 'late') {
            bg = '#FBBF24';
            color = '#78350F';
          } else if (o.tone === 'excused') {
            bg = '#8B5CF6';
            color = '#FFFFFF';
          } else if (o.tone === 'unexcused') {
            bg = '#F472B6';
            color = '#FFFFFF';
          }
        }

        return (
          <button
            key={o.value}
            type="button"
            role="radio"
            aria-checked={isSelected}
            disabled={off}
            title={isExcused && !excusedAllowed ? 'Cần đơn xin nghỉ đã được duyệt trên hệ thống' : o.label}
            onClick={() => onChange(o.value)}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 5,
              padding: '5px 11px',
              borderRadius: 'var(--radius-full)',
              border,
              background: bg,
              color: isSelected ? color : '#334155',
              boxShadow: shadow,
              fontSize: 'var(--fs-xs)',
              fontWeight: isSelected ? 800 : 600,
              cursor: off ? 'not-allowed' : 'pointer',
              opacity: off ? 0.35 : 1,
              transition: 'all 0.12s cubic-bezier(0.34, 1.56, 0.64, 1)',
              transform: isSelected ? 'scale(1.02)' : 'none',
            }}
          >
            <Icon size={14} strokeWidth={isSelected ? 2.5 : 2} />
            <span>{o.label}</span>
          </button>
        );
      })}
    </div>
  );
}
