import { useMemo, useState } from 'react';
import { CalendarDays, Filter } from 'lucide-react';
import type { AttendanceDay, AttendanceStatus, StudentAttendanceHistory } from '../../types/attendance.types';
import { AttendanceSummaryBar } from './AttendanceSummaryBar';
import { SYSTEM_MESSAGES } from '../../constants/messages';

const TONE: Record<AttendanceStatus, string> = {
  Present: 'var(--status-present)',
  Late: 'var(--status-late)',
  ExcusedAbsence: 'var(--status-excused)',
  UnexcusedAbsence: 'var(--status-unexcused)',
};

const SEVERITY: Record<AttendanceStatus, number> = {
  Present: 0,
  Late: 1,
  ExcusedAbsence: 2,
  UnexcusedAbsence: 3,
};

const STATUS_LABEL: Record<AttendanceStatus, string> = {
  Present: 'Có mặt',
  Late: 'Đi muộn',
  ExcusedAbsence: 'Vắng có phép',
  UnexcusedAbsence: 'Vắng K.phép',
};

const STATUS_BADGE_STYLE: Record<AttendanceStatus, { bg: string; color: string }> = {
  Present: { bg: '#D1FAE5', color: '#065F46' },
  Late: { bg: '#FEF3C7', color: '#92400E' },
  ExcusedAbsence: { bg: '#EDE9FE', color: '#5B21B6' },
  UnexcusedAbsence: { bg: '#FCE7F3', color: '#9D174D' },
};

const WEEKDAY_VI = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'] as const;

const formatViDate = (iso: string): string => {
  const [y, m, d] = iso.slice(0, 10).split('-');
  return d && m && y ? `${d}/${m}/${y}` : iso;
};

const weekdayLabel = (iso: string): string => {
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
  if (!y || !m || !d) return '';
  return WEEKDAY_VI[new Date(y, m - 1, d).getDay()];
};

function StatusChip({ status }: { status?: AttendanceStatus }) {
  if (!status) {
    return <span style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>—</span>;
  }
  const st = STATUS_BADGE_STYLE[status];
  return (
    <span
      className="badge"
      style={{
        background: st.bg,
        color: st.color,
        border: '1.5px solid var(--color-fg)',
        fontWeight: 700,
        fontSize: 'var(--fs-xs)',
      }}
    >
      {STATUS_LABEL[status]}
    </span>
  );
}

interface DayGroup {
  date: string;
  morning?: AttendanceDay;
  afternoon?: AttendanceDay;
  hasIssue: boolean;
}

interface Props {
  data: StudentAttendanceHistory;
  /** Optional YYYY-MM to render the monthly calendar grid above the daily log table. */
  month?: string;
}

export function StudentAttendanceDetailView({ data, month }: Props) {
  const [filterMode, setFilterMode] = useState<'all' | 'issues'>('all');
  const [selectedDate, setSelectedDate] = useState<string | null>(null);

  const byDate = useMemo(() => {
    const map = new Map<string, AttendanceDay[]>();
    for (const d of data.days ?? []) {
      const key = d.date.slice(0, 10);
      map.set(key, [...(map.get(key) ?? []), d]);
    }
    return map;
  }, [data.days]);

  const dayRows = useMemo<DayGroup[]>(() => {
    const entries = Array.from(byDate.entries()).sort((a, b) => b[0].localeCompare(a[0]));
    return entries.map(([date, list]) => {
      const morning = list.find((x) => x.session === 'Morning');
      const afternoon = list.find((x) => x.session === 'Afternoon');
      const hasIssue = list.some((x) => x.status !== 'Present');
      return { date, morning, afternoon, hasIssue };
    });
  }, [byDate]);

  const filteredRows = useMemo(() => {
    if (filterMode === 'issues') return dayRows.filter((r) => r.hasIssue);
    return dayRows;
  }, [dayRows, filterMode]);

  const issueCount = useMemo(() => dayRows.filter((r) => r.hasIssue).length, [dayRows]);

  let calendarGrid: React.ReactNode = null;
  if (month) {
    const [y, m] = month.split('-').map(Number);
    if (y && m) {
      const daysInMonth = new Date(y, m, 0).getDate();
      const offset = (new Date(y, m - 1, 1).getDay() + 6) % 7;
      calendarGrid = (
        <div
          className="card"
          style={{
            marginTop: 16,
            border: '2px solid var(--color-fg)',
            boxShadow: '4px 4px 0 0 var(--color-fg)',
            borderRadius: 'var(--radius-lg)',
          }}
        >
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gap: 8 }}>
            {['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'].map((d) => (
              <div
                key={d}
                style={{
                  textAlign: 'center',
                  fontWeight: 800,
                  padding: '4px 0',
                  color: 'var(--color-muted-fg)',
                  fontSize: 'var(--fs-sm)',
                }}
              >
                {d}
              </div>
            ))}
            {Array.from({ length: offset }).map((_, i) => (
              <span key={`e${i}`} />
            ))}
            {Array.from({ length: daysInMonth }).map((_, i) => {
              const iso = `${month}-${String(i + 1).padStart(2, '0')}`;
              const sessions = byDate.get(iso) ?? [];
              const rec = sessions.reduce<AttendanceDay | undefined>(
                (worst, d) => (!worst || SEVERITY[d.status] > SEVERITY[worst.status] ? d : worst),
                undefined,
              );
              const isDarkBg = rec && (rec.status === 'ExcusedAbsence' || rec.status === 'UnexcusedAbsence');
              const isSelected = selectedDate === iso;
              return (
                <button
                  key={iso}
                  type="button"
                  onClick={() => {
                    if (!rec) return;
                    setFilterMode('all');
                    setSelectedDate(iso);
                    document.getElementById(`att-row-${iso}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
                  }}
                  title={sessions
                    .map((d) => `${d.session === 'Morning' ? 'Sáng' : 'Chiều'}: ${STATUS_LABEL[d.status]}${d.remarks ? ' – ' + d.remarks : ''}`)
                    .join('\n')}
                  style={{
                    aspectRatio: '1',
                    display: 'grid',
                    placeItems: 'center',
                    fontWeight: 700,
                    fontSize: 'var(--fs-base)',
                    border: isSelected ? '3px solid var(--color-accent)' : '2px solid var(--color-fg)',
                    borderRadius: 12,
                    boxShadow: rec ? '2px 2px 0 0 var(--color-fg)' : 'none',
                    background: rec ? TONE[rec.status] : 'var(--color-muted)',
                    color: isDarkBg ? '#ffffff' : 'var(--color-fg)',
                    cursor: rec ? 'pointer' : 'default',
                  }}
                >
                  {i + 1}
                </button>
              );
            })}
          </div>

          <div
            style={{
              display: 'flex',
              flexWrap: 'wrap',
              gap: 16,
              marginTop: 20,
              paddingTop: 16,
              borderTop: '1px dashed var(--color-border)',
              fontSize: 'var(--fs-sm)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 14, height: 14, borderRadius: 4, border: '1.5px solid var(--color-fg)', background: 'var(--status-present)' }} />
              <span>Có mặt</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 14, height: 14, borderRadius: 4, border: '1.5px solid var(--color-fg)', background: 'var(--status-late)' }} />
              <span>Đi muộn</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 14, height: 14, borderRadius: 4, border: '1.5px solid var(--color-fg)', background: 'var(--status-excused)' }} />
              <span>Vắng có phép</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 14, height: 14, borderRadius: 4, border: '1.5px solid var(--color-fg)', background: 'var(--status-unexcused)' }} />
              <span>Vắng không phép</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <span style={{ width: 14, height: 14, borderRadius: 4, border: '1.5px solid var(--color-fg)', background: 'var(--color-muted)' }} />
              <span>Chưa có dữ liệu / Ngày nghỉ</span>
            </div>
          </div>
        </div>
      );
    }
  }

  return (
    <div>
      <AttendanceSummaryBar summary={data.summary} totalLabel="Tổng số buổi" />

      {data.days.length === 0 ? (
        <div className="card empty-state" style={{ marginTop: 16 }}>
          {SYSTEM_MESSAGES.MSG43.content}
        </div>
      ) : (
        <>
          {calendarGrid}

          {/* Nhật ký chi tiết: Đi ngày nào – Nghỉ ngày nào */}
          <div
            className="card"
            style={{
              marginTop: 20,
              padding: 0,
              overflow: 'hidden',
              border: '2px solid var(--color-fg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
              borderRadius: 'var(--radius-lg)',
            }}
          >
            <div
              style={{
                display: 'flex',
                flexWrap: 'wrap',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: 12,
                padding: '14px 18px',
                borderBottom: '2px solid var(--color-fg)',
                background: '#F8FAFC',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontWeight: 800, fontSize: 'var(--fs-sm)' }}>
                <CalendarDays size={18} />
                <span>Nhật ký chuyên cần từng ngày</span>
              </div>

              <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
                <button
                  type="button"
                  className={`btn btn--sm ${filterMode === 'all' ? 'btn--primary' : 'btn--outline'}`}
                  onClick={() => setFilterMode('all')}
                  style={{ borderRadius: 'var(--radius-full)' }}
                >
                  <Filter size={13} /> Tất cả ({dayRows.length} ngày)
                </button>
                <button
                  type="button"
                  className={`btn btn--sm ${filterMode === 'issues' ? 'btn--primary' : 'btn--outline'}`}
                  onClick={() => setFilterMode('issues')}
                  style={{ borderRadius: 'var(--radius-full)' }}
                >
                  Ngày nghỉ / Đi muộn ({issueCount})
                </button>
              </div>
            </div>

            {filteredRows.length === 0 ? (
              <div style={{ padding: 28, textAlign: 'center', color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
                Không có ngày nghỉ hay đi muộn nào trong khoảng thời gian này.
              </div>
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table className="table" style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                  <thead>
                    <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#FFFFFF' }}>
                      <th scope="col" style={{ padding: '10px 14px', textAlign: 'left', fontWeight: 800 }}>Ngày</th>
                      <th scope="col" style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 800 }}>Thứ</th>
                      <th scope="col" style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 800 }}>Buổi sáng</th>
                      <th scope="col" style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 800 }}>Buổi chiều</th>
                      <th scope="col" style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 800 }}>Đơn nghỉ</th>
                      <th scope="col" style={{ padding: '10px 14px', textAlign: 'left', fontWeight: 800 }}>Ghi chú</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredRows.map((r) => {
                      const leave = r.morning?.leaveRequestNumber || r.afternoon?.leaveRequestNumber;
                      const remarks = [
                        r.morning?.remarks ? `Sáng: ${r.morning.remarks}` : '',
                        r.afternoon?.remarks ? `Chiều: ${r.afternoon.remarks}` : '',
                      ]
                        .filter(Boolean)
                        .join(' · ');
                      const isHighlighted = selectedDate === r.date;
                      return (
                        <tr
                          key={r.date}
                          id={`att-row-${r.date}`}
                          style={{
                            borderBottom: '1px solid var(--color-border)',
                            background: isHighlighted ? '#EDE9FE' : r.hasIssue ? '#FFFBEB' : 'transparent',
                          }}
                        >
                          <td style={{ padding: '10px 14px', fontWeight: 700, whiteSpace: 'nowrap' }}>
                            {formatViDate(r.date)}
                          </td>
                          <td style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 600 }}>
                            {weekdayLabel(r.date)}
                          </td>
                          <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                            <StatusChip status={r.morning?.status} />
                          </td>
                          <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                            <StatusChip status={r.afternoon?.status} />
                          </td>
                          <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 600, fontSize: 'var(--fs-xs)' }}>
                            {leave || '—'}
                          </td>
                          <td style={{ padding: '10px 14px', color: '#334155', fontSize: 'var(--fs-xs)' }}>
                            {remarks || '—'}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
}
