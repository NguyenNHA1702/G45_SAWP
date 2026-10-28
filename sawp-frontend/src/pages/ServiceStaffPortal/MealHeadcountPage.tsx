import { useEffect, useMemo, useState } from 'react';
import {
  Sparkles,
  UtensilsCrossed,
  Clock,
  CheckCircle2,
  AlertTriangle,
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
  X,
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

  // Bộ lọc danh sách vận hành (UC-33)
  const [selectedClassFilter, setSelectedClassFilter] = useState<string>('ALL');
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'ACTIVE' | 'CANCELLED'>('ACTIVE');

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

  const filteredStudentMeals = useMemo(() => {
    const list = board?.studentMeals ?? [];
    return list.filter((item) => {
      if (selectedClassFilter !== 'ALL' && String(item.classId) !== selectedClassFilter) {
        return false;
      }
      if (statusFilter === 'ACTIVE' && !item.isActive) return false;
      if (statusFilter === 'CANCELLED' && item.isActive) return false;
      return true;
    });
  }, [board?.studentMeals, selectedClassFilter, statusFilter]);

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

      {/* Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
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
              Bảng Vận Hành <span className="squiggle">Suất Ăn Bán Trú</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)' }}>
              Cấu hình ngày phục vụ & hạn đăng ký/hủy ({effectiveCutoffLabel}), tự động trừ suất khi đơn nghỉ duyệt trước {effectiveCutoffLabel} và tổng hợp suất ăn theo lớp.
            </p>
          </div>

          {/* Quick Date Control & UC-32 Button */}
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

            {board?.canManage && (
              <button
                type="button"
                className="btn btn--secondary"
                onClick={() => setShowConfigModal(true)}
                title="Cấu hình ngày phục vụ, hạn chốt và thực đơn (UC-32)"
              >
                <Settings size={15} />
                Cấu hình ngày (UC-32)
              </button>
            )}

            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => fetchBoard(selectedDate)}
              disabled={loading}
              title="Làm mới dữ liệu"
            >
              <RefreshCw size={15} className={loading ? 'spin' : ''} />
              Làm mới
            </button>
          </div>
        </div>
      </header>

      {/* Notifications */}
      {message && (
        <div
          className="card"
          style={{
            marginBottom: 20,
            padding: '12px 18px',
            background: message.type === 'success' ? 'var(--color-quaternary-soft)' : 'var(--color-secondary-soft)',
            borderColor: message.type === 'success' ? 'var(--color-quaternary)' : 'var(--color-secondary)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, color: 'var(--color-fg)', fontWeight: 600 }}>
            {message.type === 'success' ? <CheckCircle2 size={18} color="#059669" /> : <AlertTriangle size={18} color="#e11d48" />}
            <span>{message.text}</span>
          </div>
          <button
            type="button"
            onClick={() => setMessage(null)}
            style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--color-fg)' }}
          >
            <X size={16} />
          </button>
        </div>
      )}

      {/* Status Bar: Cutoff, Menu & Lock state */}
      {board && (
        <div
          className="card card--featured"
          style={{
            marginBottom: 28,
            padding: '16px 24px',
            background: !board.isServing
              ? 'var(--color-muted)'
              : board.isLocked
                ? 'var(--color-quaternary-soft)'
                : 'var(--color-tertiary-soft)',
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
                background: !board.isServing
                  ? 'var(--color-border)'
                  : board.isLocked
                    ? 'var(--color-quaternary)'
                    : 'var(--color-tertiary)',
                border: 'var(--border)',
                boxShadow: 'var(--shadow-pop-active)',
                display: 'grid',
                placeItems: 'center',
                flexShrink: 0,
              }}
            >
              {board.isLocked ? <Lock size={20} color="var(--color-fg)" /> : <Clock size={20} color="var(--color-fg)" />}
            </div>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                <strong style={{ fontSize: 'var(--fs-md)', color: 'var(--color-fg)', fontFamily: 'var(--font-heading)' }}>
                  {!board.isServing
                    ? 'NGÀY KHÔNG PHỤC VỤ BÁN TRÚ'
                    : board.isLocked
                      ? 'ĐÃ CHỐT SỐ LIỆU SUẤT ĂN GỬI NHÀ BẾP'
                      : 'ĐANG MỞ CẬP NHẬT SUẤT ĂN TRONG NGÀY'}
                </strong>
                <span className={`badge ${!board.isServing ? 'badge--muted' : board.isLocked ? 'badge--mint' : 'badge--amber'}`}>
                  {!board.isServing
                    ? 'Đóng dịch vụ'
                    : board.isLocked
                      ? `Đã khóa (Mốc ${effectiveCutoffLabel})`
                      : `Hạn đăng ký/hủy: ${effectiveCutoffLabel}`}
                </span>
              </div>
              <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
                {board.serviceRemarks ? (
                  <>
                    <strong>Thực đơn / Ghi chú ngày:</strong> {board.serviceRemarks}
                  </>
                ) : (
                  `Đơn xin nghỉ học có bữa trưa được duyệt trước ${effectiveCutoffLabel} sẽ tự động giảm trừ suất ăn.`
                )}
              </p>
            </div>
          </div>

          {/* Action buttons */}
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={handleExportCsv}
              title="Xuất danh sách vận hành ra file CSV/Excel"
            >
              <Download size={16} />
              Xuất CSV
            </button>

            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowPrintModal(true)}
            >
              <Printer size={16} />
              In Phiếu Giao Bếp
            </button>

            {board.canManage && board.isServing && !board.isLocked && (
              <button
                type="button"
                className="btn btn--primary"
                onClick={() => setShowLockModal(true)}
              >
                <Lock size={16} />
                Chốt Suất Ăn (Mốc {effectiveCutoffLabel})
              </button>
            )}
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
            marginBottom: 32,
            paddingTop: 16,
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
              Theo các bản đăng ký ngày độc lập (UC-29)
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
              PH hủy trực tiếp (UC-30) & Tự hủy từ đơn nghỉ
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

      {/* Main Content Layout: Two Columns */}
      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1.4fr) minmax(0, 1fr)', gap: 24, alignItems: 'start', marginBottom: 28 }}>
        {/* Left Column: Class Meal Breakdown Table */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
              <FileSpreadsheet size={18} color="var(--color-accent)" />
              Tổng hợp số suất nhóm theo Lớp học (UC-33)
            </h3>
            <span className="badge badge--muted">
              {board?.classBreakdowns?.length || 0} lớp
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
                        onClick={() => setSelectedClassFilter(String(cls.classId))}
                        style={{ borderBottom: '1px solid var(--color-border)', cursor: 'pointer' }}
                        title="Bấm để lọc danh sách học sinh của lớp này"
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
                      </tr>
                    );
                  })
                ) : (
                  <tr>
                    <td colSpan={6} style={{ textAlign: 'center', padding: '32px', color: 'var(--color-muted-fg)' }}>
                      Chưa có dữ liệu phân bổ cho ngày này.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* Right Column: Real-time Audit Logs */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
              <Clock size={18} color="var(--color-secondary)" />
              Nhật ký biến động suất ăn
            </h3>
            <span className="badge badge--pink">
              UC-30 & WFL 1.2.3
            </span>
          </div>

          <div style={{ maxHeight: 440, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 12 }}>
            {board?.mealChangeLogs && board.mealChangeLogs.length > 0 ? (
              board.mealChangeLogs.map((log, idx) => {
                const isCancel = log.changeType === 'AutoCancelled' || log.changeType === 'ParentCancelled';
                return (
                  <div
                    key={idx}
                    style={{
                      padding: '12px 14px',
                      borderRadius: 'var(--radius-sm)',
                      border: 'var(--border)',
                      boxShadow: 'var(--shadow-pop-active)',
                      background: isCancel ? 'var(--color-secondary-soft)' : 'var(--color-quaternary-soft)',
                      fontSize: 'var(--fs-sm)',
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 4 }}>
                      <div>
                        <strong style={{ fontSize: 'var(--fs-base)', color: 'var(--color-fg)' }}>{log.studentName}</strong>
                        <span style={{ marginLeft: 6, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>
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

                    <p style={{ margin: '4px 0 0', color: 'var(--color-fg)', fontSize: 'var(--fs-xs)', fontStyle: 'italic' }}>
                      {log.reason}
                    </p>

                    <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 6, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                      <span>Thời điểm: {new Date(log.timestamp).toLocaleTimeString('vi-VN')}</span>
                      <span style={{ color: '#059669', fontWeight: 600 }}>✓ Trước giờ chốt {effectiveCutoffLabel}</span>
                    </div>
                  </div>
                );
              })
            ) : (
              <div style={{ textAlign: 'center', padding: '40px 20px', color: 'var(--color-muted-fg)' }}>
                <UtensilsCrossed size={36} color="var(--color-border)" style={{ marginBottom: 8 }} />
                <p style={{ margin: 0 }}>Không có biến động hủy/khôi phục suất ăn nào trong ngày.</p>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Bottom Section: Minimal Operational Student List & Filters (UC-33) */}
      <div className="card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12, marginBottom: 16 }}>
          <div>
            <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
              <Users size={18} color="var(--color-accent)" />
              Danh sách tối thiểu phục vụ vận hành theo ngày ({filteredStudentMeals.length} học sinh)
            </h3>
            <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
              Phục vụ nhân viên bán trú và giáo viên đối chiếu phát khay ăn theo từng lớp (UC-33).
            </p>
          </div>

          {/* Filters */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
              <Filter size={15} color="var(--color-muted-fg)" />
              <select
                className="select"
                value={selectedClassFilter}
                onChange={(e) => setSelectedClassFilter(e.target.value)}
                style={{ minWidth: 150, padding: '6px 12px', fontSize: 'var(--fs-sm)' }}
              >
                <option value="ALL">Tất cả các lớp</option>
                {board?.classBreakdowns?.map((c) => (
                  <option key={c.classId} value={String(c.classId)}>
                    Lớp {c.className} (Khối {c.grade})
                  </option>
                ))}
              </select>
            </div>

            <select
              className="select"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as 'ALL' | 'ACTIVE' | 'CANCELLED')}
              style={{ minWidth: 180, padding: '6px 12px', fontSize: 'var(--fs-sm)' }}
            >
              <option value="ACTIVE">Đang còn hiệu lực (Thực ăn)</option>
              <option value="CANCELLED">Đã hủy trong ngày</option>
              <option value="ALL">Tất cả bản ghi đăng ký</option>
            </select>
          </div>
        </div>

        <div style={{ overflowX: 'auto' }}>
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
              {filteredStudentMeals.length > 0 ? (
                filteredStudentMeals.map((item, idx) => (
                  <tr key={item.registrationId} style={{ borderBottom: '1px solid var(--color-border)' }}>
                    <td style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 600 }}>{idx + 1}</td>
                    <td style={{ padding: '10px 12px', fontWeight: 700, color: 'var(--color-accent)' }}>{item.studentCode}</td>
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
                  <td colSpan={7} style={{ textAlign: 'center', padding: '28px', color: 'var(--color-muted-fg)' }}>
                    {SYSTEM_MESSAGES.MSG43.content}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* MODAL UC-32: Cấu hình Ngày phục vụ, Hạn đăng ký/hủy & Thực đơn */}
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
            <label className="label" htmlFor="cfg-serving">Trạng thái phục vụ ngày {new Date(selectedDate).toLocaleDateString('vi-VN')}</label>
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
            <label className="label" htmlFor="cfg-cutoff">Hạn đăng ký / hủy suất ăn trong ngày (Quy tắc Tân Lập A: 08:15)</label>
            <input
              id="cfg-cutoff"
              type="time"
              className="input"
              value={cfgCutoffTime}
              onChange={(e) => setCfgCutoffTime(e.target.value)}
            />
          </div>

          <div className="field" style={{ marginBottom: 14 }}>
            <label className="label" htmlFor="cfg-remarks">Thực đơn trưa hoặc Ghi chú phục vụ (hiển thị cho Phụ huynh)</label>
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

      {/* MODAL 1: Confirm Lock Headcount (GeometricModal) */}
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

      {/* MODAL 2: Printable Handover Sheet (GeometricModal) */}
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
