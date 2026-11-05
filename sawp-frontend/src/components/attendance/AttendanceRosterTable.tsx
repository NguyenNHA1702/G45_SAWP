import { Utensils } from 'lucide-react';
import type { AttendanceSheetEntry, AttendanceStatus } from '../../types/attendance.types';
import type { DraftMap } from '../../hooks/useAttendanceSheet';
import { AttendanceStatusToggle } from './AttendanceStatusToggle';
import { LeaveBadge } from './LeaveBadge';

interface Props {
  entries: AttendanceSheetEntry[];
  draft: DraftMap;
  disabled: boolean;
  onStatusChange: (studentId: number, status: AttendanceStatus) => void;
  onRemarksChange: (studentId: number, remarks: string) => void;
}

const initials = (name: string) => {
  const parts = name.trim().split(/\s+/);
  return (parts.at(-1)?.[0] ?? '?').toUpperCase();
};

export function AttendanceRosterTable({ entries, draft, disabled, onStatusChange, onRemarksChange }: Props) {
  return (
    <div
      className="card roster-card pop-in"
      style={{
        border: '2px solid var(--color-fg)',
        boxShadow: '4px 4px 0 0 var(--color-fg)',
        borderRadius: 'var(--radius-lg)',
        background: 'var(--color-card)',
        padding: 0,
        overflow: 'hidden',
        marginBottom: 24,
      }}
    >
      <div style={{ overflowX: 'auto' }}>
        <table className="table roster-table" style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
          <thead>
            <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
              <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', width: 48, fontWeight: 800 }}>#</th>
              <th scope="col" style={{ padding: '12px 16px', textAlign: 'left', minWidth: 200, fontWeight: 800 }}>Học sinh</th>
              <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', width: 120, fontWeight: 800 }}>Đơn nghỉ</th>
              <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', width: 100, fontWeight: 800 }}>Ăn trưa</th>
              <th scope="col" style={{ padding: '12px 16px', textAlign: 'center', minWidth: 380, fontWeight: 800 }}>Trạng thái buổi học</th>
              <th scope="col" style={{ padding: '12px 16px', textAlign: 'left', minWidth: 180, fontWeight: 800 }}>Ghi chú</th>
            </tr>
          </thead>
          <tbody>
            {entries.map((e, i) => {
              const row = draft[e.studentId];
              return (
                <tr
                  key={e.studentId}
                  className={row?.status ? '' : 'is-unmarked'}
                  style={{
                    borderBottom: '1px solid var(--color-border)',
                    transition: 'background 0.15s ease',
                  }}
                  onMouseEnter={(ev) => (ev.currentTarget.style.background = '#FFFDF5')}
                  onMouseLeave={(ev) => (ev.currentTarget.style.background = 'transparent')}
                >
                  <td style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600 }}>{i + 1}</td>
                  <td style={{ padding: '12px 16px' }}>
                    <div className="student-cell" style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                      <span
                        className="avatar"
                        aria-hidden="true"
                        style={{
                          width: 36,
                          height: 36,
                          borderRadius: '50%',
                          border: '1.5px solid var(--color-fg)',
                          boxShadow: '1.5px 1.5px 0 0 var(--color-fg)',
                          display: 'grid',
                          placeItems: 'center',
                          fontWeight: 800,
                          fontSize: '0.85rem',
                          background: '#EDE9FE',
                          flexShrink: 0,
                        }}
                      >
                        {initials(e.fullName)}
                      </span>
                      <div>
                        <strong style={{ display: 'block', fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>{e.fullName}</strong>
                        <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 600 }}>{e.studentCode}</span>
                      </div>
                    </div>
                  </td>
                  <td style={{ padding: '12px 14px', textAlign: 'center' }}><LeaveBadge leave={e.leave} /></td>
                  <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                    {e.hasLunch ? (
                      <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                        <Utensils size={13} aria-hidden="true" /> Có
                      </span>
                    ) : e.lunchCancelledBy === 'Attendance' ? (
                      <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }} title="Đã trừ do vắng">
                        Đã trừ
                      </span>
                    ) : (
                      <span style={{ color: 'var(--color-muted-fg)' }}>—</span>
                    )}
                  </td>
                  <td style={{ padding: '10px 16px', textAlign: 'center' }}>
                    <AttendanceStatusToggle
                      label={`Trạng thái của ${e.fullName}`}
                      value={row?.status ?? null}
                      excusedAllowed={e.leave?.status === 'Approved'}
                      disabled={disabled}
                      onChange={(s) => onStatusChange(e.studentId, s)}
                    />
                  </td>
                  <td style={{ padding: '10px 16px' }}>
                    <input
                      id={`remarks-${e.studentId}`}
                      className="input input--sm"
                      maxLength={500}
                      placeholder="Ghi chú thêm…"
                      value={row?.remarks ?? ''}
                      disabled={disabled}
                      onChange={(ev) => onRemarksChange(e.studentId, ev.target.value)}
                      aria-label={`Ghi chú cho ${e.fullName}`}
                      style={{
                        borderRadius: 'var(--radius-sm)',
                        border: '1.5px solid var(--color-fg)',
                        fontSize: 'var(--fs-xs)',
                        padding: '6px 10px',
                        width: '100%',
                      }}
                    />
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
