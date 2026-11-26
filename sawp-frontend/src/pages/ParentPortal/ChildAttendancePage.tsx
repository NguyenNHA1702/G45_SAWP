import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import toast from 'react-hot-toast';
import { attendanceApi } from '../../services/implementations/attendanceApi';
import { asApiError } from '../../utils/apiError';
import { monthRange, toIsoDate } from '../../utils/date';
import type { StudentAttendanceHistory } from '../../types/attendance.types';
import { useParentChild } from '../../contexts/ParentChildContext';
import { StudentAttendanceDetailView } from '../../components/attendance/StudentAttendanceDetailView';

export default function ChildAttendancePage() {
  const { activeChild, activeChildId } = useParentChild();
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

  return (
    <main className="container">
      <div className="page-header" style={{ marginBottom: 20 }}>
        <h1>Chuyên cần của con</h1>
        {data && (
          <p style={{ color: 'var(--color-muted-fg)', marginTop: 4, fontSize: 'var(--fs-base)' }}>
            Học sinh: <strong style={{ color: 'var(--color-fg)' }}>{data.fullName}</strong> — Lớp: <strong style={{ color: 'var(--color-fg)' }}>{data.className}</strong>
            {activeChild?.homeroomTeacherName && (
              <>
                {' '}— GVCN: <strong style={{ color: 'var(--color-fg)' }}>{activeChild.homeroomTeacherName}</strong>
              </>
            )}
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

      {data && <StudentAttendanceDetailView data={data} month={month} />}
    </main>
  );
}
