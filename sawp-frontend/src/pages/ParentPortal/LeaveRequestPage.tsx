import { useEffect, useState } from 'react';
import { Calendar, AlertCircle, Plus, Send, X, ShieldAlert, CheckCircle2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { leaveRequestApi, type LeaveRequestItem } from '../../services/implementations/leaveRequestApi';
import { apiClient } from '../../services/apiClient';

interface LinkedChild {
  studentId: number;
  fullName: string;
  studentCode: string;
  currentClassName?: string | null;
}

export default function LeaveRequestPage() {
  const [requests, setRequests] = useState<LeaveRequestItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);

  // Form states
  const [children, setChildren] = useState<LinkedChild[]>([]);
  const [selectedStudentId, setSelectedStudentId] = useState<number>(0);
  const [startDate, setStartDate] = useState(new Date().toISOString().split('T')[0]);
  const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);
  const [session, setSession] = useState<number>(3); // 1: Sáng, 2: Chiều, 3: Cả ngày
  const [keepLunch, setKeepLunch] = useState<boolean>(false);
  const [reason, setReason] = useState('');
  const [attachmentUrl, setAttachmentUrl] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const calculateDays = (start: string, end: string) => {
    if (!start || !end) return 1;
    const s = new Date(start);
    const e = new Date(end);
    const diffDays = Math.round((e.getTime() - s.getTime()) / (1000 * 3600 * 24)) + 1;
    return Math.max(1, diffDays);
  };
  const durationDays = calculateDays(startDate, endDate);

  const fetchRequests = async () => {
    try {
      setLoading(true);
      const res = await leaveRequestApi.getMyRequests();
      if (res.success) {
        setRequests(res.data);
      }
    } catch {
      toast.error('Không thể tải danh sách đơn xin nghỉ.');
    } finally {
      setLoading(false);
    }
  };

  const fetchChildren = async () => {
    try {
      // Try fetch linked children or students
      const res = await apiClient.get('/guardians/my-children').catch(() => null);
      if (res?.data?.data && Array.isArray(res.data.data)) {
        setChildren(res.data.data);
        if (res.data.data.length > 0) {
          setSelectedStudentId(res.data.data[0].studentId);
        }
      } else {
        // Fallback for demo: load first student from students list
        const fallback = await apiClient.get('/students?pageSize=5').catch(() => null);
        if (fallback?.data?.data?.items) {
          const mapped = fallback.data.data.items.map((s: { id: number; fullName: string; studentCode: string }) => ({
            studentId: s.id,
            fullName: s.fullName,
            studentCode: s.studentCode,
          }));
          setChildren(mapped);
          if (mapped.length > 0) setSelectedStudentId(mapped[0].studentId);
        }
      }
    } catch {
      // silent
    }
  };

  useEffect(() => {
    fetchRequests();
    fetchChildren();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedStudentId) {
      toast.error('Vui lòng chọn học sinh.');
      return;
    }
    if (!reason.trim()) {
      toast.error('Vui lòng nhập lý do xin nghỉ.');
      return;
    }

    try {
      setSubmitting(true);
      const res = await leaveRequestApi.submit({
        studentId: selectedStudentId,
        startDate,
        endDate,
        session,
        keepLunchService: keepLunch,
        reason,
        doctorNoteAttachmentUrl: attachmentUrl.trim() || undefined,
      });

      if (res.success) {
        toast.success(
          durationDays >= 3
            ? 'Đã gửi đơn nghỉ (≥ 3 ngày)! GVCN sẽ xác nhận và chuyển Ban Giám Hiệu phê duyệt.'
            : 'Đã gửi đơn xin nghỉ thành công! GVCN sẽ sớm xem xét.',
          { duration: 5000 }
        );
        setShowModal(false);
        setReason('');
        setAttachmentUrl('');
        fetchRequests();
      } else {
        toast.error(res.message || 'Gửi đơn thất bại.');
      }
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Có lỗi xảy ra khi gửi đơn.';
      toast.error(errorMsg);
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancel = async (id: number) => {
    if (!window.confirm('Bạn có chắc chắn muốn hủy đơn xin nghỉ này?')) return;
    try {
      const res = await leaveRequestApi.cancel(id);
      if (res.success) {
        toast.success('Đã hủy đơn thành công.');
        fetchRequests();
      } else {
        toast.error(res.message);
      }
    } catch {
      toast.error('Không thể hủy đơn.');
    }
  };

  return (
    <div style={{ padding: '24px', maxWidth: '1200px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 800, color: '#0F172A', margin: 0 }}>Đơn xin nghỉ học cho con</h1>
          <p style={{ color: '#64748B', marginTop: '4px', fontSize: '14px' }}>
            Quản lý gửi đơn xin nghỉ trực tuyến, đồng bộ chuyên cần lớp học và suất ăn bán trú.
          </p>
        </div>
        <button
          onClick={() => setShowModal(true)}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '8px',
            backgroundColor: '#2563EB',
            color: '#FFFFFF',
            border: 'none',
            borderRadius: '10px',
            padding: '12px 20px',
            fontWeight: 700,
            cursor: 'pointer',
            boxShadow: '0 4px 12px rgba(37, 99, 235, 0.25)',
          }}
        >
          <Plus size={18} /> Gửi đơn xin nghỉ mới
        </button>
      </div>

      {/* Rules Banner */}
      <div
        style={{
          backgroundColor: '#EFF6FF',
          borderLeft: '4px solid #3B82F6',
          padding: '16px 20px',
          borderRadius: '10px',
          marginBottom: '24px',
          display: 'flex',
          gap: '12px',
          alignItems: 'flex-start',
        }}
      >
        <AlertCircle size={22} color="#2563EB" style={{ marginTop: '2px', flexShrink: 0 }} />
        <div style={{ fontSize: '13px', color: '#1E3A8A', lineHeight: 1.6 }}>
          <strong>Quy tắc bán trú Tân Lập A:</strong> Nhà bếp chốt thực phẩm lúc <strong>08h15</strong> mỗi ngày. 
          Đơn được duyệt trước 08h15 của ngày áp dụng sẽ được tự động ghi nhận hủy suất ăn trước giờ chốt. 
          Nếu con chỉ nghỉ học tiết sáng nhưng trưa vẫn vào trường ăn bán trú để học buổi chiều, vui lòng tích chọn ô <em>"Vẫn giữ suất ăn bán trú"</em>.
        </div>
      </div>

      {/* List of Requests */}
      {loading ? (
        <div style={{ textAlign: 'center', padding: '40px', color: '#64748B' }}>Đang tải danh sách đơn...</div>
      ) : requests.length === 0 ? (
        <div
          style={{
            textAlign: 'center',
            padding: '60px 20px',
            backgroundColor: '#F8FAFC',
            borderRadius: '16px',
            border: '2px dashed #CBD5E1',
          }}
        >
          <Calendar size={48} color="#94A3B8" style={{ marginBottom: '12px' }} />
          <h3 style={{ margin: 0, color: '#334155' }}>Chưa có đơn xin nghỉ nào</h3>
          <p style={{ color: '#64748B', fontSize: '14px', marginTop: '6px' }}>
            Bấm vào nút "Gửi đơn xin nghỉ mới" ở trên để tạo đơn đầu tiên cho con.
          </p>
        </div>
      ) : (
        <div style={{ display: 'grid', gap: '16px' }}>
          {requests.map((r) => (
            <div
              key={r.id}
              style={{
                backgroundColor: '#FFFFFF',
                borderRadius: '14px',
                border: '1px solid #E2E8F0',
                padding: '20px',
                boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
                display: 'flex',
                flexDirection: 'column',
                gap: '12px',
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '8px' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <span
                    style={{
                      fontFamily: 'monospace',
                      fontWeight: 700,
                      backgroundColor: '#F1F5F9',
                      padding: '4px 8px',
                      borderRadius: '6px',
                      color: '#475569',
                      fontSize: '13px',
                    }}
                  >
                    {r.requestNumber}
                  </span>
                  <strong style={{ fontSize: '16px', color: '#0F172A' }}>{r.studentName}</strong>
                  {r.className && <span style={{ color: '#64748B', fontSize: '14px' }}>({r.className})</span>}
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  {r.status === 'Submitted' && (
                    <span
                      style={{
                        backgroundColor: '#FEF3C7',
                        color: '#B45309',
                        padding: '4px 12px',
                        borderRadius: '20px',
                        fontWeight: 700,
                        fontSize: '12px',
                      }}
                    >
                      {(r.totalDays || 1) >= 3 ? 'Chờ GV chuyển BGH' : 'Chờ GVCN duyệt'}
                    </span>
                  )}
                  {r.status === 'UnderReview' && (
                    <span
                      style={{
                        backgroundColor: '#FAF5FF',
                        color: '#7E22CE',
                        border: '1px solid #E9D5FF',
                        padding: '4px 12px',
                        borderRadius: '20px',
                        fontWeight: 700,
                        fontSize: '12px',
                      }}
                    >
                      Chờ BGH duyệt
                    </span>
                  )}
                  {r.status === 'Approved' && (
                    <span
                      style={{
                        backgroundColor: '#DCFCE7',
                        color: '#15803D',
                        padding: '4px 12px',
                        borderRadius: '20px',
                        fontWeight: 700,
                        fontSize: '12px',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '4px',
                      }}
                    >
                      <CheckCircle2 size={14} /> Đã phê duyệt
                    </span>
                  )}
                  {r.status === 'Rejected' && (
                    <span
                      style={{
                        backgroundColor: '#FEE2E2',
                        color: '#B91C1C',
                        padding: '4px 12px',
                        borderRadius: '20px',
                        fontWeight: 700,
                        fontSize: '12px',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '4px',
                      }}
                    >
                      <ShieldAlert size={14} /> Đã từ chối
                    </span>
                  )}
                  {r.status === 'Cancelled' && (
                    <span
                      style={{
                        backgroundColor: '#F1F5F9',
                        color: '#64748B',
                        padding: '4px 12px',
                        borderRadius: '20px',
                        fontWeight: 700,
                        fontSize: '12px',
                      }}
                    >
                      Đã hủy
                    </span>
                  )}
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '12px', fontSize: '14px', color: '#334155' }}>
                <div>
                  <span style={{ color: '#64748B' }}>Thời gian nghỉ:</span>{' '}
                  <strong>
                    {new Date(r.startDate).toLocaleDateString('vi-VN')}{' '}
                    {r.startDate !== r.endDate && `→ ${new Date(r.endDate).toLocaleDateString('vi-VN')}`}
                  </strong>{' '}
                  <span style={{ fontSize: '12px', color: '#64748B' }}>
                    ({r.totalDays || 1} ngày - {(r.totalDays || 1) >= 3 ? 'Chuyển cấp BGH' : 'Thông thường'})
                  </span>
                </div>
                <div>
                  <span style={{ color: '#64748B' }}>Buổi nghỉ:</span> <strong>{r.sessionName}</strong>
                </div>
                <div>
                  <span style={{ color: '#64748B' }}>Suất ăn trưa:</span>{' '}
                  {r.keepLunchService ? (
                    <span style={{ color: '#0284C7', fontWeight: 600 }}>Giữ suất ăn theo yêu cầu</span>
                  ) : r.isLunchCancelled ? (
                    <span style={{ color: '#16A34A', fontWeight: 600 }}>Tự động ghi nhận hủy trước 08h15</span>
                  ) : (
                    <span style={{ color: '#64748B' }}>Không hủy (sau 08h15 hoặc không ăn bán trú)</span>
                  )}
                </div>
              </div>

              <div style={{ backgroundColor: '#F8FAFC', padding: '12px', borderRadius: '8px', fontSize: '14px' }}>
                <span style={{ color: '#64748B', fontWeight: 600 }}>Lý do xin nghỉ: </span>
                <span style={{ color: '#1E293B' }}>{r.reason}</span>
                {r.doctorNoteAttachmentUrl && (
                  <div style={{ marginTop: '6px', fontSize: '13px', color: '#0284C7' }}>
                    📎 <strong>Tài liệu đính kèm:</strong> {r.doctorNoteAttachmentUrl}
                  </div>
                )}
                {r.reviewerNotes && (
                  <div style={{ marginTop: '8px', paddingTop: '8px', borderTop: '1px dashed #CBD5E1', color: r.status === 'Rejected' ? '#B91C1C' : '#0369A1' }}>
                    <strong>Ý kiến xử lý: </strong>
                    <span>{r.reviewerNotes}</span>
                  </div>
                )}
              </div>

              {r.status === 'Submitted' && (
                <div style={{ display: 'flex', justifyContent: 'flex-end', paddingTop: '8px' }}>
                  <button
                    onClick={() => handleCancel(r.id)}
                    style={{
                      backgroundColor: 'transparent',
                      color: '#EF4444',
                      border: '1px solid #FCA5A5',
                      padding: '6px 14px',
                      borderRadius: '8px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: 'pointer',
                    }}
                  >
                    Hủy đơn này
                  </button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {/* CREATE MODAL */}
      {showModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.6)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            padding: '16px',
          }}
        >
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '20px',
              maxWidth: '560px',
              width: '100%',
              maxHeight: '90vh',
              overflowY: 'auto',
              boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
              padding: '24px',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
              <h2 style={{ fontSize: '20px', fontWeight: 800, color: '#0F172A', margin: 0 }}>Gửi đơn xin nghỉ học</h2>
              <button onClick={() => setShowModal(false)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#64748B' }}>
                <X size={20} />
              </button>
            </div>

            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {/* Chọn học sinh */}
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, color: '#334155', marginBottom: '6px' }}>Học sinh *</label>
                {children.length > 0 ? (
                  <select
                    value={selectedStudentId}
                    onChange={(e) => setSelectedStudentId(Number(e.target.value))}
                    style={{
                      width: '100%',
                      padding: '10px 14px',
                      borderRadius: '10px',
                      border: '1px solid #CBD5E1',
                      fontSize: '14px',
                      outline: 'none',
                    }}
                  >
                    {children.map((c) => (
                      <option key={c.studentId} value={c.studentId}>
                        {c.fullName} ({c.studentCode}) {c.currentClassName ? `- ${c.currentClassName}` : ''}
                      </option>
                    ))}
                  </select>
                ) : (
                  <input
                    type="number"
                    placeholder="Nhập ID học sinh (VD: 1)"
                    value={selectedStudentId || ''}
                    onChange={(e) => setSelectedStudentId(Number(e.target.value))}
                    style={{ width: '100%', padding: '10px', borderRadius: '8px', border: '1px solid #CBD5E1' }}
                    required
                  />
                )}
              </div>

              {/* Ngày nghỉ */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, color: '#334155', marginBottom: '6px' }}>Từ ngày *</label>
                  <input
                    type="date"
                    value={startDate}
                    onChange={(e) => {
                      setStartDate(e.target.value);
                      if (e.target.value > endDate) setEndDate(e.target.value);
                    }}
                    style={{ width: '100%', padding: '10px', borderRadius: '10px', border: '1px solid #CBD5E1', fontSize: '14px' }}
                    required
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, color: '#334155', marginBottom: '6px' }}>Đến ngày *</label>
                  <input
                    type="date"
                    value={endDate}
                    min={startDate}
                    onChange={(e) => setEndDate(e.target.value)}
                    style={{ width: '100%', padding: '10px', borderRadius: '10px', border: '1px solid #CBD5E1', fontSize: '14px' }}
                    required
                  />
                </div>
              </div>

              {/* Dynamic Guidance Banner for Flowchart Routing */}
              {durationDays >= 3 ? (
                <div
                  style={{
                    backgroundColor: '#FAF5FF',
                    border: '1px solid #E9D5FF',
                    borderRadius: '10px',
                    padding: '12px 14px',
                    display: 'flex',
                    gap: '10px',
                    alignItems: 'flex-start',
                  }}
                >
                  <ShieldAlert size={20} color="#9333EA" style={{ marginTop: '2px', flexShrink: 0 }} />
                  <div style={{ fontSize: '13px', color: '#6B21A8', lineHeight: 1.5 }}>
                    <strong>Thời gian nghỉ: {durationDays} ngày (Từ 3 ngày trở lên)</strong>
                    <div style={{ marginTop: '2px' }}>
                      Theo quy định, Giáo viên chủ nhiệm sẽ xác nhận yêu cầu và chuyển tiếp lên <strong>Ban Giám Hiệu</strong> xem xét phê duyệt.
                    </div>
                  </div>
                </div>
              ) : (
                <div
                  style={{
                    backgroundColor: '#F0FDF4',
                    border: '1px solid #BBF7D0',
                    borderRadius: '10px',
                    padding: '12px 14px',
                    display: 'flex',
                    gap: '10px',
                    alignItems: 'flex-start',
                  }}
                >
                  <CheckCircle2 size={20} color="#16A34A" style={{ marginTop: '2px', flexShrink: 0 }} />
                  <div style={{ fontSize: '13px', color: '#166534', lineHeight: 1.5 }}>
                    <strong>Thời gian nghỉ: {durationDays} ngày (Dưới 3 ngày)</strong>
                    <div style={{ marginTop: '2px' }}>
                      Đơn nghỉ thông thường sẽ do <strong>Giáo viên chủ nhiệm</strong> trực tiếp xem xét và phê duyệt.
                    </div>
                  </div>
                </div>
              )}

              {/* Buổi nghỉ */}
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, color: '#334155', marginBottom: '8px' }}>Buổi nghỉ *</label>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '8px' }}>
                  {[
                    { val: 3, label: 'Cả ngày' },
                    { val: 1, label: 'Buổi sáng' },
                    { val: 2, label: 'Buổi chiều' },
                  ].map((s) => (
                    <button
                      key={s.val}
                      type="button"
                      onClick={() => setSession(s.val)}
                      style={{
                        padding: '10px',
                        borderRadius: '8px',
                        border: session === s.val ? '2px solid #2563EB' : '1px solid #CBD5E1',
                        backgroundColor: session === s.val ? '#EFF6FF' : '#FFFFFF',
                        color: session === s.val ? '#1D4ED8' : '#475569',
                        fontWeight: session === s.val ? 700 : 500,
                        cursor: 'pointer',
                      }}
                    >
                      {s.label}
                    </button>
                  ))}
                </div>
              </div>

              {/* Ngoại lệ giữ suất ăn bán trú */}
              {session === 1 && (
                <div style={{ backgroundColor: '#F0FDF4', padding: '12px', borderRadius: '10px', border: '1px solid #BBF7D0' }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '10px', cursor: 'pointer', fontSize: '13px', color: '#166534', fontWeight: 600 }}>
                    <input
                      type="checkbox"
                      checked={keepLunch}
                      onChange={(e) => setKeepLunch(e.target.checked)}
                      style={{ width: '16px', height: '16px' }}
                    />
                    <span>[Ngoại lệ] Con nghỉ buổi sáng nhưng trưa vẫn vào trường ăn bán trú để học buổi chiều</span>
                  </label>
                </div>
              )}

              {/* Lý do */}
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, color: '#334155', marginBottom: '6px' }}>Lý do xin nghỉ học *</label>
                <textarea
                  rows={3}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder="Ví dụ: Cháu bị sốt nhẹ, gia đình xin phép cô cho cháu nghỉ một buổi để đi khám..."
                  style={{ width: '100%', padding: '10px 14px', borderRadius: '10px', border: '1px solid #CBD5E1', fontSize: '14px', outline: 'none' }}
                  required
                />
              </div>

              {/* Tài liệu đính kèm */}
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, color: '#334155', marginBottom: '6px' }}>
                  Tài liệu đính kèm (giấy khám bệnh / minh chứng nếu có)
                </label>
                <input
                  type="text"
                  value={attachmentUrl}
                  onChange={(e) => setAttachmentUrl(e.target.value)}
                  placeholder="Ví dụ: Giấy chỉ định bác sĩ BV Nhi Trung Ương, Sổ khám bệnh số #889..."
                  style={{ width: '100%', padding: '10px 14px', borderRadius: '10px', border: '1px solid #CBD5E1', fontSize: '14px', outline: 'none' }}
                />
              </div>

              {/* Nút gửi */}
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '12px' }}>
                <button
                  type="button"
                  onClick={() => setShowModal(false)}
                  style={{ padding: '10px 18px', borderRadius: '10px', border: '1px solid #CBD5E1', background: '#F8FAFC', cursor: 'pointer', fontWeight: 600 }}
                >
                  Đóng
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    padding: '10px 22px',
                    borderRadius: '10px',
                    border: 'none',
                    backgroundColor: '#2563EB',
                    color: '#FFFFFF',
                    fontWeight: 700,
                    cursor: 'pointer',
                    opacity: submitting ? 0.7 : 1,
                  }}
                >
                  <Send size={16} /> {submitting ? 'Đang gửi...' : 'Gửi đơn'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
