import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router';
import {
  Calendar,
  Check,
  CheckCircle2,
  Clock,
  Info,
  PlusCircle,
  RefreshCw,
  ShieldAlert,
  Soup,
  Sparkles,
  Trash2,
  UtensilsCrossed,
  XCircle,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { lunchApi } from '../../services/implementations/lunchApi';
import { useParentChild } from '../../contexts/ParentChildContext';
import { GeometricModal } from '../../components/common/GeometricModal';
import { LUNCH_CUTOFF_LABEL } from '../../constants/lunch';
import { SYSTEM_MESSAGES, getSystemMessage } from '../../constants/messages';
import type { StudentLunchDay, StudentLunchSchedule } from '../../types/lunch.types';

const MEAL_TYPE_LABELS: Record<string, string> = {
  StandardLunch: 'Suất tiêu chuẩn',
  VegetarianLunch: 'Suất ăn chay',
  SpecialDiet: 'Chế độ đặc biệt',
  '1': 'Suất tiêu chuẩn',
  '2': 'Suất ăn chay',
  '3': 'Chế độ đặc biệt',
};

const SOURCE_LABELS: Record<string, string> = {
  Parent: 'Phụ huynh hủy trực tiếp (UC-30)',
  Leave: `Tự hủy từ Đơn xin nghỉ trước ${LUNCH_CUTOFF_LABEL}`,
  Attendance: 'Giảm trừ từ Điểm danh vắng phép',
  Staff: 'Nhân viên bán trú điều chỉnh',
  '1': 'Phụ huynh hủy trực tiếp (UC-30)',
  '2': `Tự hủy từ Đơn xin nghỉ trước ${LUNCH_CUTOFF_LABEL}`,
  '3': 'Giảm trừ từ Điểm danh vắng phép',
  '4': 'Nhân viên bán trú điều chỉnh',
};

function toDateKey(isoOrDate: string): string {
  return isoOrDate.slice(0, 10);
}

export default function ParentLunchPage() {
  const { children, activeChildId, selectChild } = useParentChild();
  const [params, setParams] = useSearchParams();

  const queryStudentId = Number(params.get('studentId'));
  const studentId = activeChildId || queryStudentId || (children[0]?.studentId ?? 1);

  const [month, setMonth] = useState<string>(() => {
    const fromParam = params.get('month');
    if (fromParam && /^\d{4}-\d{2}$/.test(fromParam)) return fromParam;
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
  });

  const [schedule, setSchedule] = useState<StudentLunchSchedule | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [selectedDates, setSelectedDates] = useState<string[]>([]);

  // Modal UC-29: Đăng ký suất ăn
  const [showRegisterModal, setShowRegisterModal] = useState<boolean>(false);
  const [mealType, setMealType] = useState<number>(1);
  const [studentRemarks, setStudentRemarks] = useState<string>('');
  const [submittingRegister, setSubmittingRegister] = useState<boolean>(false);

  // Modal UC-30: Hủy suất ăn
  const [cancelTargetDay, setCancelTargetDay] = useState<StudentLunchDay | null>(null);
  const [cancelReason, setCancelReason] = useState<string>('');
  const [submittingCancel, setSubmittingCancel] = useState<boolean>(false);

  // Modal Xem chi tiết ngày / thực đơn
  const [detailDay, setDetailDay] = useState<StudentLunchDay | null>(null);

  useEffect(() => {
    if (activeChildId && activeChildId !== queryStudentId) {
      setParams({ studentId: String(activeChildId), month });
    }
  }, [activeChildId, month, queryStudentId, setParams]);

  const fetchSchedule = async () => {
    if (!studentId) return;
    try {
      setLoading(true);
      const data = await lunchApi.getStudentSchedule(studentId, month);
      setSchedule(data);
      // Giữ lại những ngày đang chọn nếu vẫn còn hợp lệ để đăng ký
      setSelectedDates((prev) =>
        prev.filter((dKey) => {
          const found = data.days.find((x) => toDateKey(x.date) === dKey);
          return found && found.isServing && !found.isCutoffPassed && !found.isRegistered;
        })
      );
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { code?: string; message?: string } } })?.response?.data;
      const sysMsg = apiErr?.code ? getSystemMessage(apiErr.code) : undefined;
      toast.error(sysMsg?.content || apiErr?.message || 'Không thể tải lịch bán trú của học sinh.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchSchedule();
  }, [studentId, month]);

  const openSelectableDates = useMemo(() => {
    if (!schedule) return [];
    return schedule.days
      .filter((d) => d.isServing && !d.isCutoffPassed && !d.isRegistered)
      .map((d) => toDateKey(d.date));
  }, [schedule]);

  const toggleSelectDate = (day: StudentLunchDay) => {
    const key = toDateKey(day.date);

    if (!day.isServing) {
      toast.error(SYSTEM_MESSAGES.MSG28.content);
      return;
    }

    if (day.isRegistered) {
      if (day.isCutoffPassed) {
        toast.error(SYSTEM_MESSAGES.MSG27.content);
        return;
      }
      setCancelTargetDay(day);
      setCancelReason('');
      return;
    }

    if (day.isCutoffPassed) {
      toast.error(SYSTEM_MESSAGES.MSG27.content);
      return;
    }

    setSelectedDates((prev) =>
      prev.includes(key) ? prev.filter((x) => x !== key) : [...prev, key].sort()
    );
  };

  const handleSelectAllOpen = () => {
    if (openSelectableDates.length === 0) {
      toast.error('Không còn ngày phục vụ nào đang mở để đăng ký thêm trong tháng này.');
      return;
    }
    setSelectedDates(openSelectableDates);
  };

  const handleClearSelection = () => {
    setSelectedDates([]);
  };

  const handleConfirmRegister = async () => {
    if (!studentId || selectedDates.length === 0) {
      toast.error(SYSTEM_MESSAGES.MSG01.content);
      return;
    }

    try {
      setSubmittingRegister(true);
      const res = await lunchApi.registerBatch({
        studentId,
        dates: selectedDates,
        mealType,
        remarks: studentRemarks.trim() || undefined,
      });

      const msg = (res.code && getSystemMessage(res.code)?.content) || res.message || SYSTEM_MESSAGES.MSG25.content;
      toast.success(`${msg} (${res.data.registeredCount} ngày)`);
      setShowRegisterModal(false);
      setSelectedDates([]);
      setStudentRemarks('');
      await fetchSchedule();
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { code?: string; message?: string; errors?: string[] } } })?.response?.data;
      const sysMsg = apiErr?.code ? getSystemMessage(apiErr.code) : undefined;
      const detail = apiErr?.errors?.[0];
      toast.error(detail || sysMsg?.content || apiErr?.message || SYSTEM_MESSAGES.MSG09.content);
    } finally {
      setSubmittingRegister(false);
    }
  };

  const handleConfirmCancel = async () => {
    if (!studentId || !cancelTargetDay) return;

    try {
      setSubmittingCancel(true);
      const res = await lunchApi.cancelRegistration({
        studentId,
        date: toDateKey(cancelTargetDay.date),
        reason: cancelReason.trim() || undefined,
      });

      const msg = (res.code && getSystemMessage(res.code)?.content) || res.message || SYSTEM_MESSAGES.MSG26.content;
      toast.success(msg);
      setCancelTargetDay(null);
      setCancelReason('');
      await fetchSchedule();
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { code?: string; message?: string; errors?: string[] } } })?.response?.data;
      const sysMsg = apiErr?.code ? getSystemMessage(apiErr.code) : undefined;
      toast.error(sysMsg?.content || apiErr?.message || SYSTEM_MESSAGES.MSG27.content);
    } finally {
      setSubmittingCancel(false);
    }
  };

  const [y, m] = month.split('-').map(Number);
  const firstDayOffset = (new Date(y, m - 1, 1).getDay() + 6) % 7; // Thứ 2 = 0 ... CN = 6

  const registeredOrCancelledDays = useMemo(() => {
    if (!schedule) return [];
    return schedule.days.filter((d) => d.isRegistered || d.cancellationSource != null);
  }, [schedule]);

  return (
    <div className="container pop-in" style={{ paddingBottom: 80 }}>
      {/* Memphis Decorative Shapes */}
      <span className="decor decor--circle" style={{ top: -20, right: 30 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 40, left: 10 }} aria-hidden="true" />

      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <span className="badge badge--pink" style={{ marginBottom: 8 }}>
              <Sparkles size={14} /> Cổng Phụ Huynh — Bán Trú RP3
            </span>
            <h1 style={{ margin: '4px 0 8px' }}>
              Lịch & Đăng Ký <span className="squiggle">Bán Trú Của Con</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)' }}>
              Chọn một hoặc nhiều ngày còn mở để đăng ký suất ăn độc lập cho từng ngày (UC-29), xem thực đơn (UC-31) hoặc hủy trực tiếp trước hạn chốt (UC-30).
            </p>
          </div>

          {/* Controls: Child selector + Month picker + Refresh */}
          <div style={{ display: 'flex', alignItems: 'flex-end', gap: 12, flexWrap: 'wrap' }}>
            {children.length > 1 && (
              <div className="field" style={{ minWidth: 200 }}>
                <label className="label" htmlFor="parent-lunch-child">Học sinh</label>
                <select
                  id="parent-lunch-child"
                  className="select"
                  value={studentId}
                  onChange={(e) => {
                    const nextId = Number(e.target.value);
                    selectChild(nextId);
                    setParams({ studentId: String(nextId), month });
                  }}
                >
                  {children.map((c) => (
                    <option key={c.studentId} value={c.studentId}>
                      {c.fullName} ({c.studentCode})
                    </option>
                  ))}
                </select>
              </div>
            )}

            <div className="field" style={{ minWidth: 165 }}>
              <label className="label" htmlFor="parent-lunch-month">Tháng phục vụ</label>
              <input
                id="parent-lunch-month"
                type="month"
                className="input"
                value={month}
                onChange={(e) => {
                  setMonth(e.target.value);
                  setParams({ studentId: String(studentId), month: e.target.value });
                }}
                style={{ fontWeight: 700 }}
              />
            </div>

            <button
              type="button"
              className="btn btn--secondary"
              onClick={fetchSchedule}
              disabled={loading}
              title="Làm mới lịch bán trú"
            >
              <RefreshCw size={16} className={loading ? 'spin' : ''} />
              Làm mới
            </button>
          </div>
        </div>
      </header>

      {/* Banner Quy tắc Địa phương Tân Lập A */}
      <div
        className="card card--featured"
        style={{
          marginBottom: 28,
          padding: '16px 22px',
          background: 'var(--color-tertiary-soft)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: 16,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <div
            style={{
              width: 44,
              height: 44,
              borderRadius: '50%',
              background: 'var(--color-tertiary)',
              border: 'var(--border)',
              boxShadow: 'var(--shadow-pop-active)',
              display: 'grid',
              placeItems: 'center',
              flexShrink: 0,
            }}
          >
            <Clock size={20} color="var(--color-fg)" />
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
              <strong style={{ fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)' }}>
                Quy định Chốt suất ăn Bán trú — Trường Tiểu học Tân Lập A
              </strong>
              <span className="badge badge--amber">Mốc chốt: {LUNCH_CUTOFF_LABEL} mỗi ngày</span>
            </div>
            <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
              • Đăng ký mới (UC-29) hoặc hủy trực tiếp (UC-30) cần thực hiện trước <strong>{LUNCH_CUTOFF_LABEL}</strong> của ngày phục vụ.<br />
              • <strong>Tích hợp Đơn xin nghỉ (WFL 1.2.3)</strong>: Đơn nghỉ có bữa trưa được GVCN duyệt trước {LUNCH_CUTOFF_LABEL} (không tích &ldquo;Vẫn giữ suất ăn&rdquo;) sẽ tự động hủy suất ăn ngày đó.
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={handleSelectAllOpen}
            disabled={openSelectableDates.length === 0}
          >
            <Check size={15} /> Chọn tất cả ngày còn mở ({openSelectableDates.length})
          </button>
          {selectedDates.length > 0 && (
            <button
              type="button"
              className="btn btn--primary btn--sm"
              onClick={() => setShowRegisterModal(true)}
            >
              <PlusCircle size={15} /> Đăng ký ngay ({selectedDates.length} ngày)
            </button>
          )}
        </div>
      </div>

      {/* 4 Sticker KPI Summary Cards */}
      {schedule && (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
            gap: 20,
            marginBottom: 28,
            paddingTop: 12,
          }}
        >
          <div className="card card--hover" style={{ position: 'relative', paddingTop: 26 }}>
            <div
              style={{
                position: 'absolute',
                top: -18,
                left: 18,
                width: 38,
                height: 38,
                borderRadius: '50%',
                background: 'var(--color-tertiary)',
                border: 'var(--border)',
                boxShadow: 'var(--shadow-pop-active)',
                display: 'grid',
                placeItems: 'center',
              }}
            >
              <Calendar size={18} color="var(--color-fg)" />
            </div>
            <div className="label">Học sinh đang xem</div>
            <div style={{ fontSize: 'var(--fs-lg)', fontWeight: 800, fontFamily: 'var(--font-heading)', marginTop: 4 }}>
              {schedule.studentName}
            </div>
            <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
              Mã HS: <strong>{schedule.studentCode}</strong> — Lớp: <strong>{schedule.className}</strong>
            </p>
          </div>

          <div className="card card--hover" style={{ position: 'relative', paddingTop: 26 }}>
            <div
              style={{
                position: 'absolute',
                top: -18,
                left: 18,
                width: 38,
                height: 38,
                borderRadius: '50%',
                background: 'var(--color-accent)',
                border: 'var(--border)',
                boxShadow: 'var(--shadow-pop-active)',
                display: 'grid',
                placeItems: 'center',
              }}
            >
              <UtensilsCrossed size={18} color="#FFFFFF" />
            </div>
            <div className="label">Ngày nhà trường phục vụ</div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 800, fontFamily: 'var(--font-heading)' }}>
              {schedule.totalServingDays}{' '}
              <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>ngày</span>
            </div>
            <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
              Còn {openSelectableDates.length} ngày đang mở đăng ký
            </p>
          </div>

          <div
            className="card card--featured card--hover"
            style={{ position: 'relative', paddingTop: 26, background: 'var(--color-quaternary-soft)' }}
          >
            <div
              style={{
                position: 'absolute',
                top: -18,
                left: 18,
                width: 38,
                height: 38,
                borderRadius: '50%',
                background: 'var(--color-quaternary)',
                border: 'var(--border)',
                boxShadow: 'var(--shadow-pop-active)',
                display: 'grid',
                placeItems: 'center',
              }}
            >
              <Soup size={18} color="var(--color-fg)" />
            </div>
            <div className="label" style={{ color: '#047857' }}>Đã đăng ký còn hiệu lực</div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 900, color: '#065f46', fontFamily: 'var(--font-heading)' }}>
              {schedule.totalRegisteredDays}{' '}
              <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 700, color: '#047857' }}>suất ăn</span>
            </div>
            <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: '#047857', fontWeight: 600 }}>
              Mỗi ngày là 1 bản ghi đăng ký độc lập
            </p>
          </div>

          <div className="card card--hover" style={{ position: 'relative', paddingTop: 26 }}>
            <div
              style={{
                position: 'absolute',
                top: -18,
                left: 18,
                width: 38,
                height: 38,
                borderRadius: '50%',
                background: 'var(--color-secondary)',
                border: 'var(--border)',
                boxShadow: 'var(--shadow-pop-active)',
                display: 'grid',
                placeItems: 'center',
              }}
            >
              <XCircle size={18} color="#FFFFFF" />
            </div>
            <div className="label">Suất đã hủy trong tháng</div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 800, color: '#e11d48', fontFamily: 'var(--font-heading)' }}>
              {schedule.totalCancelledDays}{' '}
              <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>ngày</span>
            </div>
            <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
              Gồm PH hủy trực tiếp & tự hủy từ đơn nghỉ
            </p>
          </div>
        </div>
      )}

      {/* Selection Bar when dates are picked */}
      {selectedDates.length > 0 && (
        <div
          className="card"
          style={{
            marginBottom: 24,
            padding: '14px 20px',
            background: '#F5F3FF',
            borderColor: 'var(--color-accent)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: 12,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            <span className="badge badge--violet">Đang chọn {selectedDates.length} ngày</span>
            <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 600 }}>
              {selectedDates
                .slice(0, 6)
                .map((d) => new Date(d).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit' }))
                .join(', ')}
              {selectedDates.length > 6 ? ` (+${selectedDates.length - 6} ngày khác)` : ''}
            </span>
          </div>

          <div style={{ display: 'flex', gap: 10 }}>
            <button type="button" className="btn btn--secondary btn--sm" onClick={handleClearSelection}>
              Bỏ chọn
            </button>
            <button type="button" className="btn btn--primary btn--sm" onClick={() => setShowRegisterModal(true)}>
              <CheckCircle2 size={16} /> Xác nhận đăng ký ({selectedDates.length} ngày)
            </button>
          </div>
        </div>
      )}

      {/* Calendar Grid (UC-29, UC-30, UC-31) */}
      <div className="card" style={{ marginBottom: 28 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <h3 style={{ margin: 0, fontFamily: 'var(--font-heading)', fontSize: 'var(--fs-lg)', display: 'flex', alignItems: 'center', gap: 8 }}>
            <Calendar size={20} color="var(--color-accent)" />
            Lịch Phục Vụ Bán Trú Tháng {month}
          </h3>

          {/* Legend */}
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', fontSize: 'var(--fs-xs)' }}>
            <span className="badge badge--mint">Đã đăng ký</span>
            <span className="badge badge--violet">Đang chọn đăng ký</span>
            <span className="badge badge--amber">Còn mở (bấm để chọn)</span>
            <span className="badge badge--pink">Đã hủy</span>
            <span className="badge badge--muted">Nghỉ / Đã chốt {LUNCH_CUTOFF_LABEL}</span>
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, minmax(0, 1fr))', gap: 10 }}>
          {['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'CN'].map((dow) => (
            <div
              key={dow}
              style={{
                textAlign: 'center',
                fontWeight: 800,
                padding: '8px 4px',
                background: 'var(--color-muted)',
                borderRadius: 'var(--radius-sm)',
                border: '1px solid var(--color-border)',
                fontSize: 'var(--fs-xs)',
              }}
            >
              {dow}
            </div>
          ))}

          {Array.from({ length: firstDayOffset }).map((_, idx) => (
            <div key={`empty-${idx}`} />
          ))}

          {schedule?.days.map((day) => {
            const dKey = toDateKey(day.date);
            const dayNum = Number(dKey.slice(8, 10));
            const isSelected = selectedDates.includes(dKey);
            const isCancelled = !day.isRegistered && day.cancellationSource != null;
            const canSelectToRegister = day.isServing && !day.isCutoffPassed && !day.isRegistered;
            const canCancel = day.isRegistered && !day.isCutoffPassed;

            let bg = '#FFFFFF';
            let borderColor = 'var(--color-border)';
            if (!day.isServing) {
              bg = '#F8FAFC';
            } else if (isSelected) {
              bg = '#EDE9FE';
              borderColor = 'var(--color-accent)';
            } else if (day.isRegistered) {
              bg = 'var(--color-quaternary-soft)';
              borderColor = '#10B981';
            } else if (isCancelled) {
              bg = 'var(--color-secondary-soft)';
              borderColor = 'var(--color-secondary)';
            } else if (!day.isCutoffPassed) {
              bg = '#FFFBEB';
              borderColor = '#F59E0B';
            }

            return (
              <div
                key={dKey}
                onClick={() => toggleSelectDate(day)}
                style={{
                  minHeight: 118,
                  padding: '10px',
                  borderRadius: 'var(--radius-sm)',
                  border: `2px solid ${borderColor}`,
                  background: bg,
                  boxShadow: isSelected || day.isRegistered ? 'var(--shadow-pop-active)' : 'none',
                  cursor: day.isServing ? 'pointer' : 'not-allowed',
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'space-between',
                  transition: 'transform 0.15s ease',
                  opacity: !day.isServing ? 0.65 : 1,
                }}
              >
                {/* Top row: Date number + Cutoff/Check */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 800, fontSize: 'var(--fs-base)', fontFamily: 'var(--font-heading)' }}>
                    {dayNum}
                  </span>

                  {day.isServing && (
                    <span style={{ fontSize: '10px', color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                      Chốt {day.cutoffTime}
                    </span>
                  )}
                </div>

                {/* Middle: Status badge + Menu snippet */}
                <div style={{ margin: '6px 0' }}>
                  {!day.isServing ? (
                    <span className="badge badge--muted" style={{ fontSize: '10px' }}>Không phục vụ</span>
                  ) : isSelected ? (
                    <span className="badge badge--violet" style={{ fontSize: '10px' }}>
                      <Check size={11} /> Đã chọn đăng ký
                    </span>
                  ) : day.isRegistered ? (
                    <span className="badge badge--mint" style={{ fontSize: '10px' }}>
                      ✓ Đã đăng ký ăn
                    </span>
                  ) : isCancelled ? (
                    <span className="badge badge--pink" style={{ fontSize: '10px' }}>
                      {day.cancellationSource === 'Leave' || day.cancellationSource === 2
                        ? 'Đã hủy (Đơn nghỉ)'
                        : 'Đã hủy suất'}
                    </span>
                  ) : day.isCutoffPassed ? (
                    <span className="badge badge--muted" style={{ fontSize: '10px' }}>Đã khóa sổ</span>
                  ) : (
                    <span className="badge badge--amber" style={{ fontSize: '10px' }}>+ Bấm để chọn</span>
                  )}

                  {day.isServing && day.serviceRemarks && (
                    <p
                      style={{
                        margin: '5px 0 0',
                        fontSize: '11px',
                        color: 'var(--color-muted-fg)',
                        display: '-webkit-box',
                        WebkitLineClamp: 2,
                        WebkitBoxOrient: 'vertical',
                        overflow: 'hidden',
                        lineHeight: 1.3,
                      }}
                      title={day.serviceRemarks}
                    >
                      🍴 {day.serviceRemarks}
                    </p>
                  )}
                </div>

                {/* Bottom row: Action button or info */}
                <div
                  style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 4, marginTop: 4 }}
                  onClick={(e) => e.stopPropagation()}
                >
                  {day.isServing && (
                    <button
                      type="button"
                      onClick={() => setDetailDay(day)}
                      style={{
                        background: 'transparent',
                        border: 'none',
                        padding: 0,
                        fontSize: '11px',
                        color: 'var(--color-accent)',
                        fontWeight: 700,
                        cursor: 'pointer',
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: 3,
                      }}
                    >
                      <Info size={12} /> Thực đơn
                    </button>
                  )}

                  {canCancel && (
                    <button
                      type="button"
                      onClick={() => {
                        setCancelTargetDay(day);
                        setCancelReason('');
                      }}
                      style={{
                        background: '#FFE4E6',
                        border: '1px solid #F43F5E',
                        borderRadius: 999,
                        padding: '2px 8px',
                        fontSize: '10px',
                        fontWeight: 700,
                        color: '#E11D48',
                        cursor: 'pointer',
                      }}
                    >
                      Hủy suất
                    </button>
                  )}

                  {canSelectToRegister && !isSelected && (
                    <span style={{ fontSize: '10px', color: '#B45309', fontWeight: 700 }}>Còn hạn</span>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Detailed Registration & Cancellation History Table (UC-31 & UC-30) */}
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <h3 style={{ margin: 0, fontFamily: 'var(--font-heading)', fontSize: 'var(--fs-md)', display: 'flex', alignItems: 'center', gap: 8 }}>
            <Soup size={18} color="var(--color-accent)" />
            Danh sách các ngày Đã đăng ký & Biến động trong tháng ({registeredOrCancelledDays.length} ngày)
          </h3>
        </div>

        {registeredOrCancelledDays.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '28px 16px', color: 'var(--color-muted-fg)' }}>
            Chưa có bản ghi đăng ký bán trú nào trong tháng {month}. Phụ huynh hãy tích chọn các ngày còn mở trên lịch để đăng ký cho con.
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
              <thead>
                <tr style={{ borderBottom: 'var(--border)', background: 'var(--color-muted)' }}>
                  <th style={{ padding: '10px 12px', textAlign: 'left' }}>Ngày phục vụ</th>
                  <th style={{ padding: '10px 12px', textAlign: 'left' }}>Loại suất</th>
                  <th style={{ padding: '10px 12px', textAlign: 'left' }}>Thực đơn / Ghi chú ngày</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Hạn chốt</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Trạng thái</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Thao tác (UC-30)</th>
                </tr>
              </thead>
              <tbody>
                {registeredOrCancelledDays.map((day) => {
                  const dKey = toDateKey(day.date);
                  const canCancel = day.isRegistered && !day.isCutoffPassed;
                  return (
                    <tr key={dKey} style={{ borderBottom: '1px solid var(--color-border)' }}>
                      <td style={{ padding: '12px', fontWeight: 700 }}>
                        {new Date(day.date).toLocaleDateString('vi-VN', {
                          weekday: 'short',
                          day: '2-digit',
                          month: '2-digit',
                          year: 'numeric',
                        })}
                      </td>
                      <td style={{ padding: '12px' }}>
                        {day.mealType ? MEAL_TYPE_LABELS[String(day.mealType)] ?? 'Suất tiêu chuẩn' : 'Suất tiêu chuẩn'}
                      </td>
                      <td style={{ padding: '12px', color: 'var(--color-muted-fg)', maxWidth: 320 }}>
                        <div>{day.serviceRemarks || 'Thực đơn tiêu chuẩn nhà bếp'}</div>
                        {day.cancellationReason && (
                          <div style={{ fontSize: 'var(--fs-xs)', color: '#E11D48', marginTop: 2 }}>
                            Lý do hủy: {day.cancellationReason}
                          </div>
                        )}
                      </td>
                      <td style={{ padding: '12px', textAlign: 'center', fontWeight: 600 }}>
                        {day.cutoffTime}
                      </td>
                      <td style={{ padding: '12px', textAlign: 'center' }}>
                        {day.isRegistered ? (
                          <span className="badge badge--mint">Còn hiệu lực</span>
                        ) : (
                          <span className="badge badge--pink">
                            {SOURCE_LABELS[String(day.cancellationSource)] ?? 'Đã hủy'}
                          </span>
                        )}
                      </td>
                      <td style={{ padding: '12px', textAlign: 'right' }}>
                        {canCancel ? (
                          <button
                            type="button"
                            className="btn btn--secondary btn--sm"
                            onClick={() => {
                              setCancelTargetDay(day);
                              setCancelReason('');
                            }}
                          >
                            <Trash2 size={14} /> Hủy đăng ký
                          </button>
                        ) : day.isRegistered ? (
                          <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                            Đã qua hạn {day.cutoffTime}
                          </span>
                        ) : !day.isCutoffPassed && day.isServing ? (
                          <button
                            type="button"
                            className="btn btn--primary btn--sm"
                            onClick={() => {
                              setSelectedDates([dKey]);
                              setShowRegisterModal(true);
                            }}
                          >
                            Đăng ký lại
                          </button>
                        ) : (
                          <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* MODAL UC-29: Xác nhận Đăng ký suất ăn bán trú */}
      <GeometricModal
        open={showRegisterModal}
        onClose={() => setShowRegisterModal(false)}
        title="Xác nhận Đăng ký Bán trú (UC-29)"
        badgeText="Tạo đăng ký độc lập từng ngày"
        badgeColor="mint"
        icon={<UtensilsCrossed size={22} color="var(--color-fg)" />}
        iconBg="var(--color-quaternary)"
        maxWidth="md"
      >
        <div>
          <p style={{ marginTop: 0, fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
            {SYSTEM_MESSAGES.MSG45.content} Hệ thống sẽ tạo <strong>{selectedDates.length} bản ghi đăng ký độc lập</strong> cho học sinh{' '}
            <strong>{schedule?.studentName}</strong>:
          </p>

          <div
            className="card"
            style={{
              background: 'var(--color-muted)',
              padding: '12px 14px',
              marginBottom: 16,
              maxHeight: 130,
              overflowY: 'auto',
              display: 'flex',
              flexWrap: 'wrap',
              gap: 8,
            }}
          >
            {selectedDates.map((d) => (
              <span key={d} className="badge badge--violet">
                {new Date(d).toLocaleDateString('vi-VN')}
              </span>
            ))}
          </div>

          <div className="field" style={{ marginBottom: 14 }}>
            <label className="label" htmlFor="register-meal-type">Chế độ suất ăn</label>
            <select
              id="register-meal-type"
              className="select"
              value={mealType}
              onChange={(e) => setMealType(Number(e.target.value))}
            >
              <option value={1}>Suất tiêu chuẩn (Mặn)</option>
              <option value={2}>Suất ăn chay</option>
              <option value={3}>Chế độ ăn đặc biệt (Dị ứng / Kiêng)</option>
            </select>
          </div>

          <div className="field" style={{ marginBottom: 20 }}>
            <label className="label" htmlFor="register-remarks">Ghi chú cho bộ phận bán trú (nếu có)</label>
            <input
              id="register-remarks"
              type="text"
              className="input"
              placeholder="VD: Bé dị ứng tôm/đậu phộng, nhờ cô lưu ý..."
              value={studentRemarks}
              onChange={(e) => setStudentRemarks(e.target.value)}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowRegisterModal(false)}
              disabled={submittingRegister}
            >
              Đóng
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleConfirmRegister}
              disabled={submittingRegister}
            >
              <CheckCircle2 size={16} />
              {submittingRegister ? 'Đang xử lý...' : `Xác nhận đăng ký (${selectedDates.length} ngày)`}
            </button>
          </div>
        </div>
      </GeometricModal>

      {/* MODAL UC-30: Xác nhận Hủy suất ăn trực tiếp */}
      <GeometricModal
        open={Boolean(cancelTargetDay)}
        onClose={() => setCancelTargetDay(null)}
        title="Hủy đăng ký suất ăn bán trú (UC-30)"
        badgeText={`Trước giờ chốt ${cancelTargetDay?.cutoffTime ?? LUNCH_CUTOFF_LABEL}`}
        badgeColor="pink"
        icon={<ShieldAlert size={22} color="#FFFFFF" />}
        iconBg="var(--color-secondary)"
        maxWidth="md"
      >
        {cancelTargetDay && (
          <div>
            <p style={{ marginTop: 0, fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>
              {SYSTEM_MESSAGES.MSG45.content} Bạn đang yêu cầu hủy suất ăn ngày{' '}
              <strong>{new Date(cancelTargetDay.date).toLocaleDateString('vi-VN')}</strong> của bé{' '}
              <strong>{schedule?.studentName}</strong>.
            </p>

            <div className="field" style={{ marginBottom: 20 }}>
              <label className="label" htmlFor="cancel-lunch-reason">Lý do hủy suất ăn (tùy chọn)</label>
              <input
                id="cancel-lunch-reason"
                type="text"
                className="input"
                placeholder="VD: Gia đình đón bé về ăn trưa tại nhà..."
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => setCancelTargetDay(null)}
                disabled={submittingCancel}
              >
                Giữ lại suất ăn
              </button>
              <button
                type="button"
                className="btn btn--primary"
                style={{ background: '#E11D48' }}
                onClick={handleConfirmCancel}
                disabled={submittingCancel}
              >
                <Trash2 size={16} />
                {submittingCancel ? 'Đang hủy...' : 'Xác nhận hủy suất ăn'}
              </button>
            </div>
          </div>
        )}
      </GeometricModal>

      {/* MODAL Chi tiết Ngày phục vụ & Thực đơn */}
      <GeometricModal
        open={Boolean(detailDay)}
        onClose={() => setDetailDay(null)}
        title={`Thông tin Bán trú ngày ${detailDay ? new Date(detailDay.date).toLocaleDateString('vi-VN') : ''}`}
        badgeText="Chi tiết ngày phục vụ"
        badgeColor="amber"
        icon={<Soup size={22} color="var(--color-fg)" />}
        iconBg="var(--color-tertiary)"
        maxWidth="sm"
      >
        {detailDay && (
          <div>
            <div className="card" style={{ background: 'var(--color-muted)', padding: '14px', marginBottom: 16 }}>
              <div style={{ marginBottom: 8 }}>
                <span className="label">Trạng thái phục vụ:</span>{' '}
                <strong>{detailDay.isServing ? 'Có phục vụ ăn bán trú' : 'Không phục vụ'}</strong>
              </div>
              <div style={{ marginBottom: 8 }}>
                <span className="label">Hạn đăng ký / hủy (UC-32):</span>{' '}
                <strong>{detailDay.cutoffTime} sáng</strong>
              </div>
              <div>
                <span className="label">Thực đơn / Ghi chú:</span>
                <p style={{ margin: '4px 0 0', fontWeight: 600 }}>
                  {detailDay.serviceRemarks || 'Thực đơn tiêu chuẩn theo tuần của nhà bếp.'}
                </p>
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
              <button type="button" className="btn btn--secondary" onClick={() => setDetailDay(null)}>
                Đóng
              </button>
            </div>
          </div>
        )}
      </GeometricModal>
    </div>
  );
}
