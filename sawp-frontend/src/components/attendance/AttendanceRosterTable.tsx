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
    <div className="table-wrap">
      <table className="table roster-table">
        <thead>
          <tr>
            <th scope="col">#</th>
            <th scope="col">Học sinh</th>
            <th scope="col">Đơn nghỉ</th>
            <th scope="col">Ăn trưa</th>
            <th scope="col">Trạng thái</th>
            <th scope="col">Ghi chú</th>
          </tr>
        </thead>
        <tbody>
          {entries.map((e, i) => {
            const row = draft[e.studentId];
            return (
              <tr key={e.studentId} className={row?.status ? '' : 'is-unmarked'}>
                <td>{i + 1}</td>
                <td>
                  <div className="student-cell">
                    <span className="avatar" aria-hidden="true">{initials(e.fullName)}</span>
                    <div>
                      <strong>{e.fullName}</strong>
                      <small>{e.studentCode}</small>
                    </div>
                  </div>
                </td>
                <td><LeaveBadge leave={e.leave} /></td>
                <td>
                  {e.hasLunch ? (
                    <span className="badge badge--mint"><Utensils size={14} aria-hidden="true" /> Có</span>
                  ) : e.lunchCancelledBy === 'Attendance' ? (
                    <span className="badge badge--pink" title="Đã trừ do vắng">Đã trừ</span>
                  ) : (
                    <span className="badge badge--muted">—</span>
                  )}
                </td>
                <td>
                  <AttendanceStatusToggle
                    label={`Trạng thái của ${e.fullName}`}
                    value={row?.status ?? null}
                    excusedAllowed={e.leave?.status === 'Approved'}
                    disabled={disabled}
                    onChange={(s) => onStatusChange(e.studentId, s)}
                  />
                </td>
                <td>
                  <input
                    id={`remarks-${e.studentId}`}
                    className="input input--sm"
                    maxLength={500}
                    placeholder="Ghi chú…"
                    value={row?.remarks ?? ''}
                    disabled={disabled}
                    onChange={(ev) => onRemarksChange(e.studentId, ev.target.value)}
                    aria-label={`Ghi chú cho ${e.fullName}`}
                  />
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
