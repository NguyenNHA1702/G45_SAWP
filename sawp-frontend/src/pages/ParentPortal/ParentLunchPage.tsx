import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router';
import {
  Calendar,
  Check,
  CheckCircle2,
  Clock,
  GraduationCap,
  Info,
  PlusCircle,
  RefreshCw,
  ShieldAlert,
  Soup,
  Sparkles,
  Trash2,
  Users,
  UtensilsCrossed,
  XCircle,
  AlertTriangle,
  Edit3,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { lunchApi } from '../../services/implementations/lunchApi';
import { useParentChild } from '../../contexts/ParentChildContext';
import { GeometricModal } from '../../components/common/GeometricModal';
import { LUNCH_CUTOFF_LABEL, formatDateDDMMYYYY, formatWeekdayDateDDMMYYYY } from '../../constants/lunch';
import { ALLERGEN_OPTIONS, getAllergenShortLabel, normalizeMealTypeNumber } from '../../constants/allergens';
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
  Parent: 'Phụ huynh hủy trực tiếp',
  Leave: `Tự hủy từ Đơn xin nghỉ trước ${LUNCH_CUTOFF_LABEL}`,
  Attendance: 'Giảm trừ từ Điểm danh vắng phép',
  Staff: 'Nhân viên bán trú điều chỉnh',
  '1': 'Phụ huynh hủy trực tiếp',
  '2': `Tự hủy từ Đơn xin nghỉ trước ${LUNCH_CUTOFF_LABEL}`,
  '3': 'Giảm trừ từ Điểm danh vắng phép',
  '4': 'Nhân viên bán trú điều chỉnh',
};

function toDateKey(isoOrDate: string): string {
  return isoOrDate.slice(0, 10);
}

function cleanText(text?: string | null): string {
  if (!text) return '';
  return text
    .replace(/Thá»±c Ä‘Æ¡n/g, 'Thực đơn')
    .replace(/CÆ¡m sÆ°á»\s*n/g, 'Cơm sườn')
    .replace(/Trá»©ng/g, 'Trứng')
    .replace(/cuá»™n/g, 'cuộn')
    .replace(/ngÅ© sáº¯c/g, 'ngũ sắc')
    .replace(/bÃ\s*sÆ°á»\s*n/g, 'bí sườn')
    .replace(/Chuá»‘i tiÃªu/g, 'Chuối tiêu');
}

export default function ParentLunchPage() {
  const { children, activeChildId, selectChild } = useParentChild();
  const [params, setParams] = useSearchParams();

  const queryStudentId = Number(params.get('studentId'));
  const queryMonth = params.get('month');
  const studentId = (queryStudentId && children.some((c) => c.studentId === queryStudentId) ? queryStudentId : activeChildId) || (children[0]?.studentId ?? 1);

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
  const [studentRemarks, setStudentRemarks] = useState<string>('');
  const [submittingRegister, setSubmittingRegister] = useState<boolean>(false);

  // Modal UC-30: Hủy suất ăn
  const [cancelTargetDay, setCancelTargetDay] = useState<StudentLunchDay | null>(null);
  const [cancelReason, setCancelReason] = useState<string>('');
  const [submittingCancel, setSubmittingCancel] = useState<boolean>(false);

  // Modal Chi tiết ngày / thực đơn
  const [detailDay, setDetailDay] = useState<StudentLunchDay | null>(null);

  // Modal Khai báo dị ứng thực phẩm & lưu ý sức khỏe của con
  const [showProfileModal, setShowProfileModal] = useState<boolean>(false);
  const [profileTags, setProfileTags] = useState<string[]>([]);
  const [profileNotes, setProfileNotes] = useState<string>('');
  const [savingProfile, setSavingProfile] = useState<boolean>(false);

  const openProfileModal = () => {
    const p = schedule?.mealProfile;
    setProfileTags(p?.allergenTags ?? []);
    setProfileNotes(p?.allergyNotes ?? '');
    setShowProfileModal(true);
  };

  const handleSaveProfile = async () => {
    if (!studentId) return;
    try {
      setSavingProfile(true);
      const res = await lunchApi.upsertStudentMealProfile(studentId, {
        defaultMealType: normalizeMealTypeNumber(schedule?.mealProfile?.defaultMealType),
        allergenTags: profileTags,
        allergyNotes: profileNotes.trim() || undefined,
      });
      toast.success(res.message || 'Đã lưu khai báo dị ứng thực phẩm của con.');
      setShowProfileModal(false);
      await fetchSchedule();
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { message?: string; errors?: string[] } } })?.response?.data;
      toast.error(apiErr?.errors?.[0] || apiErr?.message || 'Lưu khai báo dị ứng thất bại.');
    } finally {
      setSavingProfile(false);
    }
  };

  useEffect(() => {
    if (queryStudentId && queryStudentId !== activeChildId && children.some((c) => c.studentId === queryStudentId)) {
      selectChild(queryStudentId);
    } else if (activeChildId && !queryStudentId) {
      setParams({ studentId: String(activeChildId), month });
    }
  }, [activeChildId, children, month, queryStudentId, selectChild, setParams]);

  useEffect(() => {
    if (queryMonth && /^\d{4}-\d{2}$/.test(queryMonth) && queryMonth !== month) {
      setMonth(queryMonth);
    }
  }, [queryMonth, month]);

  const fetchSchedule = async () => {
    if (!studentId) return;
    try {
      setLoading(true);
      const data = await lunchApi.getStudentSchedule(studentId, month);
      setSchedule(data);
      // Giữ lại những ngày đang chọn nếu vẫn còn hợp lệ để đăng ký (BD-02: loại bỏ ngày đang có đơn nghỉ đã duyệt)
      setSelectedDates((prev) =>
        prev.filter((dKey) => {
          const found = data.days.find((x) => toDateKey(x.date) === dKey);
          return found && found.isServing && !found.isCutoffPassed && !found.isRegistered && !found.hasApprovedLeave;
        })
      );
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { code?: string; message?: string; errors?: string[] } } })?.response?.data;
      const sysMsg = apiErr?.code ? getSystemMessage(apiErr.code) : undefined;
      toast.error(apiErr?.errors?.[0] || sysMsg?.content || apiErr?.message || 'Không thể tải lịch bán trú của học sinh.');
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
      .filter((d) => d.isServing && !d.isCutoffPassed && !d.isRegistered && !d.hasApprovedLeave)
      .map((d) => toDateKey(d.date));
  }, [schedule]);

  // Mở modal đăng ký mới (giữ trống ô dặn dò theo ngày để tách bạch với hồ sơ dị ứng đã khai báo)
  const openRegisterModal = (overrideDates?: string[]) => {
    if (overrideDates) {
      setSelectedDates(overrideDates);
    }
    setStudentRemarks('');
    setShowRegisterModal(true);
  };

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

    if (day.hasApprovedLeave) {
      toast.error('Ngày này học sinh đang có đơn xin nghỉ đã được phê duyệt. Vui lòng thu hồi đơn xin nghỉ trước khi đăng ký lại suất ăn.');
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
    if (studentRemarks.trim().length > 250) {
      toast.error('Ghi chú suất ăn không được vượt quá 250 ký tự.');
      return;
    }

    try {
      setSubmittingRegister(true);
      const res = await lunchApi.registerBatch({
        studentId,
        dates: selectedDates,
        mealType: normalizeMealTypeNumber(schedule?.mealProfile?.defaultMealType),
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
      toast.error(apiErr?.errors?.[0] || sysMsg?.content || apiErr?.message || SYSTEM_MESSAGES.MSG27.content);
    } finally {
      setSubmittingCancel(false);
    }
  };

  const [y, m] = month.split('-').map(Number);
  const firstDayOffset = (new Date(y, m - 1, 1).getDay() + 6) % 7; // Thứ 2 = 0 ... CN = 6

  const todayCutoffLabel = useMemo(() => {
    const now = new Date();
    const todayKey = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
    const todayDay = schedule?.days.find((d) => toDateKey(d.date) === todayKey);
    return todayDay?.cutoffTime ? todayDay.cutoffTime.replace(':', 'h') : LUNCH_CUTOFF_LABEL;
  }, [schedule]);

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
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
              <span
                className="badge badge--mint"
                style={{
                  border: '2px solid var(--color-fg)',
                  padding: '5px 14px',
                  fontWeight: 800,
                  borderRadius: 'var(--radius-full)',
                }}
              >
                <Sparkles size={14} /> Cổng Phụ Huynh Bán Trú
              </span>
            </div>
            <h1 style={{ margin: '4px 0 8px' }}>
              Lịch & Đăng Ký <span className="squiggle">Bán Trú Của Con</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)' }}>
              Chọn một hoặc nhiều ngày còn mở để đăng ký suất ăn độc lập cho từng ngày, xem thực đơn hoặc hủy trực tiếp trước hạn chốt.
            </p>
          </div>

          {/* Controls: Capsule Child selector + Month picker + Refresh Button */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            {children.length > 1 && (
              <div
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 8,
                  background: '#FFFFFF',
                  border: '2px solid var(--color-fg)',
                  borderRadius: 'var(--radius-full)',
                  boxShadow: '3px 3px 0 0 var(--color-fg)',
                  padding: '6px 14px 6px 16px',
                  height: 42,
                  boxSizing: 'border-box',
                }}
              >
                <Users size={16} color="var(--color-accent)" style={{ flexShrink: 0 }} />
                <select
                  id="parent-lunch-child"
                  value={studentId}
                  onChange={(e) => {
                    const nextId = Number(e.target.value);
                    selectChild(nextId);
                    setParams({ studentId: String(nextId), month });
                  }}
                  style={{
                    border: 'none',
                    background: 'transparent',
                    fontWeight: 800,
                    fontSize: 'var(--fs-sm)',
                    color: 'var(--color-fg)',
                    outline: 'none',
                    cursor: 'pointer',
                    fontFamily: 'inherit',
                    padding: 0,
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

            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                background: '#FFFFFF',
                border: '2px solid var(--color-fg)',
                borderRadius: 'var(--radius-full)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                padding: '6px 14px 6px 16px',
                height: 42,
                boxSizing: 'border-box',
              }}
            >
              <Calendar size={16} color="var(--color-accent)" style={{ flexShrink: 0 }} />
              <input
                id="parent-lunch-month"
                type="month"
                value={month}
                onChange={(e) => {
                  setMonth(e.target.value);
                  setParams({ studentId: String(studentId), month: e.target.value });
                }}
                style={{
                  border: 'none',
                  background: 'transparent',
                  fontWeight: 800,
                  fontSize: 'var(--fs-sm)',
                  color: 'var(--color-fg)',
                  outline: 'none',
                  cursor: 'pointer',
                  fontFamily: 'inherit',
                  padding: 0,
                }}
              />
            </div>

            <button
              type="button"
              className="btn"
              onClick={fetchSchedule}
              disabled={loading}
              title="Làm mới lịch bán trú"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                background: '#FFFFFF',
                color: 'var(--color-fg)',
                fontWeight: 800,
                fontSize: 'var(--fs-sm)',
                padding: '0 18px',
                height: 42,
                boxSizing: 'border-box',
                cursor: loading ? 'not-allowed' : 'pointer',
              }}
            >
              <RefreshCw size={15} className={loading ? 'spin' : ''} />
              Làm mới
            </button>
          </div>
        </div>
      </header>

      {/* Banner Quy tắc Địa phương Tân Lập A */}
      <div
        className="card"
        style={{
          marginBottom: 28,
          padding: '18px 24px',
          background: '#FFFDF5',
          border: '2px solid var(--color-fg)',
          borderRadius: 'var(--radius-lg)',
          boxShadow: '4px 4px 0 0 var(--color-fg)',
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
              border: '2px solid var(--color-fg)',
              boxShadow: '2px 2px 0 0 var(--color-fg)',
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
              <span
                className="badge badge--amber"
                style={{
                  border: '1.5px solid var(--color-fg)',
                  fontWeight: 800,
                  padding: '3px 10px',
                }}
              >
                Mốc chốt: {todayCutoffLabel} (theo cấu hình từng ngày)
              </span>
            </div>
            <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
              • Đăng ký mới hoặc hủy trực tiếp cần thực hiện trước <strong>giờ chốt của ngày phục vụ</strong> (mặc định {LUNCH_CUTOFF_LABEL}, có thể thay đổi theo từng ngày).<br />
              • <strong>Tích hợp Đơn xin nghỉ (WFL 1.2.3)</strong>: Đơn nghỉ có bữa trưa được duyệt trước giờ chốt ngày áp dụng (không tích &ldquo;Vẫn giữ suất ăn&rdquo;) sẽ tự động hủy suất ăn ngày đó.
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
          <button
            type="button"
            className="btn"
            onClick={handleSelectAllOpen}
            disabled={openSelectableDates.length === 0}
            style={{
              borderRadius: 'var(--radius-full)',
              border: '2px solid var(--color-fg)',
              boxShadow: '2.5px 2.5px 0 0 var(--color-fg)',
              background: '#FFFFFF',
              color: 'var(--color-fg)',
              fontWeight: 800,
              fontSize: 'var(--fs-sm)',
              padding: '8px 18px',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
              cursor: openSelectableDates.length === 0 ? 'not-allowed' : 'pointer',
            }}
          >
            <Check size={15} /> Chọn tất cả ngày còn mở ({openSelectableDates.length})
          </button>
          {selectedDates.length > 0 && (
            <button
              type="button"
              className="btn"
              onClick={() => openRegisterModal()}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                background: 'var(--color-accent)',
                color: '#FFFFFF',
                fontWeight: 800,
                fontSize: 'var(--fs-sm)',
                padding: '8px 20px',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                cursor: 'pointer',
              }}
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
          <div
            className="card"
            style={{
              position: 'relative',
              paddingTop: 28,
              background: '#FFFFFF',
              border: '2px solid var(--color-fg)',
              borderRadius: 'var(--radius-lg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
            }}
          >
            <div
              style={{
                position: 'absolute',
                top: -18,
                left: 18,
                width: 38,
                height: 38,
                borderRadius: '50%',
                background: 'var(--color-tertiary)',
                border: '2px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                display: 'grid',
                placeItems: 'center',
              }}
            >
              <GraduationCap size={18} color="var(--color-fg)" />
            </div>
            <div className="label" style={{ fontWeight: 800 }}>HỌC SINH ĐANG XEM</div>
            <div style={{ fontSize: 'var(--fs-lg)', fontWeight: 900, fontFamily: 'var(--font-heading)', marginTop: 4 }}>
              {schedule.studentName}
            </div>
            <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
              Mã HS: <strong>{schedule.studentCode}</strong> — Lớp: <strong>{schedule.className}</strong>
            </p>
            <div style={{ marginTop: 10, paddingTop: 8, borderTop: '1px dashed var(--color-border)', display: 'flex', flexDirection: 'column', gap: 6 }}>
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8 }}>
                <span style={{ fontSize: 'var(--fs-xs)', fontWeight: 700 }}>
                  Khẩu phần trường xếp:{' '}
                  <strong style={{ color: 'var(--color-accent)' }}>
                    {MEAL_TYPE_LABELS[String(schedule.mealProfile?.defaultMealType ?? 1)] ?? 'Suất tiêu chuẩn'}
                  </strong>
                </span>
                <button
                  type="button"
                  onClick={openProfileModal}
                  style={{
                    border: '1.5px solid var(--color-fg)',
                    background: '#FEF9C3',
                    borderRadius: 'var(--radius-full)',
                    padding: '3px 10px',
                    fontSize: 11,
                    fontWeight: 800,
                    cursor: 'pointer',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 4,
                  }}
                >
                  <Edit3 size={11} /> Khai báo dị ứng
                </button>
              </div>
              {((schedule.mealProfile?.allergenTags?.length ?? 0) > 0 || schedule.mealProfile?.allergyNotes) && (
                <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap', alignItems: 'center' }}>
                  {schedule.mealProfile?.allergenTags?.map((tag) => (
                    <span
                      key={tag}
                      style={{
                        fontSize: 10,
                        fontWeight: 800,
                        padding: '1px 6px',
                        borderRadius: 6,
                        background: '#FEE2E2',
                        color: '#991B1B',
                        border: '1px solid #FCA5A5',
                      }}
                    >
                      {getAllergenShortLabel(tag)}
                    </span>
                  ))}
                  {schedule.mealProfile?.allergyNotes && (
                    <span style={{ fontSize: 11, color: '#991B1B', fontWeight: 600 }}>
                      {schedule.mealProfile.allergyNotes}
                    </span>
                  )}
                </div>
              )}
            </div>
          </div>

          <div
            className="card"
            style={{
              position: 'relative',
              paddingTop: 28,
              background: '#FFFFFF',
              border: '2px solid var(--color-fg)',
              borderRadius: 'var(--radius-lg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
            }}
          >
            <div
              style={{
                position: 'absolute',
                top: -18,
                left: 18,
                width: 38,
                height: 38,
                borderRadius: '50%',
                background: 'var(--color-accent)',
                border: '2px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                display: 'grid',
                placeItems: 'center',
              }}
            >
              <UtensilsCrossed size={18} color="#FFFFFF" />
            </div>
            <div className="label" style={{ fontWeight: 800 }}>NGÀY NHÀ TRƯỜNG PHỤC VỤ</div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 900, fontFamily: 'var(--font-heading)' }}>
              {schedule.totalServingDays}{' '}
              <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 600, color: 'var(--color-muted-fg)' }}>ngày</span>
            </div>
            <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
              Còn <strong>{openSelectableDates.length}</strong> ngày đang mở đăng ký
            </p>
          </div>

          <div
            className="card"
            style={{
              position: 'relative',
              paddingTop: 28,
              background: '#ECFDF5',
              border: '2px solid var(--color-fg)',
              borderRadius: 'var(--radius-lg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
            }}
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
                border: '2px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                display: 'grid',
                placeItems: 'center',
              }}
            >
              <Soup size={18} color="var(--color-fg)" />
            </div>
            <div className="label" style={{ color: '#047857', fontWeight: 800 }}>ĐÃ ĐĂNG KÝ CÒN HIỆU LỰC</div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 900, color: '#065f46', fontFamily: 'var(--font-heading)' }}>
              {schedule.totalRegisteredDays}{' '}
              <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 700, color: '#047857' }}>suất ăn</span>
            </div>
            <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: '#047857', fontWeight: 600 }}>
              Mỗi ngày là 1 bản ghi đăng ký độc lập
            </p>
          </div>

          <div
            className="card"
            style={{
              position: 'relative',
              paddingTop: 28,
              background: '#FFF1F2',
              border: '2px solid var(--color-fg)',
              borderRadius: 'var(--radius-lg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
            }}
          >
            <div
              style={{
                position: 'absolute',
                top: -18,
                left: 18,
                width: 38,
                height: 38,
                borderRadius: '50%',
                background: 'var(--color-secondary)',
                border: '2px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                display: 'grid',
                placeItems: 'center',
              }}
            >
              <XCircle size={18} color="#FFFFFF" />
            </div>
            <div className="label" style={{ color: '#E11D48', fontWeight: 800 }}>SUẤT ĐÃ HỦY TRONG THÁNG</div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 900, color: '#e11d48', fontFamily: 'var(--font-heading)' }}>
              {schedule.totalCancelledDays}{' '}
              <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 600, color: 'var(--color-muted-fg)' }}>ngày</span>
            </div>
            <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
              Gồm PH hủy trực tiếp & tự hủy từ đơn nghỉ
            </p>
          </div>
        </div>
      )}

      {/* Selection Bar when dates are picked */}
      {selectedDates.length > 0 && (
        <div
          className="card pop-in"
          style={{
            marginBottom: 24,
            padding: '16px 24px',
            background: '#FAF5FF',
            border: '2px solid var(--color-accent)',
            borderRadius: 'var(--radius-lg)',
            boxShadow: '4px 4px 0 0 var(--color-fg)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: 16,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <span
              className="badge badge--violet"
              style={{
                border: '1.5px solid var(--color-fg)',
                fontWeight: 800,
                padding: '4px 12px',
                fontSize: 'var(--fs-sm)',
              }}
            >
              Đang chọn {selectedDates.length} ngày
            </span>
            <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 700, color: 'var(--color-fg)' }}>
              {selectedDates
                .slice(0, 6)
                .map((d) => formatDateDDMMYYYY(d))
                .join(', ')}
              {selectedDates.length > 6 ? ` (+${selectedDates.length - 6} ngày khác)` : ''}
            </span>
          </div>

          <div style={{ display: 'flex', gap: 10 }}>
            <button
              type="button"
              className="btn"
              onClick={handleClearSelection}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                fontWeight: 700,
                fontSize: 'var(--fs-sm)',
                padding: '8px 18px',
                background: '#FFFFFF',
                color: 'var(--color-fg)',
                cursor: 'pointer',
              }}
            >
              Bỏ chọn
            </button>
            <button
              type="button"
              className="btn"
              onClick={() => openRegisterModal()}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                fontWeight: 800,
                fontSize: 'var(--fs-sm)',
                padding: '8px 22px',
                background: 'var(--color-accent)',
                color: '#FFFFFF',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                cursor: 'pointer',
              }}
            >
              <CheckCircle2 size={16} /> Xác nhận đăng ký ({selectedDates.length} ngày)
            </button>
          </div>
        </div>
      )}

      {/* Calendar Grid (UC-29, UC-30, UC-31) - Refactored for clean scannability */}
      <div
        className="card"
        style={{
          marginBottom: 28,
          border: '2px solid var(--color-fg)',
          boxShadow: '4px 4px 0 0 var(--color-fg)',
          borderRadius: 'var(--radius-lg)',
          background: '#FFFFFF',
          padding: '24px',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12, marginBottom: 20 }}>
          <h3 style={{ margin: 0, fontFamily: 'var(--font-heading)', fontSize: 'var(--fs-lg)', display: 'flex', alignItems: 'center', gap: 8 }}>
            <Calendar size={22} color="var(--color-accent)" />
            Lịch Phục Vụ Bán Trú Tháng {month}
          </h3>

          {/* Legend with crisp Neo-Brutalist borders */}
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', fontSize: 'var(--fs-xs)' }}>
            <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
              ✓ Đã đăng ký
            </span>
            <span className="badge badge--violet" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
              Đang chọn
            </span>
            <span
              className="badge"
              style={{
                border: '1.5px solid var(--color-fg)',
                background: '#FAF5FF',
                color: 'var(--color-accent)',
                fontWeight: 800,
              }}
            >
              + Còn mở
            </span>
            <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
              Đã hủy
            </span>
            <span className="badge badge--amber" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
              Nghỉ học (đã duyệt)
            </span>
            <span className="badge badge--muted" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
              Nghỉ / Đã chốt {LUNCH_CUTOFF_LABEL}
            </span>
          </div>
        </div>

        {/* Mobile swipe hint + Calendar Grid Wrapper */}
        <div className="mobile-only-hint">Vuốt ngang để xem đủ các ngày trong tuần →</div>
        <div className="calendar-mobile-scroll">
          <div className="calendar-mobile-scroll__inner">
        {/* Day of Week Headers */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, minmax(0, 1fr))', gap: 10, marginBottom: 10 }}>
          {['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'CN'].map((dow, i) => (
            <div
              key={dow}
              style={{
                textAlign: 'center',
                fontWeight: 800,
                padding: '10px 4px',
                background: i >= 5 ? '#F1F5F9' : '#FFFDF5',
                borderRadius: 'var(--radius-sm)',
                border: '1.5px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                fontSize: 'var(--fs-xs)',
                color: i >= 5 ? 'var(--color-muted-fg)' : 'var(--color-fg)',
              }}
            >
              {dow}
            </div>
          ))}
        </div>

        {/* Calendar Cells Grid */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, minmax(0, 1fr))', gap: 10 }}>
          {Array.from({ length: firstDayOffset }).map((_, idx) => (
            <div key={`empty-${idx}`} />
          ))}

          {schedule?.days.map((day) => {
            const dKey = toDateKey(day.date);
            const dayNum = Number(dKey.slice(8, 10));
            const isSelected = selectedDates.includes(dKey);
            const isCancelled = !day.isRegistered && day.cancellationSource != null;
            const isBlockedByLeave = Boolean(day.hasApprovedLeave) && !day.isRegistered;
            const canSelectToRegister = day.isServing && !day.isCutoffPassed && !day.isRegistered && !isBlockedByLeave;
            const canCancel = day.isRegistered && !day.isCutoffPassed;
            const cleanedRemarks = cleanText(day.serviceRemarks);

            let bg = '#FFFFFF';
            let borderColor = 'var(--color-fg)';
            let shadow = '2px 2px 0 0 var(--color-fg)';

            if (!day.isServing) {
              bg = '#F8FAFC';
              borderColor = '#CBD5E1';
              shadow = 'none';
            } else if (isSelected) {
              bg = '#EDE9FE';
              borderColor = 'var(--color-fg)';
              shadow = '4px 4px 0 0 var(--color-accent)';
            } else if (day.isRegistered) {
              bg = '#ECFDF5';
              borderColor = 'var(--color-fg)';
              shadow = '3px 3px 0 0 #10B981';
            } else if (isCancelled) {
              bg = '#FFF1F2';
              borderColor = 'var(--color-secondary)';
              shadow = '2px 2px 0 0 var(--color-secondary)';
            } else if (isBlockedByLeave) {
              bg = '#FFFBEB';
              borderColor = '#D97706';
              shadow = '2px 2px 0 0 #D97706';
            } else if (day.isCutoffPassed) {
              bg = '#F8FAFC';
              borderColor = '#CBD5E1';
              shadow = 'none';
            } else {
              // Open for registration
              bg = '#FFFFFF';
              borderColor = 'var(--color-fg)';
              shadow = '2px 2px 0 0 var(--color-fg)';
            }

            return (
              <div
                key={dKey}
                onClick={() => toggleSelectDate(day)}
                style={{
                  minHeight: 112,
                  padding: '9px 10px',
                  borderRadius: 'var(--radius-md)',
                  border: `2px solid ${borderColor}`,
                  background: bg,
                  boxShadow: shadow,
                  cursor: canSelectToRegister ? 'pointer' : 'default',
                  display: 'flex',
                  flexDirection: 'column',
                  justifyContent: 'space-between',
                  transition: 'all 0.15s ease',
                  opacity: !day.isServing ? 0.6 : 1,
                  position: 'relative',
                }}
              >
                {/* Top row: Date number + Cutoff badge */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span
                    style={{
                      fontWeight: 900,
                      fontSize: 'var(--fs-base)',
                      fontFamily: 'var(--font-heading)',
                      color: !day.isServing ? 'var(--color-muted-fg)' : 'var(--color-fg)',
                    }}
                  >
                    {dayNum}
                  </span>

                  {day.isServing && (
                    <span
                      style={{
                        fontSize: '10px',
                        fontWeight: 700,
                        color: day.isCutoffPassed ? 'var(--color-muted-fg)' : '#B45309',
                        background: day.isCutoffPassed ? 'transparent' : '#FEF3C7',
                        padding: day.isCutoffPassed ? '0' : '1px 6px',
                        borderRadius: 'var(--radius-full)',
                      }}
                    >
                      {day.cutoffTime}
                    </span>
                  )}
                </div>

                {/* Middle: Clean Status Badge */}
                <div style={{ margin: '4px 0' }}>
                  {!day.isServing ? (
                    <span style={{ fontSize: '11px', color: 'var(--color-muted-fg)', fontStyle: 'italic' }}>
                      Nghỉ
                    </span>
                  ) : isSelected ? (
                    <span
                      className="badge badge--violet"
                      style={{
                        fontSize: '10px',
                        border: '1.5px solid var(--color-fg)',
                        fontWeight: 800,
                        padding: '2px 8px',
                      }}
                    >
                      <Check size={11} /> Đã chọn
                    </span>
                  ) : day.isRegistered ? (
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4, alignItems: 'center' }}>
                      <span
                        className="badge badge--mint"
                        style={{
                          fontSize: '10px',
                          border: '1.5px solid var(--color-fg)',
                          fontWeight: 800,
                          padding: '2px 7px',
                        }}
                      >
                        ✓ Đã đăng ký
                      </span>
                      <span
                        className="badge"
                        style={{
                          fontSize: '9.5px',
                          border: '1px solid var(--color-fg)',
                          fontWeight: 800,
                          padding: '1px 6px',
                          background:
                            day.mealType === 'VegetarianLunch' || day.mealType === 2
                              ? '#FEF3C7'
                              : day.mealType === 'SpecialDiet' || day.mealType === 3
                              ? '#FCE7F3'
                              : '#E0F2FE',
                          color: 'var(--color-fg)',
                        }}
                      >
                        {day.mealType === 'VegetarianLunch' || day.mealType === 2
                          ? 'Chay'
                          : day.mealType === 'SpecialDiet' || day.mealType === 3
                          ? 'Đặc biệt'
                          : 'Tiêu chuẩn'}
                      </span>
                    </div>
                  ) : isCancelled ? (
                    <span
                      className="badge badge--pink"
                      style={{
                        fontSize: '10px',
                        border: '1.5px solid var(--color-fg)',
                        fontWeight: 800,
                        padding: '2px 8px',
                      }}
                    >
                      {day.cancellationSource === 'Leave' || day.cancellationSource === 2
                        ? 'Đã hủy (Đơn nghỉ)'
                        : 'Đã hủy'}
                    </span>
                  ) : isBlockedByLeave ? (
                    <span
                      className="badge badge--amber"
                      style={{
                        fontSize: '10px',
                        border: '1.5px solid var(--color-fg)',
                        fontWeight: 800,
                        padding: '2px 8px',
                      }}
                    >
                      Nghỉ học (đã duyệt)
                    </span>
                  ) : day.isCutoffPassed ? (
                    <span
                      className="badge badge--muted"
                      style={{
                        fontSize: '10px',
                        fontWeight: 700,
                        padding: '2px 8px',
                      }}
                    >
                      Đã khóa
                    </span>
                  ) : (
                    <div
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: 4,
                        border: '1.5px dashed var(--color-accent)',
                        borderRadius: 'var(--radius-full)',
                        padding: '2px 8px',
                        fontSize: '10px',
                        fontWeight: 800,
                        color: 'var(--color-accent)',
                        background: '#FAF5FF',
                      }}
                    >
                      + Đăng ký
                    </div>
                  )}

                  {/* Menu preview (single concise line with tooltip) */}
                  {day.isServing && cleanedRemarks && (
                    <div
                      style={{
                        margin: '4px 0 0',
                        fontSize: '10.5px',
                        color: 'var(--color-muted-fg)',
                        whiteSpace: 'nowrap',
                        overflow: 'hidden',
                        textOverflow: 'ellipsis',
                        lineHeight: 1.2,
                      }}
                      title={cleanedRemarks}
                    >
                      🍴 {cleanedRemarks.replace(/^Thực đơn:\s*/i, '').replace(/^Thực đơn Tân Lập A:\s*/i, '')}
                    </div>
                  )}
                </div>

                {/* Bottom row: Info link & Cancel pill */}
                <div
                  style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 4 }}
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
                        fontSize: '10.5px',
                        color: 'var(--color-accent)',
                        fontWeight: 800,
                        cursor: 'pointer',
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: 2,
                      }}
                      title="Xem thực đơn chi tiết"
                    >
                      <Info size={11} /> Chi tiết
                    </button>
                  )}

                  {canCancel && (
                    <div style={{ display: 'inline-flex', gap: 4 }}>
                      <button
                        type="button"
                        onClick={() => {
                          setCancelTargetDay(day);
                          setCancelReason('');
                        }}
                        style={{
                          background: '#FFE4E6',
                          border: '1.5px solid #F43F5E',
                          borderRadius: 'var(--radius-full)',
                          boxShadow: '1px 1px 0 0 var(--color-fg)',
                          padding: '2px 7px',
                          fontSize: '10px',
                          fontWeight: 800,
                          color: '#E11D48',
                          cursor: 'pointer',
                        }}
                      >
                        Hủy
                      </button>
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
          </div>
        </div>
      </div>

      {/* Detailed Registration & Cancellation History Table */}
      <div
        className="card"
        style={{
          border: '2px solid var(--color-fg)',
          boxShadow: '4px 4px 0 0 var(--color-fg)',
          borderRadius: 'var(--radius-lg)',
          background: '#FFFFFF',
          padding: '24px',
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
          <h3 style={{ margin: 0, fontFamily: 'var(--font-heading)', fontSize: 'var(--fs-md)', display: 'flex', alignItems: 'center', gap: 8 }}>
            <Soup size={20} color="var(--color-accent)" />
            Danh sách các ngày Đã đăng ký & Biến động trong tháng ({registeredOrCancelledDays.length} ngày)
          </h3>
        </div>

        {registeredOrCancelledDays.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '36px 16px', color: 'var(--color-muted-fg)', background: '#FFFDF5', borderRadius: 'var(--radius-md)', border: '1.5px dashed var(--color-fg)' }}>
            Chưa có bản ghi đăng ký bán trú nào trong tháng {month}. Phụ huynh hãy tích chọn các ngày còn mở trên lịch để đăng ký cho con.
          </div>
        ) : (
          <>
            <div className="desktop-table-wrap" style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#FFFDF5' }}>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Ngày phục vụ</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Loại suất</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Thực đơn / Ghi chú ngày</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Hạn chốt</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Trạng thái</th>
                  <th style={{ padding: '12px 14px', textAlign: 'right', fontWeight: 800 }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {registeredOrCancelledDays.map((day) => {
                  const dKey = toDateKey(day.date);
                  const canCancel = day.isRegistered && !day.isCutoffPassed;
                  const cleanedRemarks = cleanText(day.serviceRemarks);

                  return (
                    <tr key={dKey} style={{ borderBottom: '1px solid var(--color-border)' }}>
                      <td style={{ padding: '14px', fontWeight: 800 }}>
                        {formatWeekdayDateDDMMYYYY(day.date)}
                      </td>
                      <td style={{ padding: '14px' }}>
                        <span
                          className="badge"
                          style={{
                            border: '1.5px solid var(--color-fg)',
                            fontWeight: 700,
                            background: '#F1F5F9',
                          }}
                        >
                          {day.mealType ? MEAL_TYPE_LABELS[String(day.mealType)] ?? 'Suất tiêu chuẩn' : 'Suất tiêu chuẩn'}
                        </span>
                      </td>
                      <td style={{ padding: '14px', color: 'var(--color-fg)', maxWidth: 360 }}>
                        <div style={{ fontWeight: 600 }}>{cleanedRemarks || 'Thực đơn tiêu chuẩn nhà bếp'}</div>
                        {(day.allergyConflicts?.length ?? 0) > 0 && (
                          <div
                            style={{
                              marginTop: 6,
                              padding: '5px 8px',
                              borderRadius: 8,
                              background: '#FEF2F2',
                              border: '1.5px solid #DC2626',
                              color: '#991B1B',
                              fontSize: 'var(--fs-xs)',
                              fontWeight: 800,
                              display: 'flex',
                              flexDirection: 'column',
                              gap: 4,
                            }}
                          >
                            <span style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                              <AlertTriangle size={13} /> Cảnh báo dị ứng: {day.allergyConflicts!.join(' • ')}
                            </span>
                            <span style={{ fontWeight: 600, fontSize: 11, color: '#7F1D1D' }}>
                              Tổ Bán trú sẽ căn cứ cảnh báo này để bố trí khẩu phần thay thế, hoặc phụ huynh có thể hủy đăng ký trước giờ chốt nếu đón bé ăn riêng.
                            </span>
                          </div>
                        )}
                        {day.studentRemarks && (
                          <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-accent)', marginTop: 4, fontWeight: 700 }}>
                            Ghi chú PH: {day.studentRemarks}
                          </div>
                        )}
                        {day.cancellationReason && (
                          <div style={{ fontSize: 'var(--fs-xs)', color: '#E11D48', marginTop: 4, fontWeight: 700 }}>
                            Lý do hủy: {day.cancellationReason}
                          </div>
                        )}
                      </td>
                      <td style={{ padding: '14px', textAlign: 'center', fontWeight: 700 }}>
                        {day.cutoffTime}
                      </td>
                      <td style={{ padding: '14px', textAlign: 'center' }}>
                        {day.isRegistered ? (
                          <span
                            className="badge badge--mint"
                            style={{
                              border: '1.5px solid var(--color-fg)',
                              fontWeight: 800,
                              padding: '3px 10px',
                            }}
                          >
                            ✓ Còn hiệu lực
                          </span>
                        ) : (
                          <span
                            className="badge badge--pink"
                            style={{
                              border: '1.5px solid var(--color-fg)',
                              fontWeight: 800,
                              padding: '3px 10px',
                            }}
                          >
                            {SOURCE_LABELS[String(day.cancellationSource)] ?? 'Đã hủy'}
                          </span>
                        )}
                      </td>
                      <td style={{ padding: '14px', textAlign: 'right' }}>
                        {canCancel ? (
                          <div style={{ display: 'inline-flex', gap: 8, justifyContent: 'flex-end' }}>
                            <button
                              type="button"
                              className="btn btn--sm"
                              onClick={() => {
                                setCancelTargetDay(day);
                                setCancelReason('');
                              }}
                              style={{
                                borderRadius: 'var(--radius-full)',
                                border: '1.5px solid #F43F5E',
                                boxShadow: '2px 2px 0 0 var(--color-fg)',
                                background: '#FFE4E6',
                                color: '#E11D48',
                                fontWeight: 800,
                                fontSize: 'var(--fs-xs)',
                                padding: '6px 14px',
                                cursor: 'pointer',
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: 6,
                              }}
                            >
                              <Trash2 size={13} /> Hủy đăng ký
                            </button>
                          </div>
                        ) : day.isRegistered ? (
                          <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                            Đã qua hạn {day.cutoffTime}
                          </span>
                        ) : !day.isCutoffPassed && day.isServing && !day.hasApprovedLeave ? (
                          <button
                            type="button"
                            className="btn btn--primary btn--sm"
                            onClick={() => openRegisterModal([dKey])}
                            style={{
                              borderRadius: 'var(--radius-full)',
                              border: '1.5px solid var(--color-fg)',
                              boxShadow: '2px 2px 0 0 var(--color-fg)',
                              fontWeight: 800,
                              padding: '6px 14px',
                              cursor: 'pointer',
                            }}
                          >
                            Đăng ký lại
                          </button>
                        ) : day.hasApprovedLeave ? (
                          <span style={{ fontSize: 'var(--fs-xs)', color: '#B45309', fontWeight: 700 }}>
                            Đang có đơn nghỉ đã duyệt
                          </span>
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

          <div className="mobile-card-list">
            {registeredOrCancelledDays.map((day) => {
              const dKey = toDateKey(day.date);
              const canCancel = day.isRegistered && !day.isCutoffPassed;
              const cleanedRemarks = cleanText(day.serviceRemarks);

              return (
                <div key={`m-${dKey}`} className="mobile-item-card">
                  <div className="mobile-item-card__head">
                    <div>
                      <strong style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>
                        {formatWeekdayDateDDMMYYYY(day.date)}
                      </strong>
                      <div className="mobile-item-card__meta" style={{ marginTop: 4 }}>
                        <span className="badge" style={{ border: '1px solid var(--color-fg)', fontWeight: 700, background: '#F1F5F9' }}>
                          {day.mealType ? MEAL_TYPE_LABELS[String(day.mealType)] ?? 'Suất tiêu chuẩn' : 'Suất tiêu chuẩn'}
                        </span>
                        <span>Hạn chốt: <strong>{day.cutoffTime}</strong></span>
                      </div>
                    </div>
                    {day.isRegistered ? (
                      <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                        ✓ Còn hiệu lực
                      </span>
                    ) : (
                      <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                        {SOURCE_LABELS[String(day.cancellationSource)] ?? 'Đã hủy'}
                      </span>
                    )}
                  </div>

                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-fg)' }}>
                    <div>{cleanedRemarks || 'Thực đơn tiêu chuẩn nhà bếp'}</div>
                    {day.studentRemarks && (
                      <div style={{ color: 'var(--color-accent)', marginTop: 4, fontWeight: 700 }}>
                        Ghi chú PH: {day.studentRemarks}
                      </div>
                    )}
                    {day.cancellationReason && (
                      <div style={{ color: '#E11D48', marginTop: 4, fontWeight: 700 }}>
                        Lý do hủy: {day.cancellationReason}
                      </div>
                    )}
                  </div>

                  <div className="mobile-item-card__foot">
                    {canCancel ? (
                      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', width: '100%' }}>
                        <button
                          type="button"
                          className="btn btn--sm"
                          onClick={() => {
                            setCancelTargetDay(day);
                            setCancelReason('');
                          }}
                          style={{
                            flex: 1,
                            border: '1.5px solid #F43F5E',
                            boxShadow: '2px 2px 0 0 var(--color-fg)',
                            background: '#FFE4E6',
                            color: '#E11D48',
                            fontWeight: 800,
                            fontSize: 'var(--fs-xs)',
                          }}
                        >
                          <Trash2 size={13} /> Hủy đăng ký
                        </button>
                      </div>
                    ) : day.isRegistered ? (
                      <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 600 }}>
                        Đã qua hạn {day.cutoffTime}
                      </span>
                    ) : !day.isCutoffPassed && day.isServing && !day.hasApprovedLeave ? (
                      <button
                        type="button"
                        className="btn btn--primary btn--sm btn--full"
                        onClick={() => openRegisterModal([dKey])}
                      >
                        Đăng ký lại
                      </button>
                    ) : day.hasApprovedLeave ? (
                      <span style={{ fontSize: 'var(--fs-xs)', color: '#B45309', fontWeight: 700 }}>
                        Đang có đơn nghỉ đã duyệt
                      </span>
                    ) : (
                      <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>—</span>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
          </>
        )}
      </div>

      {/* MODAL UC-29: Xác nhận Đăng ký suất ăn bán trú */}
      <GeometricModal
        open={showRegisterModal}
        onClose={() => setShowRegisterModal(false)}
        title="Xác nhận Đăng ký Bán trú"
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
              background: '#FFFDF5',
              border: '2px solid var(--color-fg)',
              boxShadow: '2px 2px 0 0 var(--color-fg)',
              borderRadius: 'var(--radius-md)',
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
              <span
                key={d}
                className="badge badge--violet"
                style={{
                  border: '1.5px solid var(--color-fg)',
                  fontWeight: 800,
                }}
              >
                {formatDateDDMMYYYY(d)}
              </span>
            ))}
          </div>

          <div
            style={{
              marginBottom: 14,
              padding: '10px 14px',
              borderRadius: 'var(--radius-md)',
              border: '1.5px solid var(--color-fg)',
              background: '#ECFDF5',
              fontSize: 'var(--fs-xs)',
              lineHeight: 1.5,
              color: '#065F46',
            }}
          >
            <strong>Thực đơn thống nhất của nhà trường:</strong> Khẩu phần ăn áp dụng theo lịch thực đơn tuần chung của trường{' '}
            (chế độ hiện tại của bé:{' '}
            <strong>{MEAL_TYPE_LABELS[String(schedule?.mealProfile?.defaultMealType ?? 1)] ?? 'Suất tiêu chuẩn'}</strong>). Nếu bé có dị ứng thực phẩm, Tổ Bán trú sẽ chủ động bố trí khẩu phần phù hợp.
          </div>

          <div className="field" style={{ marginBottom: 20 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <label className="label" htmlFor="register-remarks" style={{ fontWeight: 800, letterSpacing: '0.04em' }}>
                GHI CHÚ SỨC KHỎE / DẶN DÒ BỘ PHẬN BÁN TRÚ (NẾU CÓ)
              </label>
              <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>{studentRemarks.length}/250</span>
            </div>
            <input
              id="register-remarks"
              type="text"
              className="input"
              maxLength={250}
              placeholder="VD: Bé dị ứng tôm/đậu phộng hoặc mới khỏi ốm, nhờ cô lưu ý..."
              value={studentRemarks}
              onChange={(e) => setStudentRemarks(e.target.value)}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                fontWeight: 700,
                padding: '8px 16px',
              }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowRegisterModal(false)}
              disabled={submittingRegister}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                fontWeight: 700,
                padding: '10px 20px',
              }}
            >
              Đóng
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleConfirmRegister}
              disabled={submittingRegister}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '4px 4px 0 0 var(--color-fg)',
                fontWeight: 800,
                padding: '10px 24px',
                background: 'var(--color-accent)',
                color: '#FFFFFF',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
              }}
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
        title="Hủy đăng ký suất ăn bán trú"
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
              <strong>{formatDateDDMMYYYY(cancelTargetDay.date)}</strong> của bé{' '}
              <strong>{schedule?.studentName}</strong>.
            </p>

            <div className="field" style={{ marginBottom: 20 }}>
              <label className="label" htmlFor="cancel-lunch-reason" style={{ fontWeight: 800, letterSpacing: '0.04em' }}>
                LÝ DO HỦY SUẤT ĂN (TÙY CHỌN, TỐI ĐA 300 KÝ TỰ)
              </label>
              <input
                id="cancel-lunch-reason"
                type="text"
                className="input"
                maxLength={300}
                placeholder="VD: Gia đình đón bé về ăn trưa tại nhà..."
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  fontWeight: 700,
                  padding: '8px 16px',
                }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => setCancelTargetDay(null)}
                disabled={submittingCancel}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  fontWeight: 700,
                  padding: '10px 20px',
                }}
              >
                Giữ lại suất ăn
              </button>
              <button
                type="button"
                className="btn btn--primary"
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '4px 4px 0 0 var(--color-fg)',
                  fontWeight: 800,
                  padding: '10px 24px',
                  background: '#E11D48',
                  color: '#FFFFFF',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 8,
                }}
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
        title={`Thông tin Bán trú ngày ${detailDay ? formatDateDDMMYYYY(detailDay.date) : ''}`}
        badgeText="Chi tiết ngày phục vụ"
        badgeColor="amber"
        icon={<Soup size={22} color="var(--color-fg)" />}
        iconBg="var(--color-tertiary)"
        maxWidth="sm"
      >
        {detailDay && (
          <div>
            <div
              className="card"
              style={{
                background: '#FFFDF5',
                border: '2px solid var(--color-fg)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-md)',
                padding: '16px 18px',
                marginBottom: 16,
              }}
            >
              <div style={{ marginBottom: 10, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span className="label" style={{ fontWeight: 800 }}>Trạng thái phục vụ:</span>{' '}
                <span
                  className={detailDay.isServing ? 'badge badge--mint' : 'badge badge--pink'}
                  style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}
                >
                  {detailDay.isServing ? 'Có phục vụ bán trú' : 'Không phục vụ'}
                </span>
              </div>
              <div style={{ marginBottom: 10, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span className="label" style={{ fontWeight: 800 }}>Hạn chốt ngày:</span>{' '}
                <strong style={{ color: '#B45309' }}>{detailDay.cutoffTime} sáng</strong>
              </div>
              <div style={{ borderTop: '1px solid var(--color-border)', paddingTop: 10, marginTop: 10 }}>
                <span className="label" style={{ fontWeight: 800, marginBottom: 6, display: 'block' }}>Thực đơn dinh dưỡng:</span>
                {detailDay.menus ? (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6, fontSize: 'var(--fs-xs)' }}>
                    {detailDay.menus.standard.length > 0 && (
                      <div>
                        <strong>Tiêu chuẩn:</strong> {detailDay.menus.standard.map((d) => d.name).join(', ')}
                      </div>
                    )}
                    {detailDay.menus.vegetarian.length > 0 && (
                      <div>
                        <strong>Ăn chay:</strong> {detailDay.menus.vegetarian.map((d) => d.name).join(', ')}
                      </div>
                    )}
                    {detailDay.menus.special.length > 0 && (
                      <div>
                        <strong>Đặc biệt:</strong> {detailDay.menus.special.map((d) => d.name).join(', ')}
                      </div>
                    )}
                  </div>
                ) : (
                  <p style={{ margin: '4px 0 0', fontWeight: 700, fontSize: 'var(--fs-sm)', color: 'var(--color-fg)', lineHeight: 1.5 }}>
                    🍴 {cleanText(detailDay.serviceRemarks) || 'Thực đơn tiêu chuẩn theo tuần của nhà bếp.'}
                  </p>
                )}

                {(detailDay.allergyConflicts?.length ?? 0) > 0 && (
                  <div
                    style={{
                      marginTop: 10,
                      padding: '8px 10px',
                      borderRadius: 8,
                      background: '#FEF2F2',
                      border: '1.5px solid #DC2626',
                      color: '#991B1B',
                      fontSize: 'var(--fs-xs)',
                      fontWeight: 800,
                    }}
                  >
                    Cảnh báo trùng dị ứng của con: {detailDay.allergyConflicts!.join(' • ')}
                  </div>
                )}
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => setDetailDay(null)}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  fontWeight: 700,
                  padding: '8px 20px',
                }}
              >
                Đóng
              </button>
            </div>
          </div>
        )}
      </GeometricModal>

      {/* MODAL Khai báo Dị ứng Thực phẩm & Lưu ý Sức khỏe của con */}
      <GeometricModal
        open={showProfileModal}
        onClose={() => setShowProfileModal(false)}
        title={`Khai báo Dị ứng Thực phẩm • ${schedule?.studentName ?? ''}`}
        subtitle="Cung cấp thông tin dị ứng / bệnh lý dinh dưỡng để Tổ Bán trú & Y tế nhà trường bố trí khẩu phần phù hợp"
        badgeText="Thông tin sức khỏe"
        badgeColor="mint"
        icon={<ShieldAlert size={22} color="var(--color-fg)" />}
        iconBg="#DCFCE7"
        maxWidth="md"
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div
            style={{
              padding: '10px 12px',
              borderRadius: 'var(--radius-sm)',
              background: '#F8FAFC',
              border: '1.5px solid var(--color-border)',
              fontSize: 'var(--fs-xs)',
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
            }}
          >
            <span style={{ fontWeight: 700, color: 'var(--color-muted-fg)' }}>Khẩu phần nhà trường đang phân bổ:</span>
            <strong style={{ color: 'var(--color-accent)' }}>
              {MEAL_TYPE_LABELS[String(schedule?.mealProfile?.defaultMealType ?? 1)] ?? 'Suất tiêu chuẩn'}
            </strong>
          </div>

          <div>
            <label className="label" style={{ fontSize: 'var(--fs-xs)' }}>NHÓM THỰC PHẨM BÉ BỊ DỊ ỨNG</label>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              {ALLERGEN_OPTIONS.map((al) => {
                const active = profileTags.includes(al.code);
                return (
                  <button
                    key={al.code}
                    type="button"
                    onClick={() =>
                      setProfileTags((prev) =>
                        prev.includes(al.code) ? prev.filter((x) => x !== al.code) : [...prev, al.code]
                      )
                    }
                    style={{
                      padding: '6px 12px',
                      borderRadius: 'var(--radius-full)',
                      border: active ? '2px solid #DC2626' : '1.5px solid var(--color-border)',
                      background: active ? '#FEE2E2' : '#FFFFFF',
                      color: active ? '#991B1B' : 'var(--color-fg)',
                      fontWeight: 700,
                      fontSize: 'var(--fs-xs)',
                      cursor: 'pointer',
                    }}
                  >
                    {active ? '• ' : '+ '}
                    {al.label}
                  </button>
                );
              })}
            </div>
          </div>

          <div>
            <label className="label" style={{ fontSize: 'var(--fs-xs)' }}>GHI CHÚ DỊ ỨNG / DẶN DÒ NHÀ BẾP</label>
            <textarea
              className="input"
              rows={3}
              maxLength={250}
              value={profileNotes}
              onChange={(e) => setProfileNotes(e.target.value)}
              placeholder="VD: Cháu dị ứng nặng tôm cua, uống sữa không đường hoặc cháo mềm khi ốm..."
              style={{ width: '100%' }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button type="button" className="btn btn--secondary" onClick={() => setShowProfileModal(false)} disabled={savingProfile}>
              Hủy
            </button>
            <button type="button" className="btn btn--primary" onClick={handleSaveProfile} disabled={savingProfile}>
              {savingProfile ? 'Đang lưu...' : 'Lưu khai báo dị ứng'}
            </button>
          </div>
        </div>
      </GeometricModal>
    </div>
  );
}
