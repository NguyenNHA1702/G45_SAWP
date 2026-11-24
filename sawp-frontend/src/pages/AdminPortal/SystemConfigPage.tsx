import { useCallback, useEffect, useState } from 'react';
import { CalendarDays, Clock, Save, Settings } from 'lucide-react';
import toast from 'react-hot-toast';
import { getAuthUser } from '../../services/apiClient';
import { adminApi } from '../../services/implementations/adminApi';
import type { LunchCutoffDayDto, SchoolCalendarDayDto } from '../../types/admin.types';
import { getSystemMessage } from '../../constants/messages';

export default function SystemConfigPage() {
  const isAdmin = (() => {
    try {
      const user = JSON.parse(getAuthUser() ?? '{}');
      const rawRoles = user?.roles ?? (user?.role ? [user.role] : []);
      const roles: string[] = Array.isArray(rawRoles) ? rawRoles : [String(rawRoles)];
      return roles.some((r) => typeof r === 'string' && r.toLowerCase() === 'admin');
    } catch {
      return false;
    }
  })();

  const [activeTab, setActiveTab] = useState<'calendar' | 'lunch'>('calendar');
  const [month, setMonth] = useState<string>(() => new Date().toISOString().slice(0, 7));

  // Calendar tab
  const [calendarDays, setCalendarDays] = useState<SchoolCalendarDayDto[]>([]);
  const [calDate, setCalDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [calIsSchoolDay, setCalIsSchoolDay] = useState<boolean>(true);
  const [calNote, setCalNote] = useState<string>('');

  // Lunch cutoff tab
  const [lunchDays, setLunchDays] = useState<LunchCutoffDayDto[]>([]);
  const [lunchDate, setLunchDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [lunchServing, setLunchServing] = useState<boolean>(true);
  const [lunchCutoff, setLunchCutoff] = useState<string>('08:15');
  const [lunchRemarks, setLunchRemarks] = useState<string>('');

  const [loading, setLoading] = useState(false);

  const loadTab = useCallback(async () => {
    setLoading(true);
    try {
      if (activeTab === 'calendar') {
        const res = await adminApi.getCalendarDays(month);
        if (res.success && res.data) setCalendarDays(res.data);
      } else {
        const res = await adminApi.getLunchCutoffDays(month);
        if (res.success && res.data) setLunchDays(res.data);
      }
    } catch {
      toast.error(getSystemMessage('MSG50')?.content || 'Không thể tải dữ liệu cấu hình.');
    } finally {
      setLoading(false);
    }
  }, [activeTab, month]);

  useEffect(() => {
    loadTab();
  }, [loadTab]);

  const handleSaveCalendarDay = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const res = await adminApi.upsertCalendarDay({
        date: calDate,
        isSchoolDay: calIsSchoolDay,
        note: calNote || undefined,
      });
      if (res.success) {
        toast.success(getSystemMessage('MSG44')?.content || 'Đã lưu cấu hình hệ thống.');
        loadTab();
      } else {
        toast.error(res.message || 'Lỗi khi lưu lịch ngày học.');
      }
    } catch {
      toast.error(getSystemMessage('MSG50')?.content || 'Đã xảy ra lỗi hệ thống.');
    }
  };

  const handleSaveLunchCutoff = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const res = await adminApi.upsertLunchCutoff({
        date: lunchDate,
        isServing: lunchServing,
        cutoffTime: lunchCutoff,
        remarks: lunchRemarks || undefined,
      });
      if (res.success) {
        toast.success(getSystemMessage('MSG44')?.content || 'Đã lưu cấu hình hệ thống.');
        loadTab();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Lỗi khi lưu cấu hình bán trú.');
      }
    } catch {
      toast.error(getSystemMessage('MSG01')?.content || 'Vui lòng kiểm tra dữ liệu nhập.');
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
        <div>
          <span className="badge badge--amber" style={{ marginBottom: 8 }}>
            <Settings size={13} /> Cấu hình hệ thống (UC-58 – UC-60)
          </span>
          <h1 style={{ margin: '4px 0 4px' }}>Lịch năm học & Giờ chốt bán trú</h1>
          <p style={{ margin: 0, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
            Thiết lập ngày học / ngày nghỉ lễ và mốc giờ chốt suất ăn theo ngày.
          </p>
        </div>

        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>Tháng:</label>
          <input type="month" className="input" value={month} onChange={(e) => setMonth(e.target.value)} />
        </div>
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: 10 }}>
        <button
          type="button"
          className={activeTab === 'calendar' ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
          onClick={() => setActiveTab('calendar')}
        >
          <CalendarDays size={15} /> Lịch ngày học / Ngày nghỉ
        </button>
        {isAdmin && (
          <button
            type="button"
            className={activeTab === 'lunch' ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
            onClick={() => setActiveTab('lunch')}
          >
            <Clock size={15} /> Giờ chốt bán trú (Cutoff)
          </button>
        )}
      </div>

      {activeTab === 'calendar' ? (
        <div style={{ display: 'grid', gridTemplateColumns: 'minmax(280px, 360px) 1fr', gap: 20, alignItems: 'start' }}>
          <form onSubmit={handleSaveCalendarDay} className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <h3 style={{ margin: 0 }}>Cập nhật ngày học</h3>
            <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
              Ngày áp dụng
              <input type="date" className="input" style={{ width: '100%', marginTop: 4 }} value={calDate} onChange={(e) => setCalDate(e.target.value)} />
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 'var(--fs-sm)', fontWeight: 700, cursor: 'pointer' }}>
              <input type="checkbox" checked={calIsSchoolDay} onChange={(e) => setCalIsSchoolDay(e.target.checked)} />
              Là ngày học chính thức (bỏ tích = ngày nghỉ)
            </label>
            <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
              Ghi chú
              <input className="input" style={{ width: '100%', marginTop: 4 }} placeholder="VD: Nghỉ lễ Quốc khánh..." value={calNote} onChange={(e) => setCalNote(e.target.value)} />
            </label>
            <button type="submit" className="btn btn--primary btn--sm">
              <Save size={15} /> Lưu cấu hình
            </button>
          </form>

          <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
            {loading ? (
              <div style={{ padding: 30, textAlign: 'center' }}>Đang tải lịch năm học...</div>
            ) : calendarDays.length === 0 ? (
              <div style={{ padding: 30, textAlign: 'center', fontWeight: 700 }}>
                {getSystemMessage('MSG43')?.content || 'Không có dữ liệu phù hợp để hiển thị.'}
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                <thead>
                  <tr style={{ background: 'var(--color-muted)', borderBottom: 'var(--border)' }}>
                    <th style={{ padding: '10px 14px', textAlign: 'left', fontWeight: 800 }}>Ngày</th>
                    <th style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 800 }}>Trạng thái</th>
                    <th style={{ padding: '10px 14px', textAlign: 'left', fontWeight: 800 }}>Ghi chú</th>
                  </tr>
                </thead>
                <tbody>
                  {calendarDays.map((d) => (
                    <tr
                      key={d.id}
                      style={{ borderBottom: '1px solid var(--color-border)', cursor: 'pointer' }}
                      onClick={() => {
                        setCalDate(d.date.slice(0, 10));
                        setCalIsSchoolDay(d.isSchoolDay);
                        setCalNote(d.note || '');
                      }}
                    >
                      <td style={{ padding: '10px 14px', fontWeight: 700 }}>{new Date(d.date).toLocaleDateString('vi-VN')}</td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                        <span className={d.isSchoolDay ? 'badge badge--mint' : 'badge badge--pink'}>
                          {d.isSchoolDay ? 'Ngày học' : 'Ngày nghỉ'}
                        </span>
                      </td>
                      <td style={{ padding: '10px 14px' }}>{d.note || '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'minmax(280px, 360px) 1fr', gap: 20, alignItems: 'start' }}>
          <form onSubmit={handleSaveLunchCutoff} className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <h3 style={{ margin: 0 }}>Cập nhật giờ chốt bán trú</h3>
            <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
              Ngày phục vụ
              <input type="date" className="input" style={{ width: '100%', marginTop: 4 }} value={lunchDate} onChange={(e) => setLunchDate(e.target.value)} />
            </label>
            <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
              Giờ chốt suất (HH:mm)
              <input type="time" className="input" style={{ width: '100%', marginTop: 4 }} value={lunchCutoff} onChange={(e) => setLunchCutoff(e.target.value)} />
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 'var(--fs-sm)', fontWeight: 700, cursor: 'pointer' }}>
              <input type="checkbox" checked={lunchServing} onChange={(e) => setLunchServing(e.target.checked)} />
              Có phục vụ ăn trưa
            </label>
            <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
              Ghi chú
              <input className="input" style={{ width: '100%', marginTop: 4 }} placeholder="Ghi chú vận hành..." value={lunchRemarks} onChange={(e) => setLunchRemarks(e.target.value)} />
            </label>
            <button type="submit" className="btn btn--primary btn--sm">
              <Save size={15} /> Lưu cấu hình
            </button>
          </form>

          <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
            {loading ? (
              <div style={{ padding: 30, textAlign: 'center' }}>Đang tải danh sách ngày bán trú...</div>
            ) : lunchDays.length === 0 ? (
              <div style={{ padding: 30, textAlign: 'center', fontWeight: 700 }}>
                {getSystemMessage('MSG43')?.content || 'Không có dữ liệu phù hợp để hiển thị.'}
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                <thead>
                  <tr style={{ background: 'var(--color-muted)', borderBottom: 'var(--border)' }}>
                    <th style={{ padding: '10px 14px', textAlign: 'left', fontWeight: 800 }}>Ngày</th>
                    <th style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 800 }}>Phục vụ</th>
                    <th style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 800 }}>Giờ chốt</th>
                    <th style={{ padding: '10px 14px', textAlign: 'left', fontWeight: 800 }}>Ghi chú</th>
                  </tr>
                </thead>
                <tbody>
                  {lunchDays.map((d) => (
                    <tr
                      key={d.id}
                      style={{ borderBottom: '1px solid var(--color-border)', cursor: 'pointer' }}
                      onClick={() => {
                        setLunchDate(d.date.slice(0, 10));
                        setLunchServing(d.isServing);
                        setLunchCutoff(d.cutoffTime);
                        setLunchRemarks(d.remarks || '');
                      }}
                    >
                      <td style={{ padding: '10px 14px', fontWeight: 700 }}>{new Date(d.date).toLocaleDateString('vi-VN')}</td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                        <span className={d.isServing ? 'badge badge--mint' : 'badge badge--pink'}>
                          {d.isServing ? 'Có phục vụ' : 'Nghỉ phục vụ'}
                        </span>
                      </td>
                      <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 700 }}>{d.cutoffTime}</td>
                      <td style={{ padding: '10px 14px' }}>{d.remarks || '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
