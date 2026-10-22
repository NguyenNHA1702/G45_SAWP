import { useEffect, useState } from 'react';
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
  X,
} from 'lucide-react';
import { lunchApi } from '../../services/implementations/lunchApi';
import type { DailyMealBoard } from '../../types/lunch.types';
import { LUNCH_CUTOFF_LABEL } from '../../constants/lunch';
import { GeometricModal } from '../../components/common/GeometricModal';

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

  // Modals
  const [showLockModal, setShowLockModal] = useState<boolean>(false);
  const [lockNotes, setLockNotes] = useState<string>(`Đã kiểm tra đối chiếu danh sách điểm danh và đơn nghỉ lúc ${LUNCH_CUTOFF_LABEL}`);
  const [showPrintModal, setShowPrintModal] = useState<boolean>(false);

  const fetchBoard = async (date: string) => {
    try {
      setLoading(true);
      const data = await lunchApi.getDailyMealBoard(date);
      setBoard(data);
    } catch {
      setMessage({ text: 'Không thể tải bảng tổng hợp suất ăn bán trú.', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchBoard(selectedDate);
  }, [selectedDate]);

  const handleLockDay = async () => {
    try {
      setActionLoading(true);
      await lunchApi.lockDailyMeal({
        date: selectedDate,
        notes: lockNotes,
      });
      setMessage({ text: `Đã chốt số liệu suất ăn gửi nhà bếp thành công (Mốc ${LUNCH_CUTOFF_LABEL})!`, type: 'success' });
      setShowLockModal(false);
      await fetchBoard(selectedDate);
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Chốt suất ăn thất bại.';
      setMessage({ text: errorMsg, type: 'error' });
    } finally {
      setActionLoading(false);
    }
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
            <span className="badge badge--mint" style={{ marginBottom: 8 }}>
              <Sparkles size={14} /> Tổ Bán Trú & Bếp Ăn
            </span>
            <h1 style={{ margin: '4px 0 8px' }}>
              Bảng Vận Hành <span className="squiggle">Suất Ăn Bán Trú</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)' }}>
              Giám sát số lượng khẩu phần trực tiếp, tự động trừ suất khi GVCN duyệt đơn nghỉ trước {LUNCH_CUTOFF_LABEL}.
            </p>
          </div>

          {/* Quick Date Control */}
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

      {/* Status Bar: Cutoff & Lock state */}
      {board && (
        <div
          className="card card--featured"
          style={{
            marginBottom: 28,
            padding: '16px 24px',
            background: board.isLocked ? 'var(--color-quaternary-soft)' : 'var(--color-tertiary-soft)',
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
                background: board.isLocked ? 'var(--color-quaternary)' : 'var(--color-tertiary)',
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
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <strong style={{ fontSize: 'var(--fs-md)', color: 'var(--color-fg)', fontFamily: 'var(--font-heading)' }}>
                  {board.isLocked ? 'ĐÃ CHỐT SỐ LIỆU SUẤT ĂN GỬI NHÀ BẾP' : 'ĐANG CẬP NHẬT BIẾN ĐỘNG TRỰC TIẾP'}
                </strong>
                <span className={`badge ${board.isLocked ? 'badge--mint' : 'badge--amber'}`}>
                  {board.isLocked ? `Đã khóa lúc ${LUNCH_CUTOFF_LABEL}` : `Hạn chốt: ${LUNCH_CUTOFF_LABEL}`}
                </span>
              </div>
              <p style={{ margin: '4px 0 0', fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
                {board.isLocked
                  ? `Dữ liệu ngày ${new Date(board.date).toLocaleDateString('vi-VN')} đã được khóa. Mọi phát sinh mới được xử lý theo quỹ dự phòng.`
                  : `Đơn xin nghỉ học được GVCN phê duyệt trước ${LUNCH_CUTOFF_LABEL} sẽ tự động được hệ thống giảm trừ và không tính phí.`}
              </p>
            </div>
          </div>

          {/* Action buttons */}
          <div style={{ display: 'flex', gap: 12 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setShowPrintModal(true)}
            >
              <Printer size={16} />
              In Phiếu Giao Bếp
            </button>

            {!board.isLocked && (
              <button
                type="button"
                className="btn btn--primary"
                onClick={() => setShowLockModal(true)}
              >
                <Lock size={16} />
                Chốt Suất Ăn (Mốc {LUNCH_CUTOFF_LABEL})
              </button>
            )}
          </div>
        </div>
      )}

      {/* 4 Live KPI Cards (Playful Sticker Cards with Floating Circle Icons) */}
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
              Đăng ký ban đầu
            </div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 800, color: 'var(--color-fg)', fontFamily: 'var(--font-heading)' }}>
              {board.totalInitialRegistered}{' '}
              <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>suất</span>
            </div>
            <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
              Theo danh sách học sinh đăng ký bán trú tháng
            </p>
          </div>

          {/* Card 2: Auto Cancelled */}
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
              Hủy tự động trước {LUNCH_CUTOFF_LABEL}
            </div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 800, color: '#e11d48', fontFamily: 'var(--font-heading)' }}>
              -{board.totalAutoCancelled}{' '}
              <span style={{ fontSize: 'var(--fs-sm)', fontWeight: 500, color: 'var(--color-muted-fg)' }}>suất</span>
            </div>
            <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: '#e11d48' }}>
              Trừ tự động từ Đơn xin nghỉ đã duyệt
            </p>
          </div>

          {/* Card 3: Net Active To Cook (Hero Featured Card) */}
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
              Thực tế bếp cần chuẩn bị
            </div>
            <div style={{ fontSize: 'var(--fs-2xl)', fontWeight: 900, color: '#065f46', fontFamily: 'var(--font-heading)' }}>
              {board.totalActiveToCook}{' '}
              <span style={{ fontSize: 'var(--fs-md)', fontWeight: 700, color: '#047857' }}>khẩu phần</span>
            </div>
            <p style={{ margin: '6px 0 0', fontSize: 'var(--fs-xs)', color: '#047857', fontWeight: 600 }}>
              = Đăng ký ({board.totalInitialRegistered}) - Hủy ({board.totalAutoCancelled})
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
              Có đơn nghỉ nhưng GVCN điểm danh Có mặt
            </p>
          </div>
        </div>
      )}

      {/* Main Content Layout: Two Columns */}
      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1.4fr) minmax(0, 1fr)', gap: 24, alignItems: 'start' }}>
        {/* Left Column: Class Meal Breakdown Table */}
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h3 style={{ margin: 0, fontSize: 'var(--fs-md)', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
              <FileSpreadsheet size={18} color="var(--color-accent)" />
              Phân bổ số lượng theo từng Lớp học
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
                      <tr key={cls.classId} style={{ borderBottom: '1px solid var(--color-border)' }}>
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
              Real-time
            </span>
          </div>

          <div style={{ maxHeight: 440, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 12 }}>
            {board?.mealChangeLogs && board.mealChangeLogs.length > 0 ? (
              board.mealChangeLogs.map((log, idx) => (
                <div
                  key={idx}
                  style={{
                    padding: '12px 14px',
                    borderRadius: 'var(--radius-sm)',
                    border: 'var(--border)',
                    boxShadow: 'var(--shadow-pop-active)',
                    background: log.changeType === 'AutoCancelled' ? 'var(--color-secondary-soft)' : 'var(--color-quaternary-soft)',
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
                    <span className={`badge ${log.changeType === 'AutoCancelled' ? 'badge--pink' : 'badge--mint'}`}>
                      {log.changeType === 'AutoCancelled' ? `Hủy trước ${LUNCH_CUTOFF_LABEL}` : 'Khôi phục suất'}
                    </span>
                  </div>

                  <p style={{ margin: '4px 0 0', color: 'var(--color-fg)', fontSize: 'var(--fs-xs)', fontStyle: 'italic' }}>
                    {log.reason}
                  </p>

                  <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 6, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                    <span>Thời điểm: {new Date(log.timestamp).toLocaleTimeString('vi-VN')}</span>
                    <span style={{ color: '#059669', fontWeight: 600 }}>✓ Hợp lệ hoàn tiền</span>
                  </div>
                </div>
              ))
            ) : (
              <div style={{ textAlign: 'center', padding: '40px 20px', color: 'var(--color-muted-fg)' }}>
                <UtensilsCrossed size={36} color="var(--color-border)" style={{ marginBottom: 8 }} />
                <p style={{ margin: 0 }}>Không có biến động hủy/khôi phục suất ăn nào trong ngày.</p>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* MODAL 1: Confirm Lock Headcount (GeometricModal) */}
      <GeometricModal
        open={showLockModal}
        onClose={() => setShowLockModal(false)}
        title={`Chốt số liệu Bán trú lúc ${LUNCH_CUTOFF_LABEL}`}
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
            Sau khi bấm chốt, hệ thống sẽ <strong>khóa bảng số liệu</strong> ngày hôm nay. Các đơn nghỉ gửi muộn sau {LUNCH_CUTOFF_LABEL} sẽ không tự động hoàn tiền theo quy định của nhà trường.
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
              (Thời điểm chốt số liệu: {LUNCH_CUTOFF_LABEL} sáng | Dữ liệu đồng bộ trực tiếp từ điểm danh & đơn xin nghỉ)
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
                <th style={{ border: '1px solid #94A3B8', padding: '6px 8px', textAlign: 'center' }}>Hủy đơn nghỉ</th>
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
              <div style={{ fontWeight: 700, fontSize: '0.82rem' }}>KẾ TOÁN BÁN TRÚ</div>
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
