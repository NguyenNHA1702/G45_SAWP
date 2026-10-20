import { useEffect, useState } from 'react';
import { 
  Sparkles, 
  UtensilsCrossed, 
  Calendar, 
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
  X
} from 'lucide-react';
import { lunchApi } from '../../services/implementations/lunchApi';
import type { DailyMealBoard } from '../../services/implementations/lunchApi';

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
  const [lockNotes, setLockNotes] = useState<string>('Đã kiểm tra đối chiếu danh sách điểm danh và đơn nghỉ lúc 08h15');
  const [showPrintModal, setShowPrintModal] = useState<boolean>(false);

  const fetchBoard = async (date: string) => {
    try {
      setLoading(true);
      const data = await lunchApi.getDailyMealBoard(date);
      setBoard(data);
    } catch (err: any) {
      console.error(err);
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
        notes: lockNotes
      });
      setMessage({ text: 'Đã chốt số liệu suất ăn gửi nhà bếp thành công (Mốc 08h15)!', type: 'success' });
      setShowLockModal(false);
      await fetchBoard(selectedDate);
    } catch (err: any) {
      setMessage({ text: err.response?.data?.message || 'Chốt suất ăn thất bại.', type: 'error' });
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div className="container pop-in" style={{ paddingBottom: 60 }}>
      {/* Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <span className="badge badge--amber" style={{ marginBottom: 8, display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <Sparkles size={14} /> Tổ Bán trú & Bếp ăn
            </span>
            <h1 style={{ margin: '4px 0 8px 0', fontSize: '1.9rem', fontWeight: 800 }}>
              Bảng Vận Hành <span className="squiggle">Suất Ăn Bán Trú</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: '0.95rem' }}>
              Giám sát số lượng khẩu phần trực tiếp, tự động trừ suất khi GVCN duyệt đơn nghỉ trước 08h15.
            </p>
          </div>

          {/* Quick Date Control */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, background: 'var(--color-surface)', padding: '6px 12px', borderRadius: 'var(--radius-md)', border: '1px solid var(--color-border)' }}>
              <Calendar size={16} color="var(--color-primary)" />
              <input
                type="date"
                value={selectedDate}
                onChange={(e) => setSelectedDate(e.target.value)}
                style={{ border: 'none', background: 'transparent', outline: 'none', fontWeight: 600, fontSize: '0.95rem' }}
              />
            </div>

            <button 
              className="btn btn--outline" 
              onClick={() => fetchBoard(selectedDate)}
              disabled={loading}
              title="Làm mới dữ liệu"
              style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: '8px 14px' }}
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
          style={{ 
            padding: '12px 16px', 
            borderRadius: 'var(--radius-md)', 
            marginBottom: 20, 
            display: 'flex', 
            alignItems: 'center', 
            justifyContent: 'space-between',
            background: message.type === 'success' ? 'rgba(16, 185, 129, 0.12)' : 'rgba(239, 68, 68, 0.12)',
            border: `1px solid ${message.type === 'success' ? '#10b981' : '#ef4444'}`,
            color: message.type === 'success' ? '#047857' : '#b91c1c'
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            {message.type === 'success' ? <CheckCircle2 size={18} /> : <AlertTriangle size={18} />}
            <span style={{ fontWeight: 600 }}>{message.text}</span>
          </div>
          <button onClick={() => setMessage(null)} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'inherit' }}>
            <X size={16} />
          </button>
        </div>
      )}

      {/* Status Bar: Cutoff & Lock state */}
      {board && (
        <div style={{
          background: board.isLocked ? 'rgba(16, 185, 129, 0.08)' : 'rgba(245, 158, 11, 0.08)',
          border: `1px solid ${board.isLocked ? 'rgba(16, 185, 129, 0.3)' : 'rgba(245, 158, 11, 0.3)'}`,
          borderRadius: 'var(--radius-lg)',
          padding: '14px 20px',
          marginBottom: 24,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: 16
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <div style={{
              width: 40,
              height: 40,
              borderRadius: '50%',
              background: board.isLocked ? '#10b981' : '#f59e0b',
              color: '#fff',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center'
            }}>
              {board.isLocked ? <Lock size={20} /> : <Clock size={20} />}
            </div>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <strong style={{ fontSize: '1.05rem', color: board.isLocked ? '#065f46' : '#92400e' }}>
                  {board.isLocked ? 'ĐÃ CHỐT SỐ LIỆU SUẤT ĂN GỬI NHÀ BẾP' : 'ĐANG CẬP NHẬT BIẾN ĐỘNG TRỰC TIẾP'}
                </strong>
                <span className={`badge ${board.isLocked ? 'badge--emerald' : 'badge--amber'}`} style={{ fontSize: '0.75rem' }}>
                  {board.isLocked ? 'Đã khóa lúc 08h15' : 'Hạn chốt: 08h15'}
                </span>
              </div>
              <div style={{ fontSize: '0.85rem', color: 'var(--color-muted-fg)', marginTop: 2 }}>
                {board.isLocked 
                  ? `Dữ liệu ngày ${new Date(board.date).toLocaleDateString('vi-VN')} đã được khóa. Mọi phát sinh mới được chuyển sang kho suất ăn dự phòng.`
                  : `Đơn xin nghỉ học được GVCN phê duyệt trước 08h15 sẽ tự động được hệ thống giảm trừ và không tính phí.`}
              </div>
            </div>
          </div>

          {/* Action buttons */}
          <div style={{ display: 'flex', gap: 10 }}>
            <button
              className="btn btn--outline"
              onClick={() => setShowPrintModal(true)}
              style={{ display: 'inline-flex', alignItems: 'center', gap: 8, padding: '9px 16px' }}
            >
              <Printer size={16} />
              In Phiếu Giao Bếp
            </button>

            {!board.isLocked && (
              <button
                className="btn btn--primary"
                onClick={() => setShowLockModal(true)}
                style={{ display: 'inline-flex', alignItems: 'center', gap: 8, padding: '9px 18px', fontWeight: 600 }}
              >
                <Lock size={16} />
                Chốt Suất Ăn (Mốc 08h15)
              </button>
            )}
          </div>
        </div>
      )}

      {/* 4 Live KPI Cards */}
      {board && (
        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
          gap: 16,
          marginBottom: 28
        }}>
          {/* Card 1: Registered Total */}
          <div className="card" style={{ padding: '20px', borderLeft: '4px solid #3b82f6' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--color-muted-fg)', textTransform: 'uppercase' }}>
                Đăng ký ban đầu
              </span>
              <div style={{ width: 36, height: 36, borderRadius: 8, background: 'rgba(59, 130, 246, 0.1)', color: '#3b82f6', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <UtensilsCrossed size={18} />
              </div>
            </div>
            <div style={{ fontSize: '2.2rem', fontWeight: 800, color: '#1e293b' }}>
              {board.totalInitialRegistered} <span style={{ fontSize: '1rem', fontWeight: 500, color: 'var(--color-muted-fg)' }}>suất</span>
            </div>
            <p style={{ margin: '6px 0 0 0', fontSize: '0.82rem', color: 'var(--color-muted-fg)' }}>
              Theo danh sách đăng ký bán trú tháng
            </p>
          </div>

          {/* Card 2: Auto Cancelled */}
          <div className="card" style={{ padding: '20px', borderLeft: '4px solid #ef4444' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--color-muted-fg)', textTransform: 'uppercase' }}>
                Hủy tự động trước 08h15
              </span>
              <div style={{ width: 36, height: 36, borderRadius: 8, background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <UserMinus size={18} />
              </div>
            </div>
            <div style={{ fontSize: '2.2rem', fontWeight: 800, color: '#ef4444' }}>
              -{board.totalAutoCancelled} <span style={{ fontSize: '1rem', fontWeight: 500, color: 'var(--color-muted-fg)' }}>suất</span>
            </div>
            <p style={{ margin: '6px 0 0 0', fontSize: '0.82rem', color: '#b91c1c' }}>
              Trừ tự động từ Đơn xin nghỉ đã duyệt
            </p>
          </div>

          {/* Card 3: Net Active To Cook */}
          <div className="card" style={{ padding: '20px', borderLeft: '4px solid #10b981', background: 'linear-gradient(to bottom, #ffffff, rgba(16, 185, 129, 0.03))' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: '0.85rem', fontWeight: 700, color: '#047857', textTransform: 'uppercase' }}>
                Thực tế bếp cần nấu
              </span>
              <div style={{ width: 36, height: 36, borderRadius: 8, background: 'rgba(16, 185, 129, 0.12)', color: '#10b981', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <Soup size={18} />
              </div>
            </div>
            <div style={{ fontSize: '2.4rem', fontWeight: 900, color: '#065f46' }}>
              {board.totalActiveToCook} <span style={{ fontSize: '1.1rem', fontWeight: 600, color: '#047857' }}>khẩu phần</span>
            </div>
            <p style={{ margin: '6px 0 0 0', fontSize: '0.82rem', color: '#047857', fontWeight: 500 }}>
              = Đăng ký ({board.totalInitialRegistered}) - Hủy ({board.totalAutoCancelled})
            </p>
          </div>

          {/* Card 4: Anomalies */}
          <div className="card" style={{ padding: '20px', borderLeft: '4px solid #8b5cf6' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
              <span style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--color-muted-fg)', textTransform: 'uppercase' }}>
                Phát sinh bất thường
              </span>
              <div style={{ width: 36, height: 36, borderRadius: 8, background: 'rgba(139, 92, 246, 0.1)', color: '#8b5cf6', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <ShieldAlert size={18} />
              </div>
            </div>
            <div style={{ fontSize: '2.2rem', fontWeight: 800, color: board.totalAnomalies > 0 ? '#7c3aed' : '#64748b' }}>
              {board.totalAnomalies} <span style={{ fontSize: '1rem', fontWeight: 500, color: 'var(--color-muted-fg)' }}>em</span>
            </div>
            <p style={{ margin: '6px 0 0 0', fontSize: '0.82rem', color: board.totalAnomalies > 0 ? '#7c3aed' : 'var(--color-muted-fg)' }}>
              Có đơn nghỉ nhưng GVCN báo "Có mặt"
            </p>
          </div>
        </div>
      )}

      {/* Main Content Layout: Two Columns */}
      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1.4fr) minmax(0, 1fr)', gap: 24, alignItems: 'start' }}>
        
        {/* Left Column: Class Meal Breakdown Table */}
        <div className="card" style={{ padding: 24 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h3 style={{ margin: 0, fontSize: '1.15rem', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 8 }}>
              <FileSpreadsheet size={18} color="var(--color-primary)" />
              Phân bổ số lượng theo từng Lớp học
            </h3>
            <span style={{ fontSize: '0.85rem', color: 'var(--color-muted-fg)' }}>
              {board?.classBreakdowns?.length || 0} lớp hoạt động
            </span>
          </div>

          <div className="table-responsive">
            <table className="table" style={{ width: '100%', fontSize: '0.9rem' }}>
              <thead>
                <tr style={{ background: 'var(--color-surface-hover)' }}>
                  <th style={{ padding: '10px 12px', textAlign: 'left' }}>Lớp</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Khối</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Đăng ký</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Đã hủy</th>
                  <th style={{ padding: '10px 12px', textAlign: 'center' }}>Thực ăn</th>
                  <th style={{ padding: '10px 12px', textAlign: 'left' }}>Tỷ lệ nhận</th>
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
                        <td style={{ padding: '12px', fontWeight: 700, color: 'var(--color-primary)' }}>
                          Lớp {cls.className}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'center' }}>
                          Khối {cls.grade}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'center', fontWeight: 600 }}>
                          {cls.initialRegistered}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'center', color: cls.cancelledCount > 0 ? '#ef4444' : 'var(--color-muted-fg)', fontWeight: cls.cancelledCount > 0 ? 700 : 400 }}>
                          {cls.cancelledCount > 0 ? `-${cls.cancelledCount}` : '0'}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'center', fontWeight: 800, color: '#047857', fontSize: '1rem' }}>
                          {cls.actualCount}
                        </td>
                        <td style={{ padding: '12px' }}>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                            <div style={{ flex: 1, height: 6, background: '#e2e8f0', borderRadius: 3, overflow: 'hidden' }}>
                              <div style={{ width: `${percentage}%`, height: '100%', background: percentage === 100 ? '#10b981' : '#f59e0b' }} />
                            </div>
                            <span style={{ fontSize: '0.78rem', color: 'var(--color-muted-fg)', minWidth: 32 }}>
                              {percentage}%
                            </span>
                          </div>
                        </td>
                      </tr>
                    );
                  })
                ) : (
                  <tr>
                    <td colSpan={6} style={{ textAlign: 'center', padding: '30px', color: 'var(--color-muted-fg)' }}>
                      Chưa có dữ liệu phân bổ cho ngày này.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* Right Column: Real-time Audit Logs */}
        <div className="card" style={{ padding: 24 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h3 style={{ margin: 0, fontSize: '1.15rem', fontWeight: 700, display: 'flex', alignItems: 'center', gap: 8 }}>
              <Clock size={18} color="#ef4444" />
              Nhật ký biến động suất ăn
            </h3>
            <span className="badge badge--rose" style={{ fontSize: '0.75rem' }}>
              Real-time
            </span>
          </div>

          <div style={{ maxHeight: 420, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 12 }}>
            {board?.mealChangeLogs && board.mealChangeLogs.length > 0 ? (
              board.mealChangeLogs.map((log, idx) => (
                <div 
                  key={idx}
                  style={{
                    padding: '12px 14px',
                    borderRadius: 'var(--radius-md)',
                    border: '1px solid var(--color-border)',
                    background: log.changeType === 'AutoCancelled' ? 'rgba(239, 68, 68, 0.04)' : 'rgba(16, 185, 129, 0.04)',
                    fontSize: '0.85rem'
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 4 }}>
                    <div>
                      <strong style={{ fontSize: '0.92rem', color: '#1e293b' }}>{log.studentName}</strong>
                      <span style={{ marginLeft: 6, color: 'var(--color-muted-fg)', fontSize: '0.8rem' }}>({log.studentCode}) - Lớp {log.className}</span>
                    </div>
                    <span 
                      className={`badge ${log.changeType === 'AutoCancelled' ? 'badge--rose' : 'badge--emerald'}`}
                      style={{ fontSize: '0.7rem' }}
                    >
                      {log.changeType === 'AutoCancelled' ? 'Hủy suất trước 08h15' : 'Khôi phục suất'}
                    </span>
                  </div>

                  <p style={{ margin: '4px 0 0 0', color: 'var(--color-muted-fg)', fontSize: '0.8rem', fontStyle: 'italic' }}>
                    {log.reason}
                  </p>

                  <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 6, fontSize: '0.75rem', color: '#94a3b8' }}>
                    <span>Thời điểm: {new Date(log.timestamp).toLocaleTimeString('vi-VN')}</span>
                    <span style={{ color: '#059669', fontWeight: 500 }}>✓ Hợp lệ hoàn tiền</span>
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

      {/* MODAL 1: Confirm Lock Headcount at 08:15 */}
      {showLockModal && (
        <div className="modal-backdrop">
          <div className="modal card" style={{ maxWidth: 500, padding: 28 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 16 }}>
              <div style={{ width: 44, height: 44, borderRadius: '50%', background: 'rgba(245, 158, 11, 0.15)', color: '#d97706', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <Lock size={22} />
              </div>
              <div>
                <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 800 }}>Chốt số liệu Bán trú lúc 08h15</h3>
                <p style={{ margin: 0, fontSize: '0.85rem', color: 'var(--color-muted-fg)' }}>Gửi số liệu chuẩn xác sang bếp trưởng</p>
              </div>
            </div>

            <div style={{ background: 'var(--color-surface-hover)', padding: '14px 18px', borderRadius: 'var(--radius-md)', marginBottom: 16, fontSize: '0.9rem' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
                <span>Tổng suất cần nấu hôm nay:</span>
                <strong style={{ color: '#047857', fontSize: '1.1rem' }}>{board?.totalActiveToCook} suất</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', color: '#b91c1c' }}>
                <span>Tổng suất hủy hợp lệ:</span>
                <strong>-{board?.totalAutoCancelled} suất</strong>
              </div>
            </div>

            <p style={{ fontSize: '0.88rem', color: 'var(--color-muted-fg)', marginBottom: 16 }}>
              Sau khi bấm chốt, hệ thống sẽ <strong>khóa bảng số liệu</strong> ngày hôm nay. Các đơn nghỉ gửi muộn sau 08h15 sẽ không tự động hoàn tiền theo quy định của nhà trường.
            </p>

            <div style={{ marginBottom: 20 }}>
              <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: 600, marginBottom: 6 }}>
                Ghi chú gửi Nhà bếp / Kế toán:
              </label>
              <textarea
                value={lockNotes}
                onChange={(e) => setLockNotes(e.target.value)}
                rows={3}
                style={{ width: '100%', padding: '10px 12px', borderRadius: 'var(--radius-md)', border: '1px solid var(--color-border)', fontSize: '0.9rem' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <button 
                className="btn btn--outline" 
                onClick={() => setShowLockModal(false)}
                disabled={actionLoading}
              >
                Hủy bỏ
              </button>
              <button 
                className="btn btn--primary" 
                onClick={handleLockDay}
                disabled={actionLoading}
                style={{ fontWeight: 700 }}
              >
                {actionLoading ? 'Đang chốt...' : 'Xác nhận chốt số liệu'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: Printable Handover Sheet (Phiếu Bàn Giao Nhà Bếp) */}
      {showPrintModal && (
        <div className="modal-backdrop">
          <div className="modal card" style={{ maxWidth: 750, padding: 32, background: '#fff', color: '#000' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', borderBottom: '2px solid #000', paddingBottom: 16, marginBottom: 20 }}>
              <div>
                <div style={{ fontSize: '0.85rem', fontWeight: 700, textTransform: 'uppercase' }}>PHÒNG GD&ĐT HUYỆN ĐAN PHƯỢNG</div>
                <div style={{ fontSize: '0.95rem', fontWeight: 800, textTransform: 'uppercase' }}>TRƯỜNG TIỂU HỌC TÂN LẬP A</div>
                <div style={{ fontSize: '0.78rem', color: '#555' }}>Hệ thống Quản lý Bán trú SAWP</div>
              </div>
              <div style={{ textAlign: 'right' }}>
                <div style={{ fontSize: '0.85rem', fontWeight: 700 }}>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
                <div style={{ fontSize: '0.8rem', fontStyle: 'italic' }}>Độc lập - Tự do - Hạnh phúc</div>
                <div style={{ fontSize: '0.8rem', marginTop: 4 }}>Ngày: {new Date(selectedDate).toLocaleDateString('vi-VN')}</div>
              </div>
            </div>

            <div style={{ textAlign: 'center', marginBottom: 20 }}>
              <h2 style={{ margin: '0 0 6px 0', fontSize: '1.4rem', fontWeight: 900, textTransform: 'uppercase' }}>
                PHIẾU BÀN GIAO SUẤT ĂN BÁN TRÚ CHO NHÀ BẾP
              </h2>
              <div style={{ fontSize: '0.85rem', fontStyle: 'italic' }}>
                (Thời điểm chốt số liệu: 08h15 sáng | Dữ liệu đồng bộ trực tiếp từ điểm danh & đơn xin nghỉ)
              </div>
            </div>

            {/* Metrics on Paper */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 12, marginBottom: 20, textAlign: 'center' }}>
              <div style={{ border: '1px solid #ccc', padding: '10px 8px', borderRadius: 4 }}>
                <div style={{ fontSize: '0.8rem', color: '#555' }}>TỔNG ĐĂNG KÝ:</div>
                <div style={{ fontSize: '1.3rem', fontWeight: 800 }}>{board?.totalInitialRegistered}</div>
              </div>
              <div style={{ border: '1px solid #ccc', padding: '10px 8px', borderRadius: 4 }}>
                <div style={{ fontSize: '0.8rem', color: '#555' }}>SUẤT HỦY HỢP LỆ:</div>
                <div style={{ fontSize: '1.3rem', fontWeight: 800, color: '#c00' }}>-{board?.totalAutoCancelled}</div>
              </div>
              <div style={{ border: '2px solid #000', padding: '10px 8px', borderRadius: 4, background: '#f9f9f9' }}>
                <div style={{ fontSize: '0.8rem', fontWeight: 700 }}>THỰC TẾ NẤU VÀ GIAO:</div>
                <div style={{ fontSize: '1.4rem', fontWeight: 900 }}>{board?.totalActiveToCook} suất</div>
              </div>
            </div>

            {/* Table for print */}
            <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 30, fontSize: '0.85rem' }}>
              <thead>
                <tr style={{ background: '#eee' }}>
                  <th style={{ border: '1px solid #999', padding: '6px 8px', textAlign: 'center' }}>STT</th>
                  <th style={{ border: '1px solid #999', padding: '6px 8px', textAlign: 'left' }}>Lớp</th>
                  <th style={{ border: '1px solid #999', padding: '6px 8px', textAlign: 'center' }}>Đăng ký</th>
                  <th style={{ border: '1px solid #999', padding: '6px 8px', textAlign: 'center' }}>Hủy đơn nghỉ</th>
                  <th style={{ border: '1px solid #999', padding: '6px 8px', textAlign: 'center' }}>Thực giao (Khay)</th>
                  <th style={{ border: '1px solid #999', padding: '6px 8px', textAlign: 'left' }}>Ký nhận của GV/Phụ trách</th>
                </tr>
              </thead>
              <tbody>
                {board?.classBreakdowns?.map((c, i) => (
                  <tr key={c.classId}>
                    <td style={{ border: '1px solid #999', padding: '6px 8px', textAlign: 'center' }}>{i + 1}</td>
                    <td style={{ border: '1px solid #999', padding: '6px 8px', fontWeight: 700 }}>Lớp {c.className}</td>
                    <td style={{ border: '1px solid #999', padding: '6px 8px', textAlign: 'center' }}>{c.initialRegistered}</td>
                    <td style={{ border: '1px solid #999', padding: '6px 8px', textAlign: 'center', color: c.cancelledCount > 0 ? '#c00' : '#000' }}>
                      {c.cancelledCount > 0 ? `-${c.cancelledCount}` : '0'}
                    </td>
                    <td style={{ border: '1px solid #999', padding: '6px 8px', textAlign: 'center', fontWeight: 800 }}>{c.actualCount}</td>
                    <td style={{ border: '1px solid #999', padding: '6px 8px' }}></td>
                  </tr>
                ))}
              </tbody>
            </table>

            {/* Signatures */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', textAlign: 'center', marginTop: 40 }}>
              <div>
                <div style={{ fontWeight: 700, fontSize: '0.85rem' }}>ĐẠI DIỆN NHÀ BẾP</div>
                <div style={{ fontSize: '0.78rem', fontStyle: 'italic', marginBottom: 50 }}>(Ký và ghi rõ họ tên)</div>
              </div>
              <div>
                <div style={{ fontWeight: 700, fontSize: '0.85rem' }}>KẾ TOÁN BÁN TRÚ</div>
                <div style={{ fontSize: '0.78rem', fontStyle: 'italic', marginBottom: 50 }}>(Ký và ghi rõ họ tên)</div>
              </div>
              <div>
                <div style={{ fontWeight: 700, fontSize: '0.85rem' }}>BAN GIÁM HIỆU DUYỆT</div>
                <div style={{ fontSize: '0.78rem', fontStyle: 'italic', marginBottom: 50 }}>(Ký và đóng dấu)</div>
              </div>
            </div>

            {/* Print Modal Buttons */}
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 24, borderTop: '1px solid #ddd', paddingTop: 16 }}>
              <button className="btn btn--outline" onClick={() => setShowPrintModal(false)}>
                Đóng
              </button>
              <button 
                className="btn btn--primary" 
                onClick={() => window.print()}
                style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
              >
                <Printer size={16} /> In Phiếu Bàn Giao
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
