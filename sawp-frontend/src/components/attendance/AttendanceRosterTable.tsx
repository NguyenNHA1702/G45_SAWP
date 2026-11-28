import { BellRing, PencilLine, Utensils } from 'lucide-react';
import type { AttendanceSheetEntry, AttendanceStatus } from '../../types/attendance.types';
import type { DraftMap } from '../../hooks/useAttendanceSheet';
import { AttendanceStatusToggle } from './AttendanceStatusToggle';
import { LeaveBadge } from './LeaveBadge';
import { formatViDateTime } from '../../utils/date';

interface Props {
  entries: AttendanceSheetEntry[];
  draft: DraftMap;
  disabled: boolean;
  onStatusChange: (studentId: number, status: AttendanceStatus) => void;
  onRemarksChange: (studentId: number, remarks: string) => void;
  onCorrect?: (entry: AttendanceSheetEntry) => void;
  /** Office Staff only (OVR-01, UC-23 A2): shows the absence notice column. */
  onSendAbsenceNotice?: (entry: AttendanceSheetEntry) => void;
  /** Disables the send buttons, e.g. while sending or while the sheet has unsaved changes. */
  absenceNoticeDisabled?: boolean;
  /** Only today's records allow sending a new absence notice (AT-08). */
  canSendNoticeForDate?: boolean;
}

/** A notice can be sent for a saved unexcused absence not covered by an approved leave. */
const canSendAbsenceNotice = (e: AttendanceSheetEntry) =>
  e.recordId !== null && e.status === 'UnexcusedAbsence' && e.leave?.status !== 'Approved';

const initials = (name: string) => {
  const parts = name.trim().split(/\s+/);
  return (parts.at(-1)?.[0] ?? '?').toUpperCase();
};

export function AttendanceRosterTable({
  entries,
  draft,
  disabled,
  onStatusChange,
  onRemarksChange,
  onCorrect,
  onSendAbsenceNotice,
  absenceNoticeDisabled = false,
  canSendNoticeForDate = true,
}: Props) {
  const showNotice = onSendAbsenceNotice !== undefined;
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
      <div className="desktop-only-table" style={{ overflowX: 'auto' }}>
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
              {showNotice && (
                <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', minWidth: 170, fontWeight: 800 }}>Thông báo vắng</th>
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
                  <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                    {e.hasLunch ? (
                      <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                        <Utensils size={13} aria-hidden="true" /> Có
                      </span>
                    ) : e.lunchCancelledBy === 'Attendance' ? (
                      <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }} title="Đã trừ do vắng">
                        Đã trừ
                      </span>
                    ) : e.lunchCancelledBy === 'Leave' ? (
                      <span
                        className="badge"
                        style={{
                          border: '1.5px solid var(--color-fg)',
                          fontWeight: 700,
                          background: '#F1F5F9',
                          color: '#334155',
                        }}
                        title="Không ăn (suất ăn đã được hủy do đơn xin nghỉ được duyệt)"
                      >
                        Không
                      </span>
                    ) : e.lunchCancelledBy === 'Parent' || e.lunchCancelledBy === 'Staff' ? (
                      <span
                        className="badge"
                        style={{
                          border: '1.5px solid var(--color-fg)',
                          fontWeight: 700,
                          background: '#F1F5F9',
                          color: '#334155',
                        }}
                        title="Không ăn (đã hủy suất ăn bán trú)"
                      >
                        Không
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
                  {showNotice && (
                    <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                      {e.status === 'UnexcusedAbsence' && e.absenceNoticeSentAt ? (
                        <span
                          className="badge badge--mint"
                          style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}
                          title={`Đã gửi lúc ${formatViDateTime(e.absenceNoticeSentAt)}`}
                        >
                          Đã gửi · {formatViDateTime(e.absenceNoticeSentAt)}
                        </span>
                      ) : canSendAbsenceNotice(e) && canSendNoticeForDate ? (
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          onClick={() => onSendAbsenceNotice(e)}
                          disabled={absenceNoticeDisabled}
                          aria-label={`Gửi thông báo vắng mặt cho phụ huynh của ${e.fullName}`}
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
                          <BellRing size={14} aria-hidden="true" /> Gửi thông báo vắng
                        </button>
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

      {/* Mobile Card List (<= 768px) */}
      <div className="mobile-only-cards" style={{ padding: 12 }}>
        {entries.map((e, i) => {
          const row = draft[e.studentId];
          const isUnmarked = !row?.status;
          return (
            <div
              key={e.studentId}
              style={{
                border: '2px solid var(--color-fg)',
                borderLeft: isUnmarked ? '5px solid var(--color-secondary)' : '2px solid var(--color-fg)',
                borderRadius: 'var(--radius-md)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                background: '#FFFFFF',
                padding: 12,
                display: 'flex',
                flexDirection: 'column',
                gap: 10,
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
                  <span
                    aria-hidden="true"
                    style={{
                      width: 34,
                      height: 34,
                      borderRadius: '50%',
                      border: '1.5px solid var(--color-fg)',
                      background: '#EDE9FE',
                      display: 'grid',
                      placeItems: 'center',
                      fontWeight: 800,
                      fontSize: '0.8rem',
                      flexShrink: 0,
                    }}
                  >
                    {initials(e.fullName)}
                  </span>
                  <div style={{ minWidth: 0 }}>
                    <div style={{ fontWeight: 800, fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>
                      {i + 1}. {e.fullName}
                    </div>
                    <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                      {e.studentCode}
                    </div>
                  </div>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexShrink: 0, flexWrap: 'wrap', justifyContent: 'flex-end' }}>
                  {e.hasLunch ? (
                    <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700, fontSize: 11 }}>
                      <Utensils size={11} /> Có ăn
                    </span>
                  ) : (
                    <span className="badge badge--muted" style={{ fontSize: 11 }}>Không ăn</span>
                  )}
                  <LeaveBadge leave={e.leave} />
                </div>
              </div>

              <AttendanceStatusToggle
                label={`Trạng thái điểm danh của ${e.fullName}`}
                value={row?.status ?? null}
                excusedAllowed={e.leave?.status === 'Approved'}
                disabled={disabled}
                onChange={(st) => onStatusChange(e.studentId, st)}
              />

              <input
                type="text"
                className="input input--sm"
                placeholder="Ghi chú điểm danh..."
                value={row?.remarks ?? ''}
                disabled={disabled}
                onChange={(ev) => onRemarksChange(e.studentId, ev.target.value)}
                style={{ border: '1.5px solid var(--color-fg)', fontSize: 'var(--fs-xs)' }}
              />

              {(onCorrect || (showNotice && (e.absenceNoticeSentAt || (canSendAbsenceNotice(e) && canSendNoticeForDate)))) && (
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                  {onCorrect && e.recordId !== null && (
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      onClick={() => onCorrect(e)}
                      style={{ flex: 1, justifyContent: 'center', border: '1.5px solid var(--color-fg)' }}
                    >
                      <PencilLine size={13} /> Điều chỉnh
                    </button>
                  )}
                  {showNotice && e.absenceNoticeSentAt && (
                    <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontSize: 11 }}>
                      Đã gửi TB · {formatViDateTime(e.absenceNoticeSentAt)}
                    </span>
                  )}
                  {showNotice && !e.absenceNoticeSentAt && canSendAbsenceNotice(e) && canSendNoticeForDate && (
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      onClick={() => onSendAbsenceNotice(e)}
                      disabled={absenceNoticeDisabled}
                      style={{ flex: 1, justifyContent: 'center', border: '1.5px solid var(--color-fg)' }}
                    >
                      <BellRing size={13} /> Gửi thông báo vắng
                    </button>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
