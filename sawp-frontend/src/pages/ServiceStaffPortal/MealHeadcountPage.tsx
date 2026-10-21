import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { lunchApi } from '../../services/implementations/lunchApi';
import { asApiError } from '../../utils/apiError';
import { todayIso } from '../../utils/date';
import type { MealHeadcount } from '../../types/lunch.types';

export default function MealHeadcountPage() {
  const [date, setDate] = useState(todayIso());
  const [data, setData] = useState<MealHeadcount | null>(null);

  const load = useCallback(() => {
    lunchApi
      .getHeadcount(date)
      .then(setData)
      .catch((e: unknown) => toast.error(asApiError(e).message));
  }, [date]);

  useEffect(() => {
    load();
    const id = window.setInterval(load, 60_000); // Tự làm mới mỗi 60s
    return () => window.clearInterval(id);
  }, [load]);

  return (
    <main className="container">
      <h1>Suất ăn trong ngày</h1>
      <div className="toolbar" style={{ marginBottom: 20 }}>
        <div className="field">
          <label className="label" htmlFor="hc-date">
            Ngày
          </label>
          <input id="hc-date" type="date" className="input" value={date} onChange={(e) => setDate(e.target.value)} />
        </div>
        {data && (
          <span className={`badge ${data.isFinalized ? 'badge--mint' : 'badge--amber'}`}>
            {data.isFinalized ? 'Đã chốt' : `Tạm tính – chốt lúc ${data.finalizeTime.slice(0, 5)}`}
          </span>
        )}
      </div>
      {data && !data.isServiceDay && <div className="banner banner--info">Không phục vụ ăn trưa ngày này.</div>}
      {data?.isServiceDay && (
        <>
          <div className="summary-bar" style={{ marginBottom: 20 }}>
            <div className="chip chip--present">
              <span className="chip__value">{data.totalMeals}</span>
              <span className="chip__label">Tổng suất</span>
            </div>
            <div className="chip">
              <span className="chip__value">{data.standardLunch}</span>
              <span className="chip__label">Thường</span>
            </div>
            <div className="chip">
              <span className="chip__value">{data.vegetarianLunch}</span>
              <span className="chip__label">Chay</span>
            </div>
            <div className="chip">
              <span className="chip__value">{data.specialDiet}</span>
              <span className="chip__label">Đặc biệt</span>
            </div>
            <div className="chip chip--unexcused">
              <span className="chip__value">{data.deductedByAttendance}</span>
              <span className="chip__label">Trừ do vắng</span>
            </div>
          </div>
          <div className="table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>Lớp</th>
                  <th>Suất</th>
                  <th>Trừ do vắng</th>
                  <th>Điểm danh</th>
                </tr>
              </thead>
              <tbody>
                {data.classes.map((c) => (
                  <tr key={c.classId}>
                    <td>{c.className}</td>
                    <td>
                      <strong>{c.meals}</strong>
                    </td>
                    <td>{c.deductedByAttendance}</td>
                    <td>
                      {c.attendanceSubmitted ? (
                        <span className="badge badge--mint">Đã điểm danh</span>
                      ) : (
                        <span className="badge badge--amber">Chưa – số tạm tính</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </main>
  );
}
