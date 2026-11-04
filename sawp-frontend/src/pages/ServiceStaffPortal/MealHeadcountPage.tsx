import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router';
import {
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
  CalendarRange,
  Plus,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { lunchApi } from '../../services/implementations/lunchApi';
import type { DailyMealBoard } from '../../types/lunch.types';
import { LUNCH_CUTOFF_DEFAULT_TIME, LUNCH_CUTOFF_LABEL } from '../../constants/lunch';
import { SYSTEM_MESSAGES, getSystemMessage } from '../../constants/messages';
import { GeometricModal } from '../../components/common/GeometricModal';

const MENU_SUGGESTIONS = [
  'Cơm trắng',
  'Gà kho nấm',
  'Thịt kho trứng cút',
  'Sườn xào chua ngọt',
  'Cá phi lê sốt cà',
  'Canh bí đỏ nấu tôm',
  'Canh cua mồng tơi',
  'Chuối tiêu tráng miệng',
  'Sữa chua',
];

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

  const handleAddDish = (dish: string) => {
    if (!cfgRemarks.trim()) {
      setCfgRemarks(dish);
    } else if (!cfgRemarks.includes(dish)) {
      setCfgRemarks(`${cfgRemarks.trim()}, ${dish}`);
    }
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
      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
              <span className="badge badge--mint">
                <Soup size={13} /> Tổ Bán Trú & Bếp Ăn
              </span>
              {board && !board.canManage && (
                <span className="badge badge--violet">Chế độ Xem báo cáo</span>
              )}
            </div>
            <h1 style={{ margin: '4px 0 8px' }}>
              Báo Cáo <span className="squiggle">Suất Ăn Bán Trú</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)' }}>
              Theo dõi biến động và chốt khẩu phần nhà bếp hàng ngày theo mốc cắt cơm {effectiveCutoffLabel}.
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
              style={{ borderRadius: 'var(--radius-full)' }}
            >
              <RefreshCw size={15} className={loading ? 'spin' : ''} />
              Làm mới
            </button>

            {board?.canManage && (
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => setShowConfigModal(true)}
                title="Cấu hình mở/đóng phục vụ & thực đơn ngày"
                style={{ borderRadius: 'var(--radius-full)' }}
              >
                <Settings size={15} />
                Cấu hình ngày
              </button>
            )}

            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowPrintModal(true)}
              title="In phiếu bàn giao cho bếp"
              style={{ borderRadius: 'var(--radius-full)' }}
            >
              <Printer size={15} />
              In Phiếu Giao
            </button>

            {board?.canManage && board.isServing && !board.isLocked && (
              <button
                type="button"
                className="btn btn--primary"
                onClick={() => setShowLockModal(true)}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '4px 4px 0 0 var(--color-fg)',
                  fontWeight: 800,
                  padding: '10px 22px',
                  background: 'var(--color-accent)',
                  color: '#FFFFFF',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 8,
                }}
              >
                <Lock size={15} />
                Chốt Suất ({effectiveCutoffLabel})
              </button>
            )}
          </div>
        </div>
      </header>

      {/* Global alert toast - Clean, un-cluttered notice */}
      {message && (
        <div
          style={{
            marginBottom: 20,
            background: message.type === 'success' ? '#ECFDF5' : '#FFF1F2',
            border: `2px solid ${message.type === 'success' ? 'var(--color-quaternary)' : 'var(--color-secondary)'}`,
            borderRadius: 'var(--radius-md)',
            boxShadow: '3px 3px 0 0 var(--color-fg)',
            padding: '12px 18px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            animation: 'pop-in 0.2s ease',
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
            style={{ borderRadius: 'var(--radius-full)' }}
          >
            Đóng
          </button>
        </div>
      )}

      {/* TAB NAVIGATION PILLS - Clean, Sleek, Unified */}
      <div style={{ marginBottom: 24 }}>
        <div
          style={{
            display: 'inline-flex',
            gap: 6,
            background: 'var(--color-muted)',
            padding: '4px',
            borderRadius: 'var(--radius-full)',
            border: '2px solid var(--color-fg)',
            boxShadow: '3px 3px 0 0 var(--color-fg)',
            maxWidth: '100%',
            overflowX: 'auto',
          }}
        >
          <button
            type="button"
            onClick={() => handleTabChange('summary')}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
              padding: '8px 18px',
              borderRadius: 'var(--radius-full)',
              border: activeTab === 'summary' ? '2px solid var(--color-fg)' : '2px solid transparent',
              background: activeTab === 'summary' ? 'var(--color-accent)' : 'transparent',
              color: activeTab === 'summary' ? '#FFFFFF' : 'var(--color-fg)',
              fontWeight: 800,
              fontSize: 'var(--fs-sm)',
              cursor: 'pointer',
              boxShadow: activeTab === 'summary' ? '2px 2px 0 0 var(--color-fg)' : 'none',
              transition: 'all 0.15s ease',
            }}
          >
            <FileSpreadsheet size={15} />
            1. Tổng Hợp & Chốt Suất
          </button>

          <button
            type="button"
            onClick={() => handleTabChange('students')}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
              padding: '8px 18px',
              borderRadius: 'var(--radius-full)',
              border: activeTab === 'students' ? '2px solid var(--color-fg)' : '2px solid transparent',
              background: activeTab === 'students' ? 'var(--color-accent)' : 'transparent',
              color: activeTab === 'students' ? '#FFFFFF' : 'var(--color-fg)',
              fontWeight: 800,
              fontSize: 'var(--fs-sm)',
              cursor: 'pointer',
              boxShadow: activeTab === 'students' ? '2px 2px 0 0 var(--color-fg)' : 'none',
              transition: 'all 0.15s ease',
            }}
          >
            <Users size={15} />
            2. Danh Sách Học Sinh ({totalCount})
          </button>

          <button
            type="button"
            onClick={() => handleTabChange('logs')}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
              padding: '8px 18px',
              borderRadius: 'var(--radius-full)',
              border: activeTab === 'logs' ? '2px solid var(--color-fg)' : '2px solid transparent',
              background: activeTab === 'logs' ? 'var(--color-accent)' : 'transparent',
              color: activeTab === 'logs' ? '#FFFFFF' : 'var(--color-fg)',
              fontWeight: 800,
              fontSize: 'var(--fs-sm)',
              cursor: 'pointer',
              boxShadow: activeTab === 'logs' ? '2px 2px 0 0 var(--color-fg)' : 'none',
              transition: 'all 0.15s ease',
            }}
          >
            <Clock size={15} />
            3. Nhật Ký Biến Động ({board?.mealChangeLogs?.length || 0})
          </button>
        </div>
      </div>

      {/* ===================================================================== */}
      {/* MÀN HÌNH 1: TỔNG HỢP & CHỐT SUẤT BÁN TRÚ                               */}
      {/* ===================================================================== */}
      {activeTab === 'summary' && (
        <div className="pop-in">
          {/* Banner Cutoff Time & Menu (Clean Warm Cream Paper) */}
          {board && (
            <div
              style={{
                marginBottom: 24,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                flexWrap: 'wrap',
                gap: 16,
                background: '#FFFDF5',
                border: '2px solid var(--color-fg)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-lg)',
                padding: '16px 20px',
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
                    border: '2px solid var(--color-fg)',
                    boxShadow: '2px 2px 0 0 var(--color-fg)',
                    display: 'grid',
                    placeItems: 'center',
                    flexShrink: 0,
                  }}
                >
                  <Clock size={22} color="var(--color-fg)" />
                </div>
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                    <h3 style={{ margin: 0, fontSize: 'var(--fs-base)', fontFamily: 'var(--font-heading)' }}>
                      Quy tắc chốt ăn Tân Lập A: {effectiveCutoffLabel}
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
                          : `Hạn hủy suất: ${effectiveCutoffLabel}`}
                    </span>
                  </div>
                  <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
                    {board.serviceRemarks ? (
                      <>
                        <strong style={{ color: 'var(--color-fg)' }}>Thực đơn trưa hôm nay:</strong> {board.serviceRemarks}
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
                  style={{ borderRadius: 'var(--radius-full)' }}
                >
                  <Users size={14} />
                  Xem danh sách học sinh
                  <ArrowRight size={14} />
                </button>
              </div>
            </div>
          )}

          {/* 4 Live KPI Cards - Harmonious, Clean, Balanced Grid */}
          {board && (
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
                gap: 16,
                marginBottom: 28,
              }}
            >
              {/* Card 1: Registered Total */}
              <div
                className="card"
                style={{
                  background: 'var(--color-card)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '4px 4px 0 0 var(--color-fg)',
                  borderRadius: 'var(--radius-lg)',
                  padding: '20px 22px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 10,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <div className="label" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.04em' }}>
                    TỔNG ĐĂNG KÝ ĐẦU NGÀY
                  </div>
                  <div
                    style={{
                      width: 34,
                      height: 34,
                      borderRadius: '50%',
                      background: 'var(--color-tertiary)',
                      border: '1.5px solid var(--color-fg)',
                      display: 'grid',
                      placeItems: 'center',
                    }}
                  >
                    <UtensilsCrossed size={16} color="var(--color-fg)" />
                  </div>
                </div>

                <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 800, color: 'var(--color-fg)', fontFamily: 'var(--font-heading)' }}>
                  {board.totalInitialRegistered}{' '}
                  <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>suất</span>
                </div>

                <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Đăng ký bán trú chính thức trong ngày
                </p>
              </div>

              {/* Card 2: Cancelled */}
              <div
                className="card"
                style={{
                  background: 'var(--color-card)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '4px 4px 0 0 var(--color-fg)',
                  borderRadius: 'var(--radius-lg)',
                  padding: '20px 22px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 10,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <div className="label" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.04em', color: '#9F1239' }}>
                    ĐÃ HỦY TRƯỚC {effectiveCutoffLabel}
                  </div>
                  <div
                    style={{
                      width: 34,
                      height: 34,
                      borderRadius: '50%',
                      background: '#FFE4E6',
                      border: '1.5px solid var(--color-fg)',
                      display: 'grid',
                      placeItems: 'center',
                    }}
                  >
                    <UserMinus size={16} color="#E11D48" />
                  </div>
                </div>

                <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 800, color: '#E11D48', fontFamily: 'var(--font-heading)' }}>
                  -{board.totalAutoCancelled}{' '}
                  <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>suất</span>
                </div>

                <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: '#E11D48' }}>
                  Hủy từ đơn xin nghỉ & phụ huynh báo hủy
                </p>
              </div>

              {/* Card 3: Net Active To Cook - HERO METRIC CARD */}
              <div
                className="card"
                style={{
                  background: '#ECFDF5',
                  border: '2.5px solid #059669',
                  boxShadow: '4px 4px 0 0 var(--color-fg)',
                  borderRadius: 'var(--radius-lg)',
                  padding: '20px 22px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 10,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <div className="label" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.04em', color: '#065F46', fontWeight: 800 }}>
                    THỰC NẤU (BÀN GIAO BẾP)
                  </div>
                  <div
                    style={{
                      width: 34,
                      height: 34,
                      borderRadius: '50%',
                      background: 'var(--color-quaternary)',
                      border: '1.5px solid var(--color-fg)',
                      display: 'grid',
                      placeItems: 'center',
                    }}
                  >
                    <Soup size={18} color="var(--color-fg)" />
                  </div>
                </div>

                <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 900, color: '#065F46', fontFamily: 'var(--font-heading)' }}>
                  {board.totalActiveToCook}{' '}
                  <span style={{ fontSize: 'var(--fs-md)', fontWeight: 700, color: '#047857' }}>khẩu phần</span>
                </div>

                <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: '#047857', fontWeight: 700 }}>
                  ✓ Số lượng thực tế giao bếp thực hiện
                </p>
              </div>

              {/* Card 4: Anomalies */}
              <div
                className="card"
                style={{
                  background: 'var(--color-card)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '4px 4px 0 0 var(--color-fg)',
                  borderRadius: 'var(--radius-lg)',
                  padding: '20px 22px',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 10,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <div className="label" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.04em', color: board.totalAnomalies > 0 ? 'var(--color-accent)' : 'var(--color-muted-fg)' }}>
                    PHÁT SINH CẦN ĐỐI SOÁT
                  </div>
                  <div
                    style={{
                      width: 34,
                      height: 34,
                      borderRadius: '50%',
                      background: '#EDE9FE',
                      border: '1.5px solid var(--color-fg)',
                      display: 'grid',
                      placeItems: 'center',
                    }}
                  >
                    <ShieldAlert size={16} color="var(--color-accent)" />
                  </div>
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

                <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: board.totalAnomalies > 0 ? 'var(--color-accent)' : 'var(--color-muted-fg)' }}>
                  Hủy suất ăn nhưng có mặt tại lớp
                </p>
              </div>
            </div>
          )}

          {/* Class Breakdown Table */}
          <div
            className="card"
            style={{
              marginBottom: 28,
              border: '2px solid var(--color-fg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
              borderRadius: 'var(--radius-lg)',
              padding: '24px',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16, flexWrap: 'wrap', gap: 12 }}>
              <div>
                <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
                  <FileSpreadsheet size={18} color="var(--color-accent)" />
                  Tổng Hợp Khẩu Phần Theo Lớp Học
                </h3>
                <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Bấm vào hàng của một lớp để chuyển nhanh sang danh sách học sinh của lớp đó.
                </p>
              </div>
              <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', padding: '5px 12px', fontWeight: 700 }}>
                {board?.classBreakdowns?.length || 0} lớp học • {board?.totalActiveToCook || 0} suất
              </span>
            </div>

            <div style={{ overflowX: 'auto', border: '2px solid var(--color-fg)', borderRadius: 'var(--radius-md)' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                <thead>
                  <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                    <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Lớp</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Khối</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Đăng ký</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Đã hủy</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Thực ăn</th>
                    <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Tỷ lệ nhận</th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Thao tác</th>
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
                          style={{ borderBottom: '1px solid var(--color-border)', transition: 'background 0.15s' }}
                          onMouseEnter={(e) => (e.currentTarget.style.background = '#FFFDF5')}
                          onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
                        >
                          <td style={{ padding: '12px 14px', fontWeight: 800, color: 'var(--color-accent)' }}>
                            Lớp {cls.className}
                          </td>
                          <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                            Khối {cls.grade}
                          </td>
                          <td style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600 }}>
                            {cls.initialRegistered}
                          </td>
                          <td
                            style={{
                              padding: '12px 14px',
                              textAlign: 'center',
                              color: cls.cancelledCount > 0 ? '#E11D48' : 'var(--color-muted-fg)',
                              fontWeight: cls.cancelledCount > 0 ? 800 : 400,
                            }}
                          >
                            {cls.cancelledCount > 0 ? `-${cls.cancelledCount}` : '0'}
                          </td>
                          <td style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 900, color: '#047857', fontSize: 'var(--fs-base)' }}>
                            {cls.actualCount}
                          </td>
                          <td style={{ padding: '12px 14px' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                              <div style={{ flex: 1, height: 10, background: 'var(--color-border)', borderRadius: 5, overflow: 'hidden', border: '1px solid var(--color-fg)' }}>
                                <div
                                  style={{
                                    width: `${percentage}%`,
                                    height: '100%',
                                    background: percentage === 100 ? 'var(--color-quaternary)' : 'var(--color-tertiary)',
                                  }}
                                />
                              </div>
                              <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-fg)', minWidth: 36, fontWeight: 700 }}>
                                {percentage}%
                              </span>
                            </div>
                          </td>
                          <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                            <button
                              type="button"
                              className="btn btn--secondary btn--sm"
                              onClick={() => {
                                setSelectedClassFilter(String(cls.classId));
                                handleTabChange('students');
                              }}
                              title={`Xem danh sách học sinh lớp ${cls.className}`}
                              style={{
                                borderRadius: 'var(--radius-full)',
                                border: '1.5px solid var(--color-fg)',
                                boxShadow: '2px 2px 0 0 var(--color-fg)',
                                fontWeight: 700,
                                fontSize: 'var(--fs-xs)',
                                padding: '4px 12px',
                              }}
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
      {/* MÀN HÌNH 2: DANH SÁCH HỌC SINH NHẬN SUẤT + PAGINATION                 */}
      {/* ===================================================================== */}
      {activeTab === 'students' && (
        <div
          className="card pop-in"
          style={{
            border: '2px solid var(--color-fg)',
            boxShadow: '4px 4px 0 0 var(--color-fg)',
            borderRadius: 'var(--radius-lg)',
            padding: '24px',
          }}
        >
          {/* Header & Filter Controls */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16, marginBottom: 20 }}>
            <div>
              <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
                <Users size={18} color="var(--color-accent)" />
                Danh Sách Học Sinh Nhận Suất Ăn
              </h3>
              <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                Phục vụ nhân viên bán trú và giáo viên chủ nhiệm đối chiếu phát khay ăn theo từng lớp.
              </p>
            </div>

            {/* Action buttons */}
            <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={handleExportCsv}
                title="Xuất danh sách học sinh ra file CSV/Excel"
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  fontWeight: 700,
                  padding: '6px 14px',
                }}
              >
                <Download size={14} />
                Xuất CSV
              </button>
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={() => setShowPrintModal(true)}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  fontWeight: 700,
                  padding: '6px 14px',
                }}
              >
                <Printer size={14} />
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
              padding: '14px 18px',
              background: '#FFFDF5',
              borderRadius: 'var(--radius-md)',
              marginBottom: 20,
              border: '2px solid var(--color-fg)',
              boxShadow: '2px 2px 0 0 var(--color-fg)',
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
                style={{
                  paddingLeft: 36,
                  fontSize: 'var(--fs-sm)',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                }}
              />
            </div>

            {/* Class Filter */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <Filter size={15} color="var(--color-muted-fg)" />
              <select
                className="select"
                value={selectedClassFilter}
                onChange={(e) => setSelectedClassFilter(e.target.value)}
                style={{
                  minWidth: 160,
                  padding: '6px 12px',
                  fontSize: 'var(--fs-sm)',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                }}
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
              style={{
                minWidth: 190,
                padding: '6px 12px',
                fontSize: 'var(--fs-sm)',
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
              }}
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
              style={{
                minWidth: 120,
                padding: '6px 12px',
                fontSize: 'var(--fs-sm)',
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
              }}
            >
              <option value={10}>10 dòng / trang</option>
              <option value={20}>20 dòng / trang</option>
              <option value={50}>50 dòng / trang</option>
            </select>
          </div>

          {/* Quick status counters */}
          <div style={{ display: 'flex', gap: 8, marginBottom: 16, alignItems: 'center', flexWrap: 'wrap' }}>
            <span className="badge badge--violet" style={{ border: '1.5px solid var(--color-fg)' }}>
              Tổng tìm thấy: <strong>{totalCount}</strong> học sinh
            </span>
            <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)' }}>
              Thực ăn: <strong>{filteredStudentMeals.filter((s) => s.isActive).length}</strong>
            </span>
            <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)' }}>
              Đã hủy: <strong>{filteredStudentMeals.filter((s) => !s.isActive).length}</strong>
            </span>
            {searchQuery && (
              <span className="badge badge--amber" style={{ border: '1.5px solid var(--color-fg)' }}>
                Từ khóa: &quot;{searchQuery}&quot;
              </span>
            )}
          </div>

          {/* Student Table */}
          <div style={{ overflowX: 'auto', border: '2px solid var(--color-fg)', borderRadius: 'var(--radius-md)' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                  <th style={{ padding: '12px 14px', textAlign: 'center', width: 56, fontWeight: 800 }}>STT</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Mã HS</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Họ và tên học sinh</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Lớp</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Chế độ ăn</th>
                  <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Trạng thái</th>
                  <th style={{ padding: '12px 14px', textAlign: 'left', fontWeight: 800 }}>Ghi chú / Lý do hủy</th>
                </tr>
              </thead>
              <tbody>
                {pagedStudents.length > 0 ? (
                  pagedStudents.map((item, idx) => (
                    <tr
                      key={item.registrationId}
                      style={{ borderBottom: '1px solid var(--color-border)', transition: 'background 0.15s' }}
                      onMouseEnter={(e) => (e.currentTarget.style.background = '#FFFDF5')}
                      onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
                    >
                      <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 600 }}>
                        {(pageIndex - 1) * pageSize + idx + 1}
                      </td>
                      <td style={{ padding: '10px 14px', fontWeight: 800, color: 'var(--color-accent)' }}>
                        {item.studentCode}
                      </td>
                      <td style={{ padding: '10px 14px', fontWeight: 700 }}>{item.studentName}</td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>Lớp {item.className}</td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                        <span className="badge badge--amber" style={{ border: '1px solid var(--color-fg)' }}>
                          {MEAL_TYPE_TEXT[String(item.mealType)] ?? 'Tiêu chuẩn'}
                        </span>
                      </td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                        {item.isActive ? (
                          <span className="badge badge--mint" style={{ border: '1px solid var(--color-fg)' }}>Thực ăn</span>
                        ) : (
                          <span className="badge badge--pink" style={{ border: '1px solid var(--color-fg)' }}>Đã hủy</span>
                        )}
                      </td>
                      <td style={{ padding: '10px 14px', color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>
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

            {/* PAGINATION BAR */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '12px 18px',
                borderTop: '2px solid var(--color-fg)',
                background: '#F8FAFC',
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
                  style={{
                    borderRadius: 'var(--radius-full)',
                    border: '1.5px solid var(--color-fg)',
                    boxShadow: pageIndex > 1 ? '2px 2px 0 0 var(--color-fg)' : 'none',
                    fontWeight: 700,
                  }}
                >
                  <ChevronLeft size={14} /> Trang trước
                </button>
                <span className="badge badge--violet" style={{ alignSelf: 'center', padding: '4px 12px', border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                  Trang {pageIndex} / {totalPages}
                </span>
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  disabled={pageIndex >= totalPages}
                  onClick={() => setPageIndex((p) => Math.min(totalPages, p + 1))}
                  style={{
                    borderRadius: 'var(--radius-full)',
                    border: '1.5px solid var(--color-fg)',
                    boxShadow: pageIndex < totalPages ? '2px 2px 0 0 var(--color-fg)' : 'none',
                    fontWeight: 700,
                  }}
                >
                  Trang sau <ChevronRight size={14} />
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* ===================================================================== */}
      {/* MÀN HÌNH 3: NHẬT KÝ BIẾN ĐỘNG SUẤT ĂN                                */}
      {/* ===================================================================== */}
      {activeTab === 'logs' && (
        <div
          className="card pop-in"
          style={{
            border: '2px solid var(--color-fg)',
            boxShadow: '4px 4px 0 0 var(--color-fg)',
            borderRadius: 'var(--radius-lg)',
            padding: '24px',
          }}
        >
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12, marginBottom: 20 }}>
            <div>
              <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
                <Clock size={18} color="var(--color-secondary)" />
                Nhật Ký Biến Động Suất Ăn
              </h3>
              <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                Truy vết toàn bộ việc tự động hủy suất trước {effectiveCutoffLabel}, phụ huynh báo hủy trực tiếp và khôi phục suất ăn.
              </p>
            </div>

            {/* Filter logs */}
            <div style={{ display: 'flex', gap: 8 }}>
              <select
                className="select"
                value={logFilter}
                onChange={(e) => setLogFilter(e.target.value as 'ALL' | 'AutoCancelled' | 'ParentCancelled' | 'Restored')}
                style={{
                  minWidth: 220,
                  padding: '8px 14px',
                  fontSize: 'var(--fs-sm)',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                }}
              >
                <option value="ALL">Tất cả biến động ({board?.mealChangeLogs?.length || 0})</option>
                <option value="AutoCancelled">Tự hủy từ đơn xin nghỉ</option>
                <option value="ParentCancelled">Phụ huynh báo hủy</option>
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
                      padding: '16px 20px',
                      borderRadius: 'var(--radius-md)',
                      border: '2px solid var(--color-fg)',
                      boxShadow: '3px 3px 0 0 var(--color-fg)',
                      background: isCancel ? '#FFF1F2' : '#ECFDF5',
                      fontSize: 'var(--fs-sm)',
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 6, flexWrap: 'wrap', gap: 8 }}>
                      <div>
                        <strong style={{ fontSize: 'var(--fs-base)', color: 'var(--color-fg)' }}>
                          {log.studentName}
                        </strong>
                        <span style={{ marginLeft: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)' }}>
                          ({log.studentCode}) — Lớp {log.className}
                        </span>
                      </div>
                      <span className={`badge ${isCancel ? 'badge--pink' : 'badge--mint'}`} style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700 }}>
                        {log.changeType === 'ParentCancelled'
                          ? 'Phụ huynh báo hủy'
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
                      <span style={{ color: '#059669', fontWeight: 800 }}>✓ Trước giờ chốt {effectiveCutoffLabel}</span>
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
        title="Cấu hình Ngày Phục Vụ Bán Trú"
        subtitle="Thiết lập trạng thái phục vụ, giờ chốt suất ăn và thực đơn trưa của trường"
        badgeText={`Ngày ${new Date(selectedDate).toLocaleDateString('vi-VN')}`}
        badgeColor="mint"
        icon={<Settings size={22} color="var(--color-fg)" />}
        iconBg="var(--color-quaternary)"
        maxWidth="md"
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          {/* 1. Trạng thái phục vụ (2-Card Segmented Selector) */}
          <div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
              <label className="label" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.05em' }}>
                1. TRẠNG THÁI PHỤC VỤ TRONG NGÀY
              </label>
              <span style={{ fontSize: 'var(--fs-xs)', fontWeight: 700, color: cfgIsServing ? '#059669' : '#E11D48' }}>
                {cfgIsServing ? '🟢 Đang mở phục vụ' : '🔴 Tạm nghỉ phục vụ'}
              </span>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
              <button
                type="button"
                onClick={() => setCfgIsServing(true)}
                style={{
                  padding: '12px 14px',
                  borderRadius: 'var(--radius-md)',
                  border: cfgIsServing ? '2.5px solid var(--color-quaternary)' : '2px solid var(--color-border)',
                  background: cfgIsServing ? '#ECFDF5' : 'var(--color-card)',
                  boxShadow: cfgIsServing ? '3px 3px 0 0 var(--color-fg)' : 'none',
                  textAlign: 'left',
                  cursor: 'pointer',
                  transition: 'all 0.15s cubic-bezier(0.34, 1.56, 0.64, 1)',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 4,
                  transform: cfgIsServing ? 'translate(-1px, -1px)' : 'none',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <span style={{ fontWeight: 800, fontSize: 'var(--fs-sm)', color: '#065F46', display: 'flex', alignItems: 'center', gap: 6 }}>
                    <span>🟢</span> Phục vụ bán trú
                  </span>
                  {cfgIsServing && (
                    <span className="badge badge--mint" style={{ fontSize: 10, padding: '2px 6px' }}>
                      Đang chọn
                    </span>
                  )}
                </div>
                <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Mở đăng ký & nhà bếp nấu suất ăn bình thường
                </span>
              </button>

              <button
                type="button"
                onClick={() => setCfgIsServing(false)}
                style={{
                  padding: '12px 14px',
                  borderRadius: 'var(--radius-md)',
                  border: !cfgIsServing ? '2.5px solid #F43F5E' : '2px solid var(--color-border)',
                  background: !cfgIsServing ? '#FFF1F2' : 'var(--color-card)',
                  boxShadow: !cfgIsServing ? '3px 3px 0 0 var(--color-fg)' : 'none',
                  textAlign: 'left',
                  cursor: 'pointer',
                  transition: 'all 0.15s cubic-bezier(0.34, 1.56, 0.64, 1)',
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 4,
                  transform: !cfgIsServing ? 'translate(-1px, -1px)' : 'none',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <span style={{ fontWeight: 800, fontSize: 'var(--fs-sm)', color: '#9F1239', display: 'flex', alignItems: 'center', gap: 6 }}>
                    <span>🔴</span> Nghỉ phục vụ
                  </span>
                  {!cfgIsServing && (
                    <span className="badge badge--pink" style={{ fontSize: 10, padding: '2px 6px' }}>
                      Đang chọn
                    </span>
                  )}
                </div>
                <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Nghỉ lễ, hoạt động ngoại khóa hoặc nghỉ bếp
                </span>
              </button>
            </div>
          </div>

          {/* 2. Giờ chốt (Cutoff Time) */}
          <div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
              <label className="label" htmlFor="cfg-cutoff" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.05em', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                <Clock size={14} color="var(--color-accent)" /> 2. HẠN ĐĂNG KÝ / HỦY SUẤT ĂN
              </label>
              <span className="badge badge--amber" style={{ fontSize: '11px', padding: '2px 8px' }}>
                Mốc chuẩn: 08:15
              </span>
            </div>

            <div style={{ position: 'relative' }}>
              <input
                id="cfg-cutoff"
                type="time"
                className="input"
                value={cfgCutoffTime}
                onChange={(e) => setCfgCutoffTime(e.target.value)}
                style={{
                  fontWeight: 700,
                  fontSize: 'var(--fs-base)',
                  letterSpacing: '0.05em',
                  paddingLeft: 40,
                }}
              />
              <Clock
                size={18}
                color="var(--color-muted-fg)"
                style={{ position: 'absolute', left: 14, top: '50%', transform: 'translateY(-50%)', pointerEvents: 'none' }}
              />
            </div>

            {/* Quick preset chips */}
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 8, flexWrap: 'wrap' }}>
              <span style={{ fontSize: 'var(--fs-xs)', fontWeight: 700, color: 'var(--color-muted-fg)' }}>Gợi ý nhanh:</span>
              {[
                { time: '08:00', label: '08:00' },
                { time: '08:15', label: '08:15 (Tân Lập A)' },
                { time: '08:30', label: '08:30' },
                { time: '09:00', label: '09:00' },
              ].map((preset) => (
                <button
                  key={preset.time}
                  type="button"
                  onClick={() => setCfgCutoffTime(preset.time)}
                  style={{
                    padding: '3px 10px',
                    borderRadius: 'var(--radius-full)',
                    border: cfgCutoffTime === preset.time ? '2px solid var(--color-fg)' : '1.5px solid var(--color-border)',
                    background: cfgCutoffTime === preset.time ? 'var(--color-tertiary)' : 'var(--color-card)',
                    boxShadow: cfgCutoffTime === preset.time ? '2px 2px 0 0 var(--color-fg)' : 'none',
                    fontWeight: 700,
                    fontSize: 'var(--fs-xs)',
                    cursor: 'pointer',
                    transition: 'all 0.15s ease',
                  }}
                >
                  {preset.label}
                </button>
              ))}
            </div>
          </div>

          {/* 3. Thực đơn & Ghi chú (Menu Builder) */}
          <div>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
              <label className="label" htmlFor="cfg-remarks" style={{ marginBottom: 0, fontSize: 'var(--fs-xs)', letterSpacing: '0.05em', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
                <UtensilsCrossed size={14} color="var(--color-secondary)" /> 3. THỰC ĐƠN TRƯA HOẶC GHI CHÚ
              </label>
              {cfgRemarks.trim() && (
                <button
                  type="button"
                  onClick={() => setCfgRemarks('')}
                  style={{
                    background: 'none',
                    border: 'none',
                    color: '#E11D48',
                    fontSize: 'var(--fs-xs)',
                    fontWeight: 700,
                    cursor: 'pointer',
                    padding: '2px 4px',
                  }}
                >
                  Xóa thực đơn
                </button>
              )}
            </div>

            {/* Quick dish chips */}
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 8 }}>
              {MENU_SUGGESTIONS.map((dish) => (
                <button
                  key={dish}
                  type="button"
                  onClick={() => handleAddDish(dish)}
                  style={{
                    padding: '3px 8px',
                    borderRadius: 'var(--radius-sm)',
                    border: '1.5px dashed var(--color-border)',
                    background: '#FFFDF5',
                    fontSize: 'var(--fs-xs)',
                    fontWeight: 600,
                    cursor: 'pointer',
                    color: 'var(--color-fg)',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 3,
                    transition: 'all 0.12s ease',
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.borderColor = 'var(--color-fg)';
                    e.currentTarget.style.background = 'var(--color-tertiary)';
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.borderColor = 'var(--color-border)';
                    e.currentTarget.style.background = '#FFFDF5';
                  }}
                >
                  <Plus size={11} strokeWidth={3} /> {dish}
                </button>
              ))}
            </div>

            <textarea
              id="cfg-remarks"
              className="input"
              rows={3}
              placeholder="VD: Cơm trắng, Thịt gà kho nấm, Canh bí đỏ nấu tôm, Chuối tiêu..."
              value={cfgRemarks}
              onChange={(e) => setCfgRemarks(e.target.value)}
              style={{ resize: 'vertical', minHeight: 76, width: '100%', boxSizing: 'border-box' }}
            />
          </div>

          {/* 4. Áp dụng cả tuần (Playful Sticker Card) */}
          <div
            onClick={() => setCfgApplyFullWeek(!cfgApplyFullWeek)}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 14,
              padding: '12px 16px',
              background: cfgApplyFullWeek ? '#FEF3C7' : '#FFFDF5',
              borderRadius: 'var(--radius-md)',
              border: '2px solid var(--color-fg)',
              boxShadow: cfgApplyFullWeek ? '3px 3px 0 0 var(--color-fg)' : '2px 2px 0 0 var(--color-fg)',
              cursor: 'pointer',
              transition: 'all 0.15s cubic-bezier(0.34, 1.56, 0.64, 1)',
              userSelect: 'none',
            }}
          >
            <div
              style={{
                width: 40,
                height: 40,
                borderRadius: '50%',
                background: cfgApplyFullWeek ? 'var(--color-tertiary)' : 'var(--color-muted)',
                border: '2px solid var(--color-fg)',
                display: 'grid',
                placeItems: 'center',
                flexShrink: 0,
              }}
            >
              <CalendarRange size={20} color="var(--color-fg)" />
            </div>

            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 800, fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>
                Áp dụng cấu hình này cho cả tuần làm việc
              </div>
              <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 2 }}>
                Tự động đồng bộ trạng thái, giờ chốt và thực đơn cho Thứ 2 – Thứ 6 của tuần đang chọn
              </div>
            </div>

            <input
              type="checkbox"
              checked={cfgApplyFullWeek}
              onChange={(e) => setCfgApplyFullWeek(e.target.checked)}
              onClick={(e) => e.stopPropagation()}
              style={{
                width: 18,
                height: 18,
                accentColor: 'var(--color-accent)',
                cursor: 'pointer',
              }}
            />
          </div>

          {/* 5. Candy Action Buttons */}
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, paddingTop: 10, borderTop: '1.5px dashed var(--color-border)' }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowConfigModal(false)}
              disabled={actionLoading}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                fontWeight: 700,
                padding: '10px 20px',
              }}
            >
              Hủy
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleSaveConfig}
              disabled={actionLoading}
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
              <CheckCircle2 size={18} />
              {actionLoading ? 'Đang lưu...' : 'Lưu cấu hình phục vụ'}
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
              background: '#FFFDF5',
              border: '2px solid var(--color-fg)',
              boxShadow: '3px 3px 0 0 var(--color-fg)',
              borderRadius: 'var(--radius-md)',
              padding: '16px 20px',
              marginBottom: 16,
              fontSize: 'var(--fs-sm)',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8, alignItems: 'center' }}>
              <span style={{ fontWeight: 600 }}>Tổng suất cần nấu hôm nay:</span>
              <span className="badge badge--mint" style={{ fontSize: 'var(--fs-sm)', padding: '4px 10px', fontWeight: 800 }}>
                {board?.totalActiveToCook} suất
              </span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', color: '#e11d48' }}>
              <span style={{ fontWeight: 600 }}>Tổng suất hủy hợp lệ:</span>
              <strong style={{ fontSize: 'var(--fs-sm)' }}>-{board?.totalAutoCancelled} suất</strong>
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
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: '2px 2px 0 0 var(--color-fg)',
                fontWeight: 700,
                padding: '10px 20px',
              }}
            >
              Hủy bỏ
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleLockDay}
              disabled={actionLoading}
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
