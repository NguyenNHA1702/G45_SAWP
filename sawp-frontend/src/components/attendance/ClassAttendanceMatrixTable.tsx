import type {
  AttendanceStatus,
  ClassAttendanceMatrix,
  ClassMatrixCell,
} from '../../types/attendance.types';

const SHORT_CODE: Record<AttendanceStatus, string> = {
  Present: '✓',
  Late: 'M',
  ExcusedAbsence: 'P',
  UnexcusedAbsence: 'K',
};

const FULL_LABEL: Record<AttendanceStatus, string> = {
  Present: 'Có mặt',
  Late: 'Đi muộn',
  ExcusedAbsence: 'Vắng có phép',
  UnexcusedAbsence: 'Vắng K.phép',
};

const CELL_STYLE: Record<AttendanceStatus, { bg: string; color: string }> = {
  Present: { bg: '#D1FAE5', color: '#065F46' },
  Late: { bg: '#FEF3C7', color: '#92400E' },
  ExcusedAbsence: { bg: '#EDE9FE', color: '#5B21B6' },
  UnexcusedAbsence: { bg: '#FCE7F3', color: '#9D174D' },
};

const SEVERITY: Record<AttendanceStatus, number> = {
  Present: 0,
  Late: 1,
  ExcusedAbsence: 2,
  UnexcusedAbsence: 3,
};

const WEEKDAY_VI = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'] as const;

const fmtShortDate = (iso: string) => {
  const [, m, d] = iso.slice(0, 10).split('-');
  return d && m ? `${d}/${m}` : iso;
};

const fmtWeekday = (iso: string) => {
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
  return y && m && d ? WEEKDAY_VI[new Date(y, m - 1, d).getDay()] : '';
};

interface Props {
  matrix: ClassAttendanceMatrix;
  onSelectStudent: (studentId: number, fullName: string, studentCode: string) => void;
}

export function ClassAttendanceMatrixTable({ matrix, onSelectStudent }: Props) {
  return (
    <div>
      {/* Chú thích */}
      <div
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: 12,
          marginBottom: 14,
        }}
      >
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, fontSize: 'var(--fs-xs)', fontWeight: 700 }}>
          {(['Present', 'Late', 'ExcusedAbsence', 'UnexcusedAbsence'] as AttendanceStatus[]).map((st) => (
            <span
              key={st}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 5,
                padding: '3px 9px',
                borderRadius: 'var(--radius-full)',
                border: '1.5px solid var(--color-fg)',
                background: CELL_STYLE[st].bg,
                color: CELL_STYLE[st].color,
              }}
            >
              <strong>{SHORT_CODE[st]}</strong> {FULL_LABEL[st]}
            </span>
          ))}
          <span style={{ display: 'inline-flex', alignItems: 'center', color: 'var(--color-muted-fg)' }}>
            (Sáng/Chiều khác nhau hiển thị dạng S/C · Bấm vào tên HS để xem chi tiết)
          </span>
        </div>
      </div>

      <div
        className="card pop-in"
        style={{
          border: '2px solid var(--color-fg)',
          boxShadow: '4px 4px 0 0 var(--color-fg)',
          borderRadius: 'var(--radius-lg)',
          background: 'var(--color-card)',
          padding: 0,
          overflow: 'hidden',
        }}
      >
        <div style={{ overflowX: 'auto' }}>
          <table className="table" style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-xs)' }}>
            <thead>
              <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                <th
                  scope="col"
                  style={{
                    padding: '10px 10px',
                    textAlign: 'center',
                    width: 40,
                    fontWeight: 800,
                    position: 'sticky',
                    left: 0,
                    background: '#F8FAFC',
                    zIndex: 2,
                  }}
                >
                  #
                </th>
                <th
                  scope="col"
                  style={{
                    padding: '10px 12px',
                    textAlign: 'left',
                    minWidth: 180,
                    fontWeight: 800,
                    position: 'sticky',
                    left: 40,
                    background: '#F8FAFC',
                    borderRight: '2px solid var(--color-fg)',
                    zIndex: 2,
                  }}
                >
                  Học sinh
                </th>
                {matrix.dates.map((d) => {
                  const key = d.slice(0, 10);
                  return (
                    <th
                      key={key}
                      scope="col"
                      style={{
                        padding: '8px 6px',
                        textAlign: 'center',
                        minWidth: 44,
                        fontWeight: 800,
                        whiteSpace: 'nowrap',
                      }}
                    >
                      <div>{fmtShortDate(key)}</div>
                      <div style={{ fontSize: '0.65rem', color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                        {fmtWeekday(key)}
                      </div>
                    </th>
                  );
                })}
                <th
                  scope="col"
                  style={{
                    padding: '10px 10px',
                    textAlign: 'center',
                    fontWeight: 800,
                    borderLeft: '2px solid var(--color-fg)',
                    whiteSpace: 'nowrap',
                  }}
                >
                  Tỉ lệ
                </th>
              </tr>
            </thead>
            <tbody>
              {matrix.students.map((st, idx) => {
                const cellMap = new Map<string, ClassMatrixCell[]>();
                for (const c of st.cells) {
                  const key = c.date.slice(0, 10);
                  cellMap.set(key, [...(cellMap.get(key) ?? []), c]);
                }
                const joinedKey = st.joinedDate.slice(0, 10);
                const total = st.present + st.late + st.excusedAbsence + st.unexcusedAbsence;
                const rateDisplay =
                  total > 0 ? `${Math.round(((st.present + st.late) * 10000) / total) / 100}%` : '—';

                return (
                  <tr key={st.studentId} style={{ borderBottom: '1px solid var(--color-border)' }}>
                    <td
                      style={{
                        padding: '8px 10px',
                        textAlign: 'center',
                        fontWeight: 600,
                        position: 'sticky',
                        left: 0,
                        background: '#FFFFFF',
                        zIndex: 1,
                      }}
                    >
                      {idx + 1}
                    </td>
                    <td
                      style={{
                        padding: '8px 12px',
                        position: 'sticky',
                        left: 40,
                        background: '#FFFFFF',
                        borderRight: '2px solid var(--color-fg)',
                        zIndex: 1,
                      }}
                    >
                      <button
                        type="button"
                        onClick={() => onSelectStudent(st.studentId, st.fullName, st.studentCode)}
                        style={{
                          background: 'transparent',
                          border: 'none',
                          padding: 0,
                          textAlign: 'left',
                          cursor: 'pointer',
                        }}
                      >
                        <strong style={{ display: 'block', color: 'var(--color-accent)', textDecoration: 'underline' }}>
                          {st.fullName}
                        </strong>
                        <span style={{ fontSize: '0.7rem', color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                          {st.studentCode}
                        </span>
                      </button>
                    </td>
                    {matrix.dates.map((d) => {
                      const key = d.slice(0, 10);
                      if (key < joinedKey) {
                        return (
                          <td key={key} style={{ padding: '6px 4px', textAlign: 'center', color: '#CBD5E1' }} title="Chưa vào lớp">
                            ·
                          </td>
                        );
                      }
                      const list = cellMap.get(key) ?? [];
                      if (list.length === 0) {
                        return (
                          <td key={key} style={{ padding: '6px 4px', textAlign: 'center', color: 'var(--color-muted-fg)' }} title="Chưa điểm danh">
                            —
                          </td>
                        );
                      }
                      const morning = list.find((x) => x.session === 'Morning');
                      const afternoon = list.find((x) => x.session === 'Afternoon');
                      const worst = list.reduce((w, cur) => (SEVERITY[cur.status] > SEVERITY[w.status] ? cur : w), list[0]);
                      const text =
                        morning && afternoon && morning.status !== afternoon.status
                          ? `${SHORT_CODE[morning.status]}/${SHORT_CODE[afternoon.status]}`
                          : SHORT_CODE[worst.status];
                      const tooltip = list
                        .map(
                          (x) =>
                            `${x.session === 'Morning' ? 'Sáng' : 'Chiều'}: ${FULL_LABEL[x.status]}${
                              x.leaveRequestNumber ? ` (${x.leaveRequestNumber})` : ''
                            }${x.remarks ? ' – ' + x.remarks : ''}`,
                        )
                        .join('\n');
                      const style = CELL_STYLE[worst.status];

                      return (
                        <td key={key} style={{ padding: '5px 4px', textAlign: 'center' }} title={tooltip}>
                          <span
                            style={{
                              display: 'inline-block',
                              minWidth: 26,
                              padding: '2px 5px',
                              borderRadius: 6,
                              border: '1px solid var(--color-fg)',
                              background: style.bg,
                              color: style.color,
                              fontWeight: 800,
                            }}
                          >
                            {text}
                          </span>
                        </td>
                      );
                    })}
                    <td
                      style={{
                        padding: '8px 10px',
                        textAlign: 'center',
                        fontWeight: 800,
                        borderLeft: '2px solid var(--color-fg)',
                        whiteSpace: 'nowrap',
                      }}
                    >
                      {rateDisplay}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
