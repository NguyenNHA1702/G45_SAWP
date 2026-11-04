import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router';
import {
  Sparkles,
  UtensilsCrossed,
  Clock,
  CheckCircle2,
  Printer,
  Lock,
  RefreshCw,
  UserMinus,
  Soup,
  ShieldAlert,
  FileSpreadsheet,
  Settings,
  Download,
  Users,
  Filter,
  Search,
  ArrowRight,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { lunchApi } from '../../services/implementations/lunchApi';
import type { DailyMealBoard } from '../../types/lunch.types';
import { LUNCH_CUTOFF_DEFAULT_TIME, LUNCH_CUTOFF_LABEL } from '../../constants/lunch';
import { SYSTEM_MESSAGES, getSystemMessage } from '../../constants/messages';
import { GeometricModal } from '../../components/common/GeometricModal';

const MEAL_TYPE_TEXT: Record<string, string> = {
  StandardLunch: 'Tiêu chuẩn',
  VegetarianLunch: 'Ăn chay',
  SpecialDiet: 'Đặc biệt',
  '1': 'Tiêu chuẩn',
  '2': 'Ăn chay',
  '3': 'Đặc biệt',
};

function getWeekdaysOfDate(isoDate: string): string[] {
  const d = new Date(isoDate);
  const day = d.getDay(); // 0 CN, 1 T2 ...
  const diffToMonday = day === 0 ? -6 : 1 - day;
  const monday = new Date(d);
  monday.setDate(d.getDate() + diffToMonday);

  const days: string[] = [];
  for (let i = 0; i < 5; i++) {
    const cur = new Date(monday);
    cur.setDate(monday.getDate() + i);
    const yyyy = cur.getFullYear();
    const mm = String(cur.getMonth() + 1).padStart(2, '0');
    const dd = String(cur.getDate()).padStart(2, '0');
    days.push(`${yyyy}-${mm}-${dd}`);
  }
  return days;
}

export default function MealHeadcountPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = (searchParams.get('tab') as 'summary' | 'students' | 'logs') || 'summary';

  const [selectedDate, setSelectedDate] = useState<string>(() => {
    const d = new Date();
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  });
  const [board, setBoard] = useState<DailyMealBoard | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [actionLoading, setActionLoading] = useState<boolean>(false);
  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' } | null>(null);

  // Bộ lọc & Tìm kiếm danh sách học sinh (UC-33)
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [selectedClassFilter, setSelectedClassFilter] = useState<string>('ALL');
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'ACTIVE' | 'CANCELLED'>('ACTIVE');

  // Phân trang (Pagination) chuẩn theo phong cách Hồ sơ học sinh
  const [pageIndex, setPageIndex] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(10);

  // Bộ lọc cho tab Nhật ký biến động
  const [logFilter, setLogFilter] = useState<'ALL' | 'AutoCancelled' | 'ParentCancelled' | 'Restored'>('ALL');

  // Modal Chốt suất ăn (08h15)
  const [showLockModal, setShowLockModal] = useState<boolean>(false);
  const [lockNotes, setLockNotes] = useState<string>(
    `Đã kiểm tra đối chiếu danh sách điểm danh và đơn nghỉ lúc ${LUNCH_CUTOFF_LABEL}`
  );

  // Modal In phiếu bàn giao
  const [showPrintModal, setShowPrintModal] = useState<boolean>(false);

  // Modal UC-32: Cấu hình ngày phục vụ, hạn đăng ký/hủy & thực đơn
  const [showConfigModal, setShowConfigModal] = useState<boolean>(false);
  const [cfgIsServing, setCfgIsServing] = useState<boolean>(true);
  const [cfgCutoffTime, setCfgCutoffTime] = useState<string>(LUNCH_CUTOFF_DEFAULT_TIME);
  const [cfgRemarks, setCfgRemarks] = useState<string>('');
  const [cfgApplyFullWeek, setCfgApplyFullWeek] = useState<boolean>(false);

  const fetchBoard = async (date: string) => {
    try {
      setLoading(true);
      const data = await lunchApi.getDailyMealBoard(date);
      setBoard(data);
      setCfgIsServing(data.isServing ?? true);
      setCfgCutoffTime(data.cutoffTime || LUNCH_CUTOFF_DEFAULT_TIME);
      setCfgRemarks(data.serviceRemarks || '');
    } catch {
      setMessage({ text: 'Không thể tải bảng tổng hợp suất ăn bán trú.', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchBoard(selectedDate);
  }, [selectedDate]);

  const effectiveCutoffLabel = useMemo(() => {
    if (!board?.cutoffTime) return LUNCH_CUTOFF_LABEL;
    return board.cutoffTime.replace(':', 'h');
  }, [board?.cutoffTime]);

  // Lọc danh sách học sinh
  const filteredStudentMeals = useMemo(() => {
    const list = board?.studentMeals ?? [];
    return list.filter((item) => {
      if (selectedClassFilter !== 'ALL' && String(item.classId) !== selectedClassFilter) {
        return false;
      }
      if (statusFilter === 'ACTIVE' && !item.isActive) return false;
      if (statusFilter === 'CANCELLED' && item.isActive) return false;

      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const matchName = item.studentName.toLowerCase().includes(q);
        const matchCode = item.studentCode.toLowerCase().includes(q);
        const matchClass = item.className.toLowerCase().includes(q);
        if (!matchName && !matchCode && !matchClass) return false;
      }

      return true;
    });
  }, [board?.studentMeals, selectedClassFilter, statusFilter, searchQuery]);

  // Reset trang về 1 khi đổi bộ lọc
  useEffect(() => {
    setPageIndex(1);
  }, [selectedClassFilter, statusFilter, searchQuery, selectedDate, pageSize]);

  // Phân trang dữ liệu học sinh
  const totalCount = filteredStudentMeals.length;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const pagedStudents = useMemo(() => {
    const start = (pageIndex - 1) * pageSize;
    return filteredStudentMeals.slice(start, start + pageSize);
  }, [filteredStudentMeals, pageIndex, pageSize]);

  // Lọc nhật ký biến động
  const filteredLogs = useMemo(() => {
    const logs = board?.mealChangeLogs ?? [];
    if (logFilter === 'ALL') return logs;
    return logs.filter((l) => l.changeType === logFilter);
  }, [board?.mealChangeLogs, logFilter]);

  const handleTabChange = (tab: 'summary' | 'students' | 'logs') => {
    setSearchParams({ tab });
  };

  const handleSaveConfig = async () => {
    try {
      setActionLoading(true);
      const targetDates = cfgApplyFullWeek ? getWeekdaysOfDate(selectedDate) : [selectedDate];
      const res = await lunchApi.upsertServiceDays({
        dates: targetDates,
        isServing: cfgIsServing,
        cutoffTime: cfgCutoffTime,
        remarks: cfgRemarks.trim() || undefined,
      });

      const msg = (res.code && getSystemMessage(res.code)?.content) || res.message || SYSTEM_MESSAGES.MSG44.content;
      toast.success(msg);
      setMessage({ text: `${msg} (Áp dụng cho ${targetDates.length} ngày)`, type: 'success' });
      setShowConfigModal(false);
      await fetchBoard(selectedDate);
    } catch (err: unknown) {
      const apiErr = (err as { response?: { data?: { code?: string; message?: string; errors?: string[] } } })?.response?.data;
      const sysMsg = apiErr?.code ? getSystemMessage(apiErr.code) : undefined;
      const errText = apiErr?.errors?.[0] || sysMsg?.content || apiErr?.message || 'Lưu cấu hình ngày phục vụ thất bại.';
      toast.error(errText);
    } finally {
      setActionLoading(false);
    }
  };

  const handleLockDay = async () => {
    try {
      setActionLoading(true);
      await lunchApi.lockDailyMeal({
        date: selectedDate,
        notes: lockNotes,
      });
      setMessage({
        text: `Đã chốt số liệu suất ăn gửi nhà bếp thành công (Mốc ${effectiveCutoffLabel})!`,
        type: 'success',
      });
      setShowLockModal(false);
      await fetchBoard(selectedDate);
    } catch (err: unknown) {
      const errorMsg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Chốt suất ăn thất bại.';
      setMessage({ text: errorMsg, type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleExportCsv = () => {
    if (!board) return;
    const rows: string[][] = [
      ['STT', 'Ma hoc sinh', 'Ho va ten', 'Lop', 'Khoi', 'Loai suat an', 'Trang thai', 'Ghi chu / Ly do huy'],
    ];

    filteredStudentMeals.forEach((s, idx) => {
      rows.push([
        String(idx + 1),
        s.studentCode,
        s.studentName,
        s.className,
        String(s.grade),
        MEAL_TYPE_TEXT[String(s.mealType)] ?? 'Tiêu chuẩn',
        s.isActive ? 'Còn hiệu lực (Thực ăn)' : 'Đã hủy',
        (s.isActive ? s.remarks : s.cancellationReason) || '',
      ]);
    });

    const csvContent =
      '\uFEFF' +
      rows
        .map((r) => r.map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(','))
        .join('\n');

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Danh_sach_ban_tru_${selectedDate}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    toast.success('Đã xuất danh sách vận hành bán trú (.csv) thành công.');
  };

  return (
    <div className="container pop-in" style={{ paddingBottom: 60 }}>
      {/* Decorative Memphis shapes */}
      <span className="decor decor--circle" style={{ top: -20, right: 30 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 30, left: 10 }} aria-hidden="true" />
      <span className="decor decor--square" style={{ top: 160, right: -15 }} aria-hidden="true" />

      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
              <span className="badge badge--mint">
                <Sparkles size={14} /> Tổ Bán Trú & Bếp Ăn (UC-32 & UC-33)
              </span>
              {board && !board.canManage && (
                <span className="badge badge--violet">Chế độ Xem báo cáo (Theo phân quyền)</span>
              )}
            </div>
            <h1 style={{ margin: '4px 0 8px' }}>
              Báo Cáo <span className="squiggle">Suất Ăn Bếp</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)' }}>
              Theo dõi biến động và chốt suất ăn bán trú hàng ngày theo mốc cắt cơm {effectiveCutoffLabel}.
            </p>
          </div>

          {/* Controls: Date Picker & Quick Action buttons */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            <div className="field" style={{ minWidth: 160 }}>
              <input
                type="date"
                className="input"
                value={selectedDate}
                onChange={(e) => setSelectedDate(e.target.value)}
                style={{ fontWeight: 700 }}
              />
            </div>

            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => fetchBoard(selectedDate)}
              title="Làm mới dữ liệu"
              disabled={loading}
            >
              <RefreshCw size={16} className={loading ? 'spin' : ''} />
              Làm mới
            </button>

            {board?.canManage && (
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => setShowConfigModal(true)}
                title="Cấu hình mở/đóng phục vụ & thực đơn ngày (UC-32)"
              >
                <Settings size={16} />
                Cấu hình ngày
              </button>
            )}

            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowPrintModal(true)}
              title="In phiếu bàn giao cho bếp"
            >
              <Printer size={16} />
              In Phiếu Giao
            </button>

            {board?.canManage && board.isServing && !board.isLocked && (
              <button
                type="button"
                className="btn btn--primary"
                onClick={() => setShowLockModal(true)}
              >
                <Lock size={16} />
                Chốt Suất ({effectiveCutoffLabel})
              </button>
            )}
          </div>
        </div>
      </header>

      {/* Global alert toast */}
      {message && (
        <div
          className={`card card--featured`}
          style={{
            marginBottom: 20,
            background: message.type === 'success' ? 'var(--color-quaternary-soft)' : 'var(--color-secondary-soft)',
            borderColor: message.type === 'success' ? 'var(--color-quaternary)' : 'var(--color-secondary)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '12px 18px',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, fontWeight: 700 }}>
            {message.type === 'success' ? (
              <CheckCircle2 size={18} color="#047857" />
            ) : (
              <ShieldAlert size={18} color="#e11d48" />
            )}
            <span style={{ color: message.type === 'success' ? '#065f46' : '#9f1239' }}>{message.text}</span>
          </div>
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={() => setMessage(null)}
          >
            Đóng
          </button>
        </div>
      )}

      {/* TAB NAVIGATION PILLS (Tách thông tin thành các màn hình độc lập) */}
      <div
        style={{
          display: 'flex',
          gap: 10,
          marginBottom: 24,
          flexWrap: 'wrap',
          background: 'var(--color-card)',
          padding: '8px 12px',
          borderRadius: 'var(--radius-md)',
          border: 'var(--border)',
          boxShadow: 'var(--shadow-card)',
        }}
      >
        <button
          type="button"
          className={`btn ${activeTab === 'summary' ? 'btn--primary' : 'btn--secondary'}`}
          onClick={() => handleTabChange('summary')}
          style={{ borderRadius: 'var(--radius-full)' }}
        >
          <FileSpreadsheet size={16} />
          1. Tổng Hợp & Chốt Suất
        </button>

        <button
          type="button"
          className={`btn ${activeTab === 'students' ? 'btn--primary' : 'btn--secondary'}`}
          onClick={() => handleTabChange('students')}
          style={{ borderRadius: 'var(--radius-full)' }}
        >
          <Users size={16} />
          2. Danh Sách Học Sinh Nhận Suất ({totalCount})
        </button>

        <button
          type="button"
          className={`btn ${activeTab === 'logs' ? 'btn--primary' : 'btn--secondary'}`}
          onClick={() => handleTabChange('logs')}
          style={{ borderRadius: 'var(--radius-full)' }}
        >
          <Clock size={16} />
          3. Nhật Ký Biến Động ({board?.mealChangeLogs?.length || 0})
        </button>
      </div>

      {/* ===================================================================== */}
      {/* MÀN HÌNH 1: TỔNG HỢP & CHỐT SUẤT BÁN TRÚ                               */}
      {/* ===================================================================== */}
      {activeTab === 'summary' && (
        <div className="pop-in">
          {/* Banner Cutoff Time */}
          {board && (
            <div
              className="card card--featured"
              style={{
                marginBottom: 24,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                flexWrap: 'wrap',
                gap: 16,
                background: !board.isServing
                  ? 'var(--color-secondary-soft)'
                  : board.isLocked
                    ? 'var(--color-quaternary-soft)'
                    : 'var(--color-tertiary-soft)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
                <div
                  style={{
                    width: 44,
                    height: 44,
                    borderRadius: '50%',
                    background: !board.isServing
                      ? 'var(--color-secondary)'
                      : board.isLocked
                        ? 'var(--color-quaternary)'
                        : 'var(--color-tertiary)',
                    border: 'var(--border)',
                    boxShadow: 'var(--shadow-pop-active)',
                    display: 'grid',
                    placeItems: 'center',
                  }}
                >
                  <Clock size={22} color="var(--color-fg)" />
                </div>
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <h3 style={{ margin: 0, fontSize: 'var(--fs-base)', fontFamily: 'var(--font-heading)' }}>
                      Quy tắc chốt ăn Tân Lập A: 08h15
                    </h3>
                    <span
                      className={`badge ${
                        !board.isServing ? 'badge--pink' : board.isLocked ? 'badge--mint' : 'badge--amber'
                      }`}
                    >
                      {!board.isServing
                        ? 'Đóng dịch vụ ngày'
                        : board.isLocked
                          ? `Đã khóa số liệu (${effectiveCutoffLabel})`
                          : `Hạn đăng ký/hủy: ${effectiveCutoffLabel}`}
                    </span>
                  </div>
                  <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
                    {board.serviceRemarks ? (
                      <>
                        <strong>Thực đơn / Ghi chú ngày:</strong> {board.serviceRemarks}
                      </>
                    ) : (
                      `Đơn xin nghỉ học có bữa trưa được GVCN duyệt trước ${effectiveCutoffLabel} sẽ tự động giảm trừ suất ăn.`
                    )}
                  </p>
                </div>
              </div>

              <div style={{ display: 'flex', gap: 10 }}>
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  onClick={() => handleTabChange('students')}
                >
                  <Users size={14} />
                  Xem danh sách học sinh
                  <ArrowRight size={14} />
                </button>
              </div>
            </div>
          )}

          {/* 4 Live KPI Cards */}
          {board && (
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(230px, 1fr))',
                gap: 20,
                marginBottom: 28,
              }}
            >
              {/* Card 1: Registered Total */}
              <div className="card card--hover" style={{ position: 'relative', paddingTop: 28 }}>
                <div
                  style={{
                    position: 'absolute',
                    top: -20,
                    left: 20,
                    width: 42,
                    height: 42,
                    borderRadius: '50%',
                    background: 'var(--color-tertiary)',
                    border: 'var(--border)',
                    boxShadow: 'var(--shadow-pop-active)',
                    display: 'grid',
                    placeItems: 'center',
                  }}
                >
                  <UtensilsCrossed size={18} color="var(--color-fg)" />
                </div>
                <div className="label" style={{ marginBottom: 4 }}>
                  Tổng bản ghi đăng ký ngày
                </div>
                <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 800, color: 'var(--color-fg)', fontFamily: 'var(--font-heading)' }}>
                  {board.totalInitialRegistered}{' '}
                  <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>suất</span>
                </div>
                <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Đăng ký bán trú hợp lệ theo ngày (UC-29)
                </p>
              </div>

              {/* Card 2: Cancelled */}
              <div className="card card--hover" style={{ position: 'relative', paddingTop: 28 }}>
                <div
                  style={{
                    position: 'absolute',
                    top: -20,
                    left: 20,
                    width: 42,
                    height: 42,
                    borderRadius: '50%',
                    background: 'var(--color-secondary)',
                    border: 'var(--border)',
                    boxShadow: 'var(--shadow-pop-active)',
                    display: 'grid',
                    placeItems: 'center',
                  }}
                >
                  <UserMinus size={18} color="#FFFFFF" />
                </div>
                <div className="label" style={{ marginBottom: 4 }}>
                  Đã hủy trước {effectiveCutoffLabel}
                </div>
                <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 800, color: '#e11d48', fontFamily: 'var(--font-heading)' }}>
                  -{board.totalAutoCancelled}{' '}
                  <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>suất</span>
                </div>
                <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: '#e11d48' }}>
                  PH hủy trực tiếp & Tự hủy từ đơn xin nghỉ
                </p>
              </div>

              {/* Card 3: Net Active To Cook */}
              <div
                className="card card--featured card--hover"
                style={{
                  position: 'relative',
                  paddingTop: 28,
                  background: 'var(--color-quaternary-soft)',
                }}
              >
                <div
                  style={{
                    position: 'absolute',
                    top: -20,
                    left: 20,
                    width: 42,
                    height: 42,
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
                <div className="label" style={{ marginBottom: 4, color: '#047857' }}>
                  Tổng suất còn hiệu lực (Thực nấu)
                </div>
                <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 900, color: '#065f46', fontFamily: 'var(--font-heading)' }}>
                  {board.totalActiveToCook}{' '}
                  <span style={{ fontSize: 'var(--fs-md)', fontWeight: 700, color: '#047857' }}>khẩu phần</span>
                </div>
                <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: '#047857', fontWeight: 600 }}>
                  = Đăng ký ({board.totalInitialRegistered}) - Đã hủy ({board.totalAutoCancelled})
                </p>
              </div>

              {/* Card 4: Anomalies */}
              <div className="card card--hover" style={{ position: 'relative', paddingTop: 28 }}>
                <div
                  style={{
                    position: 'absolute',
                    top: -20,
                    left: 20,
                    width: 42,
                    height: 42,
                    borderRadius: '50%',
                    background: 'var(--color-accent)',
                    border: 'var(--border)',
                    boxShadow: 'var(--shadow-pop-active)',
                    display: 'grid',
                    placeItems: 'center',
                  }}
                >
                  <ShieldAlert size={18} color="#FFFFFF" />
                </div>
                <div className="label" style={{ marginBottom: 4 }}>
                  Phát sinh bất thường
                </div>
                <div
                  style={{
                    fontSize: 'var(--fs-2xl)',
                    fontWeight: 800,
                    color: board.totalAnomalies > 0 ? 'var(--color-accent)' : 'var(--color-fg)',
                    fontFamily: 'var(--font-heading)',
                  }}
                >
                  {board.totalAnomalies}{' '}
                  <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>em</span>
                </div>
                <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: board.totalAnomalies > 0 ? 'var(--color-accent)' : 'var(--color-muted-fg)' }}>
                  Suất đã hủy nhưng GVCN điểm danh Có mặt
                </p>
              </div>
            </div>
          )}

          {/* Class Breakdown Table */}
          <div className="card" style={{ marginBottom: 28 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <div>
                <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
                  <FileSpreadsheet size={18} color="var(--color-accent)" />
                  Tổng hợp số suất nhóm theo Lớp học (UC-33)
                </h3>
                <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Bấm vào hàng của một lớp để chuyển nhanh sang danh sách học sinh của lớp đó.
                </p>
              </div>
              <span className="badge badge--muted">
                {board?.classBreakdowns?.length || 0} lớp học
              </span>
            </div>

            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                <thead>
                  <tr style={{ borderBottom: 'var(--border)', background: 'var(--color-muted)' }}>
                    <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 700 }}>Lớp</th>
                    <th style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 700 }}>Khối</th>
                    <th style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 700 }}>Đăng ký</th>
                    <th style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 700 }}>Đã hủy</th>
                    <th style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 700 }}>Thực ăn</th>
                    <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 700 }}>Tỷ lệ nhận</th>
                    <th style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 700 }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {board?.classBreakdowns && board.classBreakdowns.length > 0 ? (
                    board.classBreakdowns.map((cls) => {
                      const percentage = cls.initialRegistered > 0
                        ? Math.round((cls.actualCount / cls.initialRegistered) * 100)
                        : 0;

                      return (
                        <tr
                          key={cls.classId}
                          style={{ borderBottom: '1px solid var(--color-border)' }}
                        >
                          <td style={{ padding: '12px', fontWeight: 700, color: 'var(--color-accent)' }}>
                            Lớp {cls.className}
                          </td>
                          <td style={{ padding: '12px', textAlign: 'center' }}>
                            Khối {cls.grade}
                          </td>
                          <td style={{ padding: '12px', textAlign: 'center', fontWeight: 600 }}>
                            {cls.initialRegistered}
                          </td>
                          <td
                            style={{
                              padding: '12px',
                              textAlign: 'center',
                              color: cls.cancelledCount > 0 ? '#e11d48' : 'var(--color-muted-fg)',
                              fontWeight: cls.cancelledCount > 0 ? 700 : 400,
                            }}
                          >
                            {cls.cancelledCount > 0 ? `-${cls.cancelledCount}` : '0'}
                          </td>
                          <td style={{ padding: '12px', textAlign: 'center', fontWeight: 800, color: '#047857', fontSize: 'var(--fs-base)' }}>
                            {cls.actualCount}
                          </td>
                          <td style={{ padding: '12px' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                              <div style={{ flex: 1, height: 8, background: 'var(--color-border)', borderRadius: 4, overflow: 'hidden' }}>
                                <div
                                  style={{
                                    width: `${percentage}%`,
                                    height: '100%',
                                    background: percentage === 100 ? 'var(--color-quaternary)' : 'var(--color-tertiary)',
                                  }}
                                />
                              </div>
                              <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', minWidth: 32 }}>
                                {percentage}%
                              </span>
                            </div>
                          </td>
                          <td style={{ padding: '12px', textAlign: 'center' }}>
                            <button
                              type="button"
                              className="btn btn--secondary btn--sm"
                              onClick={() => {
                                setSelectedClassFilter(String(cls.classId));
                                handleTabChange('students');
                              }}
                              title={`Xem danh sách học sinh lớp ${cls.className}`}
                            >
                              Xem danh sách →
                            </button>
                          </td>
                        </tr>
                      );
                    })
                  ) : (
                    <tr>
                      <td colSpan={7} style={{ textAlign: 'center', padding: '32px', color: 'var(--color-muted-fg)' }}>
                        Chưa có dữ liệu phân bổ cho ngày này.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* ===================================================================== */}
      {/* MÀN HÌNH 2: DANH SÁCH HỌC SINH NHẬN SUẤT (UC-33) + PAGINATION         */}
      {/* ===================================================================== */}
      {activeTab === 'students' && (
        <div className="card pop-in">
          {/* Header & Filter Controls */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16, marginBottom: 20 }}>
            <div>
              <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
                <Users size={18} color="var(--color-accent)" />
                Danh sách tối thiểu phục vụ vận hành theo ngày
              </h3>
              <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                Phục vụ nhân viên bán trú và giáo viên đối chiếu phát khay ăn theo từng lớp (UC-33).
              </p>
            </div>

            {/* Action buttons */}
            <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={handleExportCsv}
                title="Xuất danh sách học sinh ra file CSV/Excel"
              >
                <Download size={15} />
                Xuất CSV
              </button>
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={() => setShowPrintModal(true)}
              >
                <Printer size={15} />
                In Phiếu
              </button>
            </div>
          </div>

          {/* Search & Filter Bar */}
          <div
            style={{
              display: 'flex',
              gap: 12,
              flexWrap: 'wrap',
              alignItems: 'center',
              padding: '12px 16px',
              background: 'var(--color-muted)',
              borderRadius: 'var(--radius-sm)',
              marginBottom: 20,
              border: '1px solid var(--color-border)',
            }}
          >
            {/* Search Input */}
            <div style={{ position: 'relative', flex: 1, minWidth: 220 }}>
              <Search
                size={16}
                color="var(--color-muted-fg)"
                style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)' }}
              />
              <input
                type="text"
                className="input"
                placeholder="Tìm tên hoặc mã học sinh..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                style={{ paddingLeft: 36, fontSize: 'var(--fs-sm)' }}
              />
            </div>

            {/* Class Filter */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <Filter size={15} color="var(--color-muted-fg)" />
              <select
                className="select"
                value={selectedClassFilter}
                onChange={(e) => setSelectedClassFilter(e.target.value)}
                style={{ minWidth: 160, padding: '6px 12px', fontSize: 'var(--fs-sm)' }}
              >
                <option value="ALL">Tất cả các lớp</option>
                {board?.classBreakdowns?.map((c) => (
                  <option key={c.classId} value={String(c.classId)}>
                    Lớp {c.className} (Khối {c.grade})
                  </option>
                ))}
              </select>
            </div>

            {/* Status Filter */}
            <select
              className="select"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as 'ALL' | 'ACTIVE' | 'CANCELLED')}
              style={{ minWidth: 190, padding: '6px 12px', fontSize: 'var(--fs-sm)' }}
            >
              <option value="ACTIVE">Đang còn hiệu lực (Thực ăn)</option>
              <option value="CANCELLED">Đã hủy trong ngày</option>
              <option value="ALL">Tất cả bản ghi đăng ký</option>
            </select>

            {/* Page Size Select */}
            <select
              className="select"
              value={pageSize}
              onChange={(e) => setPageSize(Number(e.target.value))}
              style={{ minWidth: 120, padding: '6px 12px', fontSize: 'var(--fs-sm)' }}
            >
              <option value={10}>10 dòng / trang</option>
              <option value={20}>20 dòng / trang</option>
              <option value={50}>50 dòng / trang</option>
            </select>
          </div>

          {/* Quick status counters */}
          <div style={{ display: 'flex', gap: 8, marginBottom: 16, alignItems: 'center', flexWrap: 'wrap' }}>
            <span className="badge badge--violet">
              Tổng tìm thấy: <strong>{totalCount}</strong> học sinh
            </span>
            <span className="badge badge--mint">
              Thực ăn: <strong>{filteredStudentMeals.filter((s) => s.isActive).length}</strong>
            </span>
            <span className="badge badge--pink">
              Đã hủy: <strong>{filteredStudentMeals.filter((s) => !s.isActive).length}</strong>
            </span>
            {searchQuery && (
              <span className="badge badge--amber">
                Từ khóa: &quot;{searchQuery}&quot;
              </span>
            )}
          </div>

          {/* Student Table */}
          <div style={{ overflowX: 'auto', border: 'var(--border)', borderRadius: 'var(--radius-sm)' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
              <thead>
                <tr style={{ borderBottom: 'var(--border)', background: 'var(--color-muted)' }}>
                  <th style={{ padding: '10px 12px', textAlign: 'center', width: 56 }}>STT</th>
                  <th style={{ padding: '10px 12px', textAlign: 'left' }}>Mã HS</th>
                  <th style={{ padding: '10px 12px', textAlign: 'left' }}>Họ và tên học sinh</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Lớp</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Chế độ ăn</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Trạng thái</th>
                  <th style={{ padding: '10px 12px', textAlign: 'left' }}>Ghi chú / Lý do hủy</th>
                </tr>
              </thead>
              <tbody>
                {pagedStudents.length > 0 ? (
                  pagedStudents.map((item, idx) => (
                    <tr key={item.registrationId} style={{ borderBottom: '1px solid var(--color-border)' }}>
                      <td style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 600 }}>
                        {(pageIndex - 1) * pageSize + idx + 1}
                      </td>
                      <td style={{ padding: '10px 12px', fontWeight: 700, color: 'var(--color-accent)' }}>
                        {item.studentCode}
                      </td>
                      <td style={{ padding: '10px 12px', fontWeight: 700 }}>{item.studentName}</td>
                      <td style={{ padding: '10px 12px', textAlign: 'center' }}>Lớp {item.className}</td>
                      <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                        <span className="badge badge--amber">
                          {MEAL_TYPE_TEXT[String(item.mealType)] ?? 'Tiêu chuẩn'}
                        </span>
                      </td>
                      <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                        {item.isActive ? (
                          <span className="badge badge--mint">Thực ăn</span>
                        ) : (
                          <span className="badge badge--pink">Đã hủy</span>
                        )}
                      </td>
                      <td style={{ padding: '10px 12px', color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>
                        {(item.isActive ? item.remarks : item.cancellationReason) || '—'}
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={7} style={{ textAlign: 'center', padding: '36px', color: 'var(--color-muted-fg)' }}>
                      {SYSTEM_MESSAGES.MSG43.content}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>

            {/* PAGINATION BAR (Chuẩn hóa như màn hình Hồ sơ học sinh - admin) */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '12px 18px',
                borderTop: 'var(--border)',
                background: 'var(--color-muted)',
                fontSize: 'var(--fs-sm)',
                flexWrap: 'wrap',
                gap: 12,
              }}
            >
              <div>
                Hiển thị <strong>{pagedStudents.length}</strong> / <strong>{totalCount}</strong> học sinh
                {totalCount > 0 && (
                  <span style={{ color: 'var(--color-muted-fg)', marginLeft: 8 }}>
                    (Bản ghi {(pageIndex - 1) * pageSize + 1} – {Math.min(pageIndex * pageSize, totalCount)})
                  </span>
                )}
              </div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  disabled={pageIndex <= 1}
                  onClick={() => setPageIndex((p) => Math.max(1, p - 1))}
                >
                  <ChevronLeft size={14} /> Trang trước
                </button>
                <span className="badge badge--violet" style={{ alignSelf: 'center', padding: '4px 10px' }}>
                  Trang {pageIndex} / {totalPages}
                </span>
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  disabled={pageIndex >= totalPages}
                  onClick={() => setPageIndex((p) => Math.min(totalPages, p + 1))}
                >
                  Trang sau <ChevronRight size={14} />
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ===================================================================== */}
      {/* MÀN HÌNH 3: NHẬT KÝ BIẾN ĐỘNG SUẤT ĂN (WFL 1.2.3 & UC-30)              */}
      {/* ===================================================================== */}
      {activeTab === 'logs' && (
        <div className="card pop-in">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12, marginBottom: 20 }}>
            <div>
              <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
                <Clock size={18} color="var(--color-secondary)" />
                Nhật ký biến động suất ăn (UC-30 & WFL 1.2.3)
              </h3>
              <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                Truy vết toàn bộ việc hủy suất tự động trước {effectiveCutoffLabel}, phụ huynh hủy trực tiếp và khôi phục suất ăn.
              </p>
            </div>

            {/* Filter logs */}
            <div style={{ display: 'flex', gap: 8 }}>
              <select
                className="select"
                value={logFilter}
                onChange={(e) => setLogFilter(e.target.value as 'ALL' | 'AutoCancelled' | 'ParentCancelled' | 'Restored')}
                style={{ minWidth: 200, padding: '6px 12px', fontSize: 'var(--fs-sm)' }}
              >
                <option value="ALL">Tất cả loại biến động ({board?.mealChangeLogs?.length || 0})</option>
                <option value="AutoCancelled">Tự hủy từ đơn xin nghỉ</option>
                <option value="ParentCancelled">Phụ huynh hủy trực tiếp (UC-30)</option>
                <option value="Restored">Khôi phục suất ăn</option>
              </select>
            </div>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {filteredLogs.length > 0 ? (
              filteredLogs.map((log, idx) => {
                const isCancel = log.changeType === 'AutoCancelled' || log.changeType === 'ParentCancelled';
                return (
                  <div
                    key={idx}
                    style={{
                      padding: '14px 18px',
                      borderRadius: 'var(--radius-sm)',
                      border: 'var(--border)',
                      boxShadow: 'var(--shadow-pop-active)',
                      background: isCancel ? 'var(--color-secondary-soft)' : 'var(--color-quaternary-soft)',
                      fontSize: 'var(--fs-sm)',
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 6 }}>
                      <div>
                        <strong style={{ fontSize: 'var(--fs-base)', color: 'var(--color-fg)' }}>
                          {log.studentName}
                        </strong>
                        <span style={{ marginLeft: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
                          ({log.studentCode}) — Lớp {log.className}
                        </span>
                      </div>
                      <span className={`badge ${isCancel ? 'badge--pink' : 'badge--mint'}`}>
                        {log.changeType === 'ParentCancelled'
                          ? 'PH hủy trực tiếp (UC-30)'
                          : log.changeType === 'AutoCancelled'
                            ? `Tự hủy trước ${effectiveCutoffLabel}`
                            : 'Khôi phục suất'}
                      </span>
                    </div>

                    <p style={{ margin: '6px 0', color: 'var(--color-fg)', fontSize: 'var(--fs-sm)', fontStyle: 'italic' }}>
                      Lý do: {log.reason}
                    </p>

                    <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 8, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                      <span>Thời điểm ghi nhận: {new Date(log.timestamp).toLocaleTimeString('vi-VN')}</span>
                      <span style={{ color: '#059669', fontWeight: 700 }}>✓ Trước giờ chốt {effectiveCutoffLabel}</span>
                    </div>
                  </div>
                );
              })
            ) : (
              <div style={{ textAlign: 'center', padding: '48px 20px', color: 'var(--color-muted-fg)' }}>
                <UtensilsCrossed size={36} color="var(--color-border)" style={{ marginBottom: 12 }} />
                <p style={{ margin: 0, fontWeight: 600 }}>Không có dữ liệu biến động suất ăn nào cho ngày này.</p>
              </div>
            )}
          </div>
        </div>
      )}

      {/* ===================================================================== */}
      {/* MODAL UC-32: Cấu hình Ngày phục vụ, Hạn đăng ký/hủy & Thực đơn        */}
      {/* ===================================================================== */}
      <GeometricModal
        open={showConfigModal}
        onClose={() => setShowConfigModal(false)}
        title={`Cấu hình Ngày Phục Vụ Bán Trú (UC-32)`}
        badgeText={`Ngày ${new Date(selectedDate).toLocaleDateString('vi-VN')}`}
        badgeColor="mint"
        icon={<Settings size={22} color="var(--color-fg)" />}
        iconBg="var(--color-quaternary)"
        maxWidth="md"
      >
        <div>
          <div className="field" style={{ marginBottom: 14 }}>
            <label className="label" htmlFor="cfg-serving">
              Trạng thái phục vụ ngày {new Date(selectedDate).toLocaleDateString('vi-VN')}
            </label>
            <select
              id="cfg-serving"
              className="select"
              value={cfgIsServing ? 'OPEN' : 'CLOSED'}
              onChange={(e) => setCfgIsServing(e.target.value === 'OPEN')}
            >
              <option value="OPEN">🟢 Có phục vụ ăn bán trú (Mở đăng ký)</option>
              <option value="CLOSED">🔴 Không phục vụ (Nghỉ lễ / Ngoại khóa / Nghỉ bếp)</option>
            </select>
          </div>

          <div className="field" style={{ marginBottom: 14 }}>
            <label className="label" htmlFor="cfg-cutoff">
              Hạn đăng ký / hủy suất ăn trong ngày (Quy tắc Tân Lập A: 08:15)
            </label>
            <input
              id="cfg-cutoff"
              type="time"
              className="input"
              value={cfgCutoffTime}
              onChange={(e) => setCfgCutoffTime(e.target.value)}
            />
          </div>

          <div className="field" style={{ marginBottom: 14 }}>
            <label className="label" htmlFor="cfg-remarks">
              Thực đơn trưa hoặc Ghi chú phục vụ (hiển thị cho Phụ huynh)
            </label>
            <textarea
              id="cfg-remarks"
              className="input"
              rows={3}
              placeholder="VD: Cơm trắng, Thịt gà kho nấm, Canh bí đỏ nấu tôm, Chuối tiêu..."
              value={cfgRemarks}
              onChange={(e) => setCfgRemarks(e.target.value)}
              style={{ resize: 'vertical', minHeight: 76 }}
            />
          </div>

          <label
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 10,
              padding: '10px 14px',
              background: 'var(--color-muted)',
              borderRadius: 'var(--radius-sm)',
              border: '1px solid var(--color-border)',
              cursor: 'pointer',
              marginBottom: 20,
              fontSize: 'var(--fs-sm)',
              fontWeight: 600,
            }}
          >
            <input
              type="checkbox"
              checked={cfgApplyFullWeek}
              onChange={(e) => setCfgApplyFullWeek(e.target.checked)}
            />
            <span>Áp dụng cấu hình này cho cả tuần làm việc (Thứ 2 – Thứ 6 của tuần đang chọn)</span>
          </label>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowConfigModal(false)}
              disabled={actionLoading}
            >
              Hủy
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleSaveConfig}
              disabled={actionLoading}
            >
              <CheckCircle2 size={16} />
              {actionLoading ? 'Đang lưu...' : 'Lưu cấu hình (UC-32)'}
            </button>
          </div>
        </div>
      </GeometricModal>

      {/* ===================================================================== */}
      {/* MODAL 1: Confirm Lock Headcount                                       */}
      {/* ===================================================================== */}
      <GeometricModal
        open={showLockModal}
        onClose={() => setShowLockModal(false)}
        title={`Chốt số liệu Bán trú lúc ${effectiveCutoffLabel}`}
        badgeText="Khóa số liệu nhà bếp"
        badgeColor="amber"
        icon={<Lock size={24} color="var(--color-fg)" />}
        iconBg="var(--color-tertiary)"
        maxWidth="md"
      >
        <div>
          <div
            className="card"
            style={{
              background: 'var(--color-muted)',
              padding: '14px 18px',
              marginBottom: 16,
              fontSize: 'var(--fs-sm)',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
              <span>Tổng suất cần nấu hôm nay:</span>
              <strong style={{ color: '#047857', fontSize: 'var(--fs-base)' }}>{board?.totalActiveToCook} suất</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', color: '#e11d48' }}>
              <span>Tổng suất hủy hợp lệ:</span>
              <strong>-{board?.totalAutoCancelled} suất</strong>
            </div>
          </div>

          <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)', marginBottom: 16, lineHeight: 1.5 }}>
            Sau khi bấm chốt, hệ thống sẽ <strong>khóa bảng số liệu</strong> ngày hôm nay. Các đơn nghỉ hoặc yêu cầu hủy gửi muộn sau {effectiveCutoffLabel} sẽ giữ nguyên đăng ký hiện tại theo quy định.
          </p>

          <div className="field" style={{ marginBottom: 20 }}>
            <label className="label" htmlFor="lock-notes">Ghi chú gửi Nhà bếp / Kế toán:</label>
            <textarea
              id="lock-notes"
              className="input"
              value={lockNotes}
              onChange={(e) => setLockNotes(e.target.value)}
              rows={3}
              style={{ minHeight: 80, resize: 'vertical' }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowLockModal(false)}
              disabled={actionLoading}
            >
              Hủy bỏ
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleLockDay}
              disabled={actionLoading}
            >
              <Lock size={16} />
              {actionLoading ? 'Đang chốt...' : 'Xác nhận chốt số liệu'}
            </button>
          </div>
        </div>
      </GeometricModal>

      {/* ===================================================================== */}
      {/* MODAL 2: Printable Handover Sheet                                     */}
      {/* ===================================================================== */}
      <GeometricModal
        open={showPrintModal}
        onClose={() => setShowPrintModal(false)}
        title="Phiếu bàn giao suất ăn bán trú cho nhà bếp"
        badgeText="In biên bản bàn giao"
        badgeColor="mint"
        icon={<Printer size={24} color="var(--color-fg)" />}
        iconBg="var(--color-quaternary)"
        maxWidth="lg"
      >
        <div style={{ background: '#FFFFFF', color: '#1E293B', padding: '16px 8px' }}>
          {/* Header A4 */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', borderBottom: '2px solid #1E293B', paddingBottom: 16, marginBottom: 20 }}>
            <div>
              <div style={{ fontSize: '0.8rem', fontWeight: 700, textTransform: 'uppercase' }}>PHÒNG GD&ĐT HUYỆN ĐAN PHƯỢNG</div>
              <div style={{ fontSize: '0.95rem', fontWeight: 800, textTransform: 'uppercase' }}>TRƯỜNG TIỂU HỌC TÂN LẬP A</div>
              <div style={{ fontSize: '0.75rem', color: '#64748B' }}>Hệ thống Quản lý Bán trú SAWP</div>
            </div>
            <div style={{ textAlign: 'right' }}>
              <div style={{ fontSize: '0.8rem', fontWeight: 700 }}>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
              <div style={{ fontSize: '0.75rem', fontStyle: 'italic' }}>Độc lập — Tự do — Hạnh phúc</div>
              <div style={{ fontSize: '0.8rem', marginTop: 4 }}>Ngày: {new Date(selectedDate).toLocaleDateString('vi-VN')}</div>
            </div>
          </div>

          <div style={{ textAlign: 'center', marginBottom: 20 }}>
            <h2 style={{ margin: '0 0 6px', fontSize: '1.25rem', fontWeight: 900, textTransform: 'uppercase' }}>
              PHIẾU BÀN GIAO SUẤT ĂN BÁN TRÚ CHO NHÀ BẾP
            </h2>
            <div style={{ fontSize: '0.8rem', fontStyle: 'italic', color: '#64748B' }}>
              (Thời điểm chốt số liệu: {effectiveCutoffLabel} sáng | Dữ liệu đồng bộ từ Đăng ký bán trú & Đơn xin nghỉ)
            </div>
          </div>

          {/* Metrics on Paper */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12, marginBottom: 20, textAlign: 'center' }}>
            <div style={{ border: '1px solid #CBD5E1', padding: '10px 8px', borderRadius: 6 }}>
              <div style={{ fontSize: '0.75rem', color: '#64748B' }}>TỔNG ĐĂNG KÝ:</div>
              <div style={{ fontSize: '1.3rem', fontWeight: 800 }}>{board?.totalInitialRegistered}</div>
            </div>
            <div style={{ border: '1px solid #CBD5E1', padding: '10px 8px', borderRadius: 6 }}>
              <div style={{ fontSize: '0.75rem', color: '#64748B' }}>SUẤT HỦY HỢP LỆ:</div>
              <div style={{ fontSize: '1.3rem', fontWeight: 800, color: '#e11d48' }}>-{board?.totalAutoCancelled}</div>
            </div>
            <div style={{ border: '2px solid #1E293B', padding: '10px 8px', borderRadius: 6, background: '#F8FAFC' }}>
              <div style={{ fontSize: '0.75rem', fontWeight: 700 }}>THỰC TẾ NẤU VÀ GIAO:</div>
              <div style={{ fontSize: '1.35rem', fontWeight: 900, color: '#047857' }}>{board?.totalActiveToCook} suất</div>
            </div>
          </div>

          {/* Table for print */}
          <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 24, fontSize: '0.82rem' }}>
            <thead>
              <tr style={{ background: '#F1F5F9' }}>
                <th style={{ border: '1px solid #94A3B8', padding: '6px 8px', textAlign: 'center' }}>STT</th>
                <th style={{ border: '1px solid #94A3B8', padding: '6px 8px', textAlign: 'left' }}>Lớp</th>
                <th style={{ border: '1px solid #94A3B8', padding: '6px 8px', textAlign: 'center' }}>Đăng ký</th>
                <th style={{ border: '1px solid #94A3B8', padding: '6px 8px', textAlign: 'center' }}>Đã hủy</th>
                <th style={{ border: '1px solid #94A3B8', padding: '6px 8px', textAlign: 'center' }}>Thực giao (Khay)</th>
                <th style={{ border: '1px solid #94A3B8', padding: '6px 8px', textAlign: 'left' }}>Ký nhận của GV/Phụ trách</th>
              </tr>
            </thead>
            <tbody>
              {board?.classBreakdowns?.map((c, i) => (
                <tr key={c.classId}>
                  <td style={{ border: '1px solid #CBD5E1', padding: '6px 8px', textAlign: 'center' }}>{i + 1}</td>
                  <td style={{ border: '1px solid #CBD5E1', padding: '6px 8px', fontWeight: 700 }}>Lớp {c.className}</td>
                  <td style={{ border: '1px solid #CBD5E1', padding: '6px 8px', textAlign: 'center' }}>{c.initialRegistered}</td>
                  <td style={{ border: '1px solid #CBD5E1', padding: '6px 8px', textAlign: 'center', color: c.cancelledCount > 0 ? '#e11d48' : '#000' }}>
                    {c.cancelledCount > 0 ? `-${c.cancelledCount}` : '0'}
                  </td>
                  <td style={{ border: '1px solid #CBD5E1', padding: '6px 8px', textAlign: 'center', fontWeight: 800 }}>{c.actualCount}</td>
                  <td style={{ border: '1px solid #CBD5E1', padding: '6px 8px' }}></td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Signatures */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', textAlign: 'center', marginTop: 32 }}>
            <div>
              <div style={{ fontWeight: 700, fontSize: '0.82rem' }}>ĐẠI DIỆN NHÀ BẾP</div>
              <div style={{ fontSize: '0.75rem', fontStyle: 'italic', marginBottom: 40 }}>(Ký và ghi rõ họ tên)</div>
            </div>
            <div>
              <div style={{ fontWeight: 700, fontSize: '0.82rem' }}>NHÂN VIÊN BÁN TRÚ</div>
              <div style={{ fontSize: '0.75rem', fontStyle: 'italic', marginBottom: 40 }}>(Ký và ghi rõ họ tên)</div>
            </div>
            <div>
              <div style={{ fontWeight: 700, fontSize: '0.82rem' }}>BAN GIÁM HIỆU DUYỆT</div>
              <div style={{ fontSize: '0.75rem', fontStyle: 'italic', marginBottom: 40 }}>(Ký và đóng dấu)</div>
            </div>
          </div>

          {/* Print Modal Buttons */}
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 24, borderTop: '1px solid var(--color-border)', paddingTop: 16 }}>
            <button type="button" className="btn btn--secondary" onClick={() => setShowPrintModal(false)}>
              Đóng
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={() => window.print()}
            >
              <Printer size={16} /> In Phiếu Bàn Giao
            </button>
          </div>
        </div>
      </GeometricModal>
    </div>
  );
}
