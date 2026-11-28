import { MessageSquareText, PencilLine, Utensils } from 'lucide-react';
import type { AttendanceSheetEntry, AttendanceStatus } from '../../types/attendance.types';
import type { DraftMap } from '../../hooks/useAttendanceSheet';
import { AttendanceStatusToggle } from './AttendanceStatusToggle';
import { LeaveBadge } from './LeaveBadge';
import { ATTENDANCE_STATUS, LEAVE_REQUEST_STATUS, LUNCH_CANCELLATION_SOURCE } from '../../constants/attendance';

interface Props {
  entries: AttendanceSheetEntry[];
  draft: DraftMap;
  disabled: boolean;
  onStatusChange: (studentId: number, status: AttendanceStatus) => void;
  onRemarksChange: (studentId: number, remarks: string) => void;
  onCorrect?: (entry: AttendanceSheetEntry) => void;
  /** School staff (OVR-10, UC-23 A1): shows the family contact column and opens the contact notes of a record. */
  onOpenContactNotes?: (entry: AttendanceSheetEntry) => void;
  /** Disables the contact note buttons, e.g. while the sheet has unsaved changes. */
  contactNotesDisabled?: boolean;
}

/** A saved unexcused absence, or a record that still has contact notes as history (BR-62). */
const hasContactColumn = (e: AttendanceSheetEntry) =>
  e.recordId !== null && (e.status === ATTENDANCE_STATUS.UnexcusedAbsence || (e.contactNotes?.length ?? 0) > 0);

const notesInEffect = (e: AttendanceSheetEntry) => (e.contactNotes ?? []).filter((n) => !n.isSuperseded).length;

function renderContactStatus(e: AttendanceSheetEntry) {
  const count = notesInEffect(e);
  if (count > 0) {
    return (
      <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
        Đã liên hệ ({count})
      </span>
    );
  }
  if (e.status === ATTENDANCE_STATUS.UnexcusedAbsence) {
    return (
      <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
        Chưa có ghi chú
      </span>
    );
  }
  return null;
}

const initials = (name: string) => {
  const parts = name.trim().split(/\s+/);
  return (parts.at(-1)?.[0] ?? '?').toUpperCase();
};

function renderLunchBadge(e: AttendanceSheetEntry) {
  if (e.hasLunch) {
    return (
      <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
        <Utensils size={13} aria-hidden="true" /> Có
      </span>
    );
  }
  if (e.lunchCancelledBy === LUNCH_CANCELLATION_SOURCE.Attendance) {
    return (
      <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }} title="Đã trừ do vắng">
        Đã trừ
      </span>
    );
  }
  if (e.lunchCancelledBy === LUNCH_CANCELLATION_SOURCE.Leave || e.lunchCancelledBy === LUNCH_CANCELLATION_SOURCE.Parent || e.lunchCancelledBy === LUNCH_CANCELLATION_SOURCE.Staff) {
    return (
      <span
        className="badge"
        style={{
          border: '1.5px solid var(--color-fg)',
          fontWeight: 700,
          background: '#F1F5F9',
          color: '#334155',
        }}
        title={
          e.lunchCancelledBy === LUNCH_CANCELLATION_SOURCE.Leave
            ? 'Không ăn (suất ăn đã được hủy do đơn xin nghỉ được duyệt)'
            : 'Không ăn (đã hủy suất ăn bán trú)'
        }
      >
        Không
      </span>
    );
  }
  return <span style={{ color: 'var(--color-muted-fg)' }}>—</span>;
}

export function AttendanceRosterTable({
  entries,
  draft,
  disabled,
  onStatusChange,
  onRemarksChange,
  onCorrect,
  onOpenContactNotes,
  contactNotesDisabled = false,
}: Props) {
  const showContact = onOpenContactNotes !== undefined;
  return (
    <>
      <div
        className="card roster-card roster-desktop-wrap pop-in"
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
              {onCorrect && (
                <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', width: 120, fontWeight: 800 }}>Điều chỉnh</th>
              )}
              {showContact && (
                <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', minWidth: 170, fontWeight: 800 }}>Liên hệ gia đình</th>
              )}
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
                  <td style={{ padding: '12px 14px', textAlign: 'center' }}>{renderLunchBadge(e)}</td>
                  <td style={{ padding: '10px 16px', textAlign: 'center' }}>
                    <AttendanceStatusToggle
                      label={`Trạng thái của ${e.fullName}`}
                      value={row?.status ?? null}
                      excusedAllowed={e.leave?.status === LEAVE_REQUEST_STATUS.Approved}
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
                  {onCorrect && (
                    <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                      {e.recordId != null ? (
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          onClick={() => onCorrect(e)}
                          style={{
                            borderRadius: 'var(--radius-full)',
                            border: '1.5px solid var(--color-fg)',
                            boxShadow: '2px 2px 0 0 var(--color-fg)',
                            fontWeight: 700,
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 5,
                          }}
                        >
                          <PencilLine size={13} />
                          Sửa
                        </button>
                      ) : (
                        <span style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>—</span>
                      )}
                    </td>
                  )}
                  {showContact && (
                    <td style={{ padding: '10px 14px', textAlign: 'center' }} data-testid={`contact-cell-${e.studentId}`}>
                      {hasContactColumn(e) ? (
                        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
                          {renderContactStatus(e)}
                          <button
                            type="button"
                            className="btn btn--secondary btn--sm"
                            onClick={() => onOpenContactNotes(e)}
                            disabled={contactNotesDisabled}
                            aria-label={`Ghi chú liên hệ của ${e.fullName}`}
                            style={{
                              borderRadius: 'var(--radius-full)',
                              border: '1.5px solid var(--color-fg)',
                              fontWeight: 700,
                              fontSize: 'var(--fs-xs)',
                              padding: '6px 12px',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 6,
                            }}
                          >
                            <MessageSquareText size={14} aria-hidden="true" /> Ghi chú liên hệ
                          </button>
                        </div>
                      ) : (
                        <span style={{ color: 'var(--color-muted-fg)' }}>—</span>
                      )}
                    </td>
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>

    {/* Mobile Card List (<= 768px) */}
    <div className="roster-mobile-list">
      {entries.map((e, i) => {
        const row = draft[e.studentId];
        const unmarked = !row?.status;
        return (
          <div
            key={e.studentId}
            className={`card roster-mobile-card ${unmarked ? 'roster-mobile-card--unmarked' : ''}`}
          >
            <div className="roster-mobile-card__head">
              <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0, flex: 1 }}>
                <span className="roster-mobile-card__stt">{i + 1}</span>
                <span className="roster-mobile-card__avatar" aria-hidden="true">
                  {initials(e.fullName)}
                </span>
                <div style={{ minWidth: 0 }}>
                  <strong style={{ display: 'block', fontSize: 'var(--fs-sm)', color: 'var(--color-fg)', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {e.fullName}
                  </strong>
                  <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                    {e.studentCode}
                  </span>
                </div>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                <LeaveBadge leave={e.leave} />
                {renderLunchBadge(e)}
              </div>
            </div>

            <AttendanceStatusToggle
              label={`Trạng thái của ${e.fullName}`}
              value={row?.status ?? null}
              excusedAllowed={e.leave?.status === LEAVE_REQUEST_STATUS.Approved}
              disabled={disabled}
              onChange={(s) => onStatusChange(e.studentId, s)}
            />

            <div className="roster-mobile-card__foot">
              <input
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
                  flex: 1,
                }}
              />
              {onCorrect && e.recordId != null && (
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  onClick={() => onCorrect(e)}
                  style={{ flexShrink: 0 }}
                >
                  <PencilLine size={13} /> Sửa
                </button>
              )}
              {showContact && hasContactColumn(e) && (
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  onClick={() => onOpenContactNotes(e)}
                  disabled={contactNotesDisabled}
                  aria-label={`Ghi chú liên hệ của ${e.fullName}`}
                  style={{ flexShrink: 0 }}
                >
                  <MessageSquareText size={13} /> Ghi chú liên hệ
                </button>
              )}
            </div>
          </div>
        );
      })}
    </div>
    </>
  );
}
