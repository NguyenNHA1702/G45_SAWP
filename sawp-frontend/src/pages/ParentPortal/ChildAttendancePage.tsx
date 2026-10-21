import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router';
import toast from 'react-hot-toast';
import { attendanceApi } from '../../services/implementations/attendanceApi';
import { asApiError } from '../../utils/apiError';
import { monthRange, toIsoDate } from '../../utils/date';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import type { AttendanceStatus, StudentAttendanceHistory } from '../../types/attendance.types';
import { AttendanceSummaryBar } from '../../components/attendance/AttendanceSummaryBar';
import { useParentChild } from '../../contexts/ParentChildContext';

const TONE: Record<AttendanceStatus, string> = {
  Present: 'var(--status-present)',
  Late: 'var(--status-late)',
  ExcusedAbsence: 'var(--status-excused)',
  UnexcusedAbsence: 'var(--status-unexcused)',
};

export default function ChildAttendancePage() {
  const { activeChildId } = useParentChild();
  const [params, setParams] = useSearchParams();
  const queryStudentId = Number(params.get('studentId'));
  const studentId = activeChildId || queryStudentId || 1;
  const month = params.get('month') ?? toIsoDate(new Date()).slice(0, 7);
  const [data, setData] = useState<StudentAttendanceHistory | null>(null);

  // Đồng bộ studentId vào URL nếu đổi con trên Header
  useEffect(() => {
    if (activeChildId && activeChildId !== queryStudentId) {
      setParams({ studentId: String(activeChildId), month });
    }
  }, [activeChildId, month, queryStudentId, setParams]);

  useEffect(() => {
    if (!studentId) return;
    const [y, m] = month.split('-').map(Number);
    const { from, to } = monthRange(y, m);
    attendanceApi
      .getStudentHistory(studentId, from, to)
      .then(setData)
      .catch((e: unknown) => toast.error(asApiError(e).message));
  }, [studentId, month]);

  const byDate = useMemo(() => new Map(data?.days.map((d) => [d.date.slice(0, 10), d]) ?? []), [data]);

  const [y, m] = month.split('-').map(Number);
  const daysInMonth = new Date(y, m, 0).getDate();
  const offset = (new Date(y, m - 1, 1).getDay() + 6) % 7;

  return (
    <main className="container">
      <div className="page-header" style={{ marginBottom: 20 }}>
        <h1>Chuyên cần của con</h1>
        {data && (
          <p style={{ color: 'var(--color-muted-fg)', marginTop: 4, fontSize: 'var(--fs-base)' }}>
            Học sinh: <strong style={{ color: 'var(--color-fg)' }}>{data.fullName}</strong> — Lớp: <strong style={{ color: 'var(--color-fg)' }}>{data.className}</strong>
          </p>
        )}
      </div>

      <div className="toolbar" style={{ marginBottom: 20 }}>
        <div className="field">
          <label className="label" htmlFor="month">
            Chọn Tháng
          </label>
          <input
            id="month"
            type="month"
            className="input"
            value={month}
            onChange={(e) => setParams({ studentId: String(studentId ?? ''), month: e.target.value })}
          />
        </div>
      </div>

      {data && <AttendanceSummaryBar summary={data.summary} />}
      {data && data.days.length === 0 && <div className="card empty-state">{SYSTEM_MESSAGES.MSG43.content}</div>}

      <div className="card" style={{ marginTop: 16 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gap: 8 }}>
          {['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'].map((d) => (
            <div key={d} style={{ textAlign: 'center', fontWeight: 800, padding: '4px 0', color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
              {d}
            </div>
          ))}
          {Array.from({ length: offset }).map((_, i) => (
            <span key={`e${i}`} />
          ))}
          {Array.from({ length: daysInMonth }).map((_, i) => {
            const iso = `${month}-${String(i + 1).padStart(2, '0')}`;
            const rec = byDate.get(iso);
            const isDarkBg = rec && (rec.status === 'ExcusedAbsence' || rec.status === 'UnexcusedAbsence');
            return (
              <div
                key={iso}
                title={rec ? `${rec.status}${rec.remarks ? ' – ' + rec.remarks : ''}` : ''}
                style={{
                  aspectRatio: '1',
                  display: 'grid',
                  placeItems: 'center',
                  fontWeight: 700,
                  fontSize: 'var(--fs-base)',
                  border: '2px solid var(--color-fg)',
                  borderRadius: 12,
                  boxShadow: rec ? '2px 2px 0 0 var(--color-fg)' : 'none',
                  background: rec ? TONE[rec.status] : 'var(--color-muted)',
                  color: isDarkBg ? '#ffffff' : 'var(--color-fg)',
                  transition: 'transform 120ms ease',
                  cursor: rec ? 'default' : 'initial',
                }}
              >
                {i + 1}
              </div>
            );
          })}
        </div>

        {/* Chú thích màu sắc */}
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 16, marginTop: 20, paddingTop: 16, borderTop: '1px dashed var(--color-border)', fontSize: 'var(--fs-sm)' }}>
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
            <span>Không có lịch / Chưa điểm danh</span>
          </div>
        </div>
      </div>
    </main>
  );
}
