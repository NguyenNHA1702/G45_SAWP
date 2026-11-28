import { useCallback, useEffect, useState } from 'react';
import { Bell, BookOpen, CalendarDays, Clock, Save, Settings, ShieldCheck } from 'lucide-react';
import toast from 'react-hot-toast';
import { getAuthUser } from '../../services/apiClient';
import { adminApi } from '../../services/implementations/adminApi';
import { asApiError } from '../../utils/apiError';
import type { LunchCutoffDayDto, SchoolCalendarDayDto } from '../../types/admin.types';
import { getSystemMessage } from '../../constants/messages';
import { DateInput } from '../../components/common/DateInput';
import { NotificationTemplatesTab, PermissionsMatrixTab, ReferenceItemsTab } from '../../components/admin/AdminMetadataTabs';
import { formatDateDDMMYYYY, todayIso } from '../../utils/date';

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

  const [activeTab, setActiveTab] = useState<'calendar' | 'lunch' | 'permissions' | 'references' | 'templates'>('calendar');
  const [month, setMonth] = useState<string>(() => todayIso().slice(0, 7));

  // Calendar tab
  const [calendarDays, setCalendarDays] = useState<SchoolCalendarDayDto[]>([]);
  const [calStartDate, setCalStartDate] = useState<string>(() => todayIso());
  const [calEndDate, setCalEndDate] = useState<string>(() => todayIso());
  const [calIsSchoolDay, setCalIsSchoolDay] = useState<boolean>(true);
  const [calNote, setCalNote] = useState<string>('');
  const [savingCalendar, setSavingCalendar] = useState(false);

  // Lunch cutoff tab
  const [lunchDays, setLunchDays] = useState<LunchCutoffDayDto[]>([]);
  const [lunchStartDate, setLunchStartDate] = useState<string>(() => todayIso());
  const [lunchEndDate, setLunchEndDate] = useState<string>(() => todayIso());
  const [lunchServing, setLunchServing] = useState<boolean>(true);
  const [lunchCutoff, setLunchCutoff] = useState<string>('08:15');
  const [lunchRemarks, setLunchRemarks] = useState<string>('');
  const [savingLunch, setSavingLunch] = useState(false);

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

  const buildDateRange = (startStr: string, endStr: string): string[] => {
    const start = new Date(`${startStr}T00:00:00Z`);
    const end = new Date(`${endStr}T00:00:00Z`);
    const dates: string[] = [];
    const cur = new Date(start);
    while (cur <= end) {
      dates.push(cur.toISOString().slice(0, 10));
      cur.setUTCDate(cur.getUTCDate() + 1);
    }
    return dates;
  };

  const saveDateRange = async (
    startDate: string,
    endDate: string,
    setSaving: (v: boolean) => void,
    saveOne: (date: string) => Promise<{ success: boolean; message?: string | null; errors?: string[] | null }>,
    fallbackError: string
  ) => {
    if (!startDate || !endDate) {
      toast.error('Vui lòng chọn đầy đủ Từ ngày và Đến ngày.');
      return;
    }
    if (endDate < startDate) {
      toast.error('Đến ngày phải lớn hơn hoặc bằng Từ ngày.');
      return;
    }

    const dates = buildDateRange(startDate, endDate);
    if (dates.length > 90) {
      toast.error('Chỉ hỗ trợ cập nhật tối đa 90 ngày mỗi lần.');
      return;
    }

    setSaving(true);
    try {
      let successCount = 0;
      let firstError: string | undefined;

      for (const date of dates) {
        const res = await saveOne(date);
        if (res.success) {
          successCount += 1;
        } else if (!firstError) {
          firstError = res.errors?.[0] || res.message || `Lỗi khi lưu ngày ${date}.`;
        }
      }

      if (successCount === dates.length) {
        toast.success(
          dates.length === 1
            ? getSystemMessage('MSG44')?.content || 'Đã lưu cấu hình hệ thống.'
            : `Đã cập nhật cấu hình cho ${successCount} ngày.`
        );
        loadTab();
      } else if (successCount > 0) {
        toast.error(`Đã lưu ${successCount}/${dates.length} ngày. ${firstError ?? ''}`);
        loadTab();
      } else {
        toast.error(firstError || fallbackError);
      }
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || fallbackError);
    } finally {
      setSaving(false);
    }
  };

  const handleSaveCalendarDay = async (e: React.FormEvent) => {
    e.preventDefault();
    await saveDateRange(
      calStartDate,
      calEndDate,
      setSavingCalendar,
      (date) =>
        adminApi.upsertCalendarDay({
          date,
          isSchoolDay: calIsSchoolDay,
          note: calNote || undefined,
        }),
      'Lỗi khi lưu lịch ngày học.'
    );
  };

  const handleSaveLunchCutoff = async (e: React.FormEvent) => {
    e.preventDefault();
    await saveDateRange(
      lunchStartDate,
      lunchEndDate,
      setSavingLunch,
      (date) =>
        adminApi.upsertLunchCutoff({
          date,
          isServing: lunchServing,
          cutoffTime: lunchCutoff,
          remarks: lunchRemarks || undefined,
        }),
      'Lỗi khi lưu cấu hình bán trú.'
    );
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      <div className="card" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
        <div style={{ minWidth: 0, flex: '1 1 320px' }}>
          <span className="badge badge--amber" style={{ marginBottom: 8 }}>
            <Settings size={13} /> {isAdmin ? 'Cấu hình hệ thống (UC-54, UC-56 – UC-60)' : 'Cấu hình lịch năm học (UC-58 – UC-60)'}
          </span>
          <h1 style={{ margin: '4px 0 4px' }}>
            {activeTab === 'permissions'
              ? 'Phân quyền chi tiết theo vai trò'
              : activeTab === 'references'
              ? 'Danh mục tham chiếu hệ thống'
              : activeTab === 'templates'
              ? 'Mẫu thông báo hệ thống'
              : 'Lịch năm học & Giờ chốt bán trú'}
          </h1>
          <p style={{ margin: 0, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
            {activeTab === 'calendar' || activeTab === 'lunch'
              ? 'Thiết lập ngày học / ngày nghỉ lễ và mốc giờ chốt suất ăn theo ngày.'
              : 'Quản trị phân quyền RBAC, danh mục tham chiếu dropdown và mẫu thông báo toàn hệ thống.'}
          </p>
        </div>

        {(activeTab === 'calendar' || activeTab === 'lunch') && (
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexShrink: 0 }}>
            <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700, whiteSpace: 'nowrap' }}>Tháng:</label>
            <input type="month" className="input" style={{ minWidth: 160 }} value={month} onChange={(e) => setMonth(e.target.value)} />
          </div>
        )}
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
        <button
          type="button"
          className={activeTab === 'calendar' ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
          onClick={() => setActiveTab('calendar')}
        >
          <CalendarDays size={15} /> Lịch ngày học / Ngày nghỉ
        </button>
        {isAdmin && (
          <>
            <button
              type="button"
              className={activeTab === 'lunch' ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
              onClick={() => setActiveTab('lunch')}
            >
              <Clock size={15} /> Giờ chốt bán trú (Cutoff)
            </button>
            <button
              type="button"
              className={activeTab === 'permissions' ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
              onClick={() => setActiveTab('permissions')}
            >
              <ShieldCheck size={15} /> Phân quyền vai trò (UC-54)
            </button>
            <button
              type="button"
              className={activeTab === 'references' ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
              onClick={() => setActiveTab('references')}
            >
              <BookOpen size={15} /> Danh mục tham chiếu (UC-56)
            </button>
            <button
              type="button"
              className={activeTab === 'templates' ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
              onClick={() => setActiveTab('templates')}
            >
              <Bell size={15} /> Mẫu thông báo (UC-57)
            </button>
          </>
        )}
      </div>

      {activeTab === 'permissions' && isAdmin ? (
        <PermissionsMatrixTab />
      ) : activeTab === 'references' && isAdmin ? (
        <ReferenceItemsTab />
      ) : activeTab === 'templates' && isAdmin ? (
        <NotificationTemplatesTab />
      ) : activeTab === 'calendar' ? (
        <div className="responsive-split">
          <form onSubmit={handleSaveCalendarDay} className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <h3 style={{ margin: 0 }}>Cập nhật lịch ngày học / nghỉ</h3>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 10 }}>
              <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
                Từ ngày (dd-mm-yyyy)
                <DateInput
                  style={{ width: '100%', marginTop: 4 }}
                  value={calStartDate}
                  ariaLabel="Từ ngày"
                  onChange={(val) => {
                    setCalStartDate(val);
                    if (calEndDate < val) setCalEndDate(val);
                  }}
                />
              </label>
              <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
                Đến ngày (dd-mm-yyyy)
                <DateInput
                  style={{ width: '100%', marginTop: 4 }}
                  min={calStartDate}
                  value={calEndDate}
                  ariaLabel="Đến ngày"
                  onChange={setCalEndDate}
                />
              </label>
            </div>
            {calStartDate && calEndDate && calEndDate >= calStartDate && (
              <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                Áp dụng cho {buildDateRange(calStartDate, calEndDate).length} ngày
              </div>
            )}
            <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 'var(--fs-sm)', fontWeight: 700, cursor: 'pointer' }}>
              <input type="checkbox" checked={calIsSchoolDay} onChange={(e) => setCalIsSchoolDay(e.target.checked)} />
              Là ngày học chính thức (bỏ tích = ngày nghỉ)
            </label>
            <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
              Ghi chú
              <input className="input" style={{ width: '100%', marginTop: 4 }} placeholder="VD: Nghỉ lễ Quốc khánh..." value={calNote} onChange={(e) => setCalNote(e.target.value)} />
            </label>
            <button type="submit" className="btn btn--primary btn--sm" disabled={savingCalendar}>
              <Save size={15} /> {savingCalendar ? 'Đang lưu...' : 'Lưu cấu hình'}
            </button>
          </form>

          <div className="card" style={{ padding: 0, overflowX: 'auto', WebkitOverflowScrolling: 'touch' }}>
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
                        const dateStr = d.date.slice(0, 10);
                        setCalStartDate(dateStr);
                        setCalEndDate(dateStr);
                        setCalIsSchoolDay(d.isSchoolDay);
                        setCalNote(d.note || '');
                      }}
                    >
                      <td style={{ padding: '10px 14px', fontWeight: 700 }}>{formatDateDDMMYYYY(d.date)}</td>
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
        <div className="responsive-split">
          <form onSubmit={handleSaveLunchCutoff} className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <h3 style={{ margin: 0 }}>Cập nhật giờ chốt bán trú</h3>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 10 }}>
              <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
                Từ ngày (dd-mm-yyyy)
                <DateInput
                  style={{ width: '100%', marginTop: 4 }}
                  value={lunchStartDate}
                  ariaLabel="Từ ngày bán trú"
                  onChange={(val) => {
                    setLunchStartDate(val);
                    if (lunchEndDate < val) setLunchEndDate(val);
                  }}
                />
              </label>
              <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
                Đến ngày (dd-mm-yyyy)
                <DateInput
                  style={{ width: '100%', marginTop: 4 }}
                  min={lunchStartDate}
                  value={lunchEndDate}
                  ariaLabel="Đến ngày bán trú"
                  onChange={setLunchEndDate}
                />
              </label>
            </div>
            {lunchStartDate && lunchEndDate && lunchEndDate >= lunchStartDate && (
              <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                Áp dụng cho {buildDateRange(lunchStartDate, lunchEndDate).length} ngày
              </div>
            )}
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
            <button type="submit" className="btn btn--primary btn--sm" disabled={savingLunch}>
              <Save size={15} /> {savingLunch ? 'Đang lưu...' : 'Lưu cấu hình'}
            </button>
          </form>

          <div className="card" style={{ padding: 0, overflowX: 'auto', WebkitOverflowScrolling: 'touch' }}>
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
                        const dateStr = d.date.slice(0, 10);
                        setLunchStartDate(dateStr);
                        setLunchEndDate(dateStr);
                        setLunchServing(d.isServing);
                        setLunchCutoff(d.cutoffTime);
                        setLunchRemarks(d.remarks || '');
                      }}
                    >
                      <td style={{ padding: '10px 14px', fontWeight: 700 }}>{formatDateDDMMYYYY(d.date)}</td>
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
