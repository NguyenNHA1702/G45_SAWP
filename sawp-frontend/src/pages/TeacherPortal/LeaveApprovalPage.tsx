import { useEffect, useState } from 'react';
import { Check, X, ShieldAlert, CheckCircle2, UserCheck, MessageSquare, AlertTriangle } from 'lucide-react';
import toast from 'react-hot-toast';
import { leaveRequestApi, type LeaveRequestItem } from '../../services/implementations/leaveRequestApi';

export default function LeaveApprovalPage() {
  const [requests, setRequests] = useState<LeaveRequestItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState<string>('Submitted');

  // Modal Reject state
  const [rejectingItem, setRejectingItem] = useState<LeaveRequestItem | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [processing, setProcessing] = useState(false);

  const fetchRequests = async () => {
    try {
      setLoading(true);
      const res = await leaveRequestApi.getClassRequests({
        status: statusFilter === 'All' ? undefined : statusFilter,
      });
      if (res.success) {
        setRequests(res.data);
      }
    } catch {
      toast.error('Không thể tải danh sách đơn của lớp.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRequests();
  }, [statusFilter]);

  const handleApprove = async (item: LeaveRequestItem) => {
    if (!window.confirm(`Xác nhận PHÊ DUYỆT đơn xin nghỉ cho học sinh ${item.studentName}?`)) return;

    try {
      setProcessing(true);
      const res = await leaveRequestApi.review(item.id, {
        isApproved: true,
        reviewerNotes: 'GVCN đồng ý cho học sinh nghỉ học.',
      });

      if (res.success) {
        toast.success(`Đã duyệt đơn thành công! ${res.data.mealSyncMessage}`, { duration: 5000 });
        fetchRequests();
      } else {
        toast.error(res.message);
      }
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Có lỗi xảy ra.';
      toast.error(errorMsg);
    } finally {
      setProcessing(false);
    }
  };

  const handleConfirmReject = async () => {
    if (!rejectingItem) return;
    if (!rejectReason.trim()) {
      toast.error('Bắt buộc phải nhập lý do từ chối.');
      return;
    }

    try {
      setProcessing(true);
      const res = await leaveRequestApi.review(rejectingItem.id, {
        isApproved: false,
        reviewerNotes: rejectReason.trim(),
      });

      if (res.success) {
        toast.success('Đã từ chối đơn xin nghỉ và gửi lý do đến phụ huynh.');
        setRejectingItem(null);
        setRejectReason('');
        fetchRequests();
      } else {
        toast.error(res.message);
      }
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Có lỗi xảy ra.';
      toast.error(errorMsg);
    } finally {
      setProcessing(false);
    }
  };

  return (
    <div style={{ padding: '24px', maxWidth: '1200px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ marginBottom: '24px' }}>
        <h1 style={{ fontSize: '24px', fontWeight: 800, color: '#0F172A', margin: 0 }}>
          Tiếp nhận & Duyệt đơn xin nghỉ học
        </h1>
        <p style={{ color: '#64748B', marginTop: '4px', fontSize: '14px' }}>
          Duyệt đơn nghỉ của phụ huynh để hệ thống tự động sinh gợi ý "Vắng có phép" khi điểm danh và đồng bộ suất ăn trước 08h15.
        </p>
      </div>

      {/* Filter Tabs */}
      <div style={{ display: 'flex', gap: '8px', marginBottom: '20px', borderBottom: '2px solid #E2E8F0', paddingBottom: '8px' }}>
        {[
          { key: 'Submitted', label: 'Chờ duyệt' },
          { key: 'Approved', label: 'Đã duyệt' },
          { key: 'Rejected', label: 'Đã từ chối' },
          { key: 'All', label: 'Tất cả' },
        ].map((tab) => (
          <button
            key={tab.key}
            onClick={() => setStatusFilter(tab.key)}
            style={{
              padding: '8px 18px',
              borderRadius: '8px',
              border: 'none',
              backgroundColor: statusFilter === tab.key ? '#2563EB' : 'transparent',
              color: statusFilter === tab.key ? '#FFFFFF' : '#475569',
              fontWeight: 700,
              fontSize: '14px',
              cursor: 'pointer',
              transition: 'all 0.15s ease',
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* List */}
      {loading ? (
        <div style={{ textAlign: 'center', padding: '40px', color: '#64748B' }}>Đang tải danh sách đơn...</div>
      ) : requests.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '60px 20px', backgroundColor: '#F8FAFC', borderRadius: '16px' }}>
          <UserCheck size={48} color="#94A3B8" style={{ marginBottom: '12px' }} />
          <h3 style={{ margin: 0, color: '#334155' }}>Không có đơn xin nghỉ nào trong mục này</h3>
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
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
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
                  <strong style={{ fontSize: '17px', color: '#0F172A' }}>{r.studentName}</strong>
                  <span style={{ color: '#475569', fontSize: '13px' }}>Mã: {r.studentCode}</span>
                  {r.className && (
                    <span style={{ backgroundColor: '#EEF2FF', color: '#4338CA', padding: '2px 8px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
                      Lớp {r.className}
                    </span>
                  )}
                </div>

                <div>
                  {r.status === 1 && (
                    <span style={{ backgroundColor: '#FEF3C7', color: '#B45309', padding: '4px 12px', borderRadius: '20px', fontWeight: 700, fontSize: '12px' }}>
                      Chờ GVCN duyệt
                    </span>
                  )}
                  {r.status === 3 && (
                    <span style={{ backgroundColor: '#DCFCE7', color: '#15803D', padding: '4px 12px', borderRadius: '20px', fontWeight: 700, fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}>
                      <CheckCircle2 size={14} /> Đã phê duyệt
                    </span>
                  )}
                  {r.status === 4 && (
                    <span style={{ backgroundColor: '#FEE2E2', color: '#B91C1C', padding: '4px 12px', borderRadius: '20px', fontWeight: 700, fontSize: '12px', display: 'flex', alignItems: 'center', gap: '4px' }}>
                      <ShieldAlert size={14} /> Đã từ chối
                    </span>
                  )}
                </div>
              </div>

              {/* Information Grid */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '12px', fontSize: '14px', color: '#334155' }}>
                <div>
                  <span style={{ color: '#64748B' }}>Phụ huynh gửi:</span>{' '}
                  <strong>{r.requesterName}</strong> {r.requesterPhone && <small>({r.requesterPhone})</small>}
                </div>
                <div>
                  <span style={{ color: '#64748B' }}>Thời gian:</span>{' '}
                  <strong>
                    {new Date(r.startDate).toLocaleDateString('vi-VN')} {r.startDate !== r.endDate && `→ ${new Date(r.endDate).toLocaleDateString('vi-VN')}`}
                  </strong>
                </div>
                <div>
                  <span style={{ color: '#64748B' }}>Buổi:</span> <strong>{r.sessionName}</strong>
                </div>
                <div>
                  <span style={{ color: '#64748B' }}>Suất ăn trưa:</span>{' '}
                  {r.keepLunchService ? (
                    <strong style={{ color: '#0284C7' }}>Phụ huynh chọn GIỮ SUẤT ĂN</strong>
                  ) : (
                    <span>Tự động hủy nếu duyệt trước 08h15</span>
                  )}
                </div>
              </div>

              {/* Reason box */}
              <div style={{ backgroundColor: '#F8FAFC', padding: '12px 16px', borderRadius: '8px', fontSize: '14px' }}>
                <span style={{ color: '#64748B', fontWeight: 600 }}>Lý do phụ huynh trình bày: </span>
                <span style={{ color: '#0F172A' }}>{r.reason}</span>
                {r.reviewerNotes && (
                  <div style={{ marginTop: '8px', paddingTop: '8px', borderTop: '1px dashed #CBD5E1', color: r.status === 4 ? '#B91C1C' : '#0369A1' }}>
                    <strong>Ý kiến giáo viên: </strong>
                    <span>{r.reviewerNotes}</span>
                  </div>
                )}
              </div>

              {/* Action Buttons for Pending */}
              {r.status === 1 && (
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', paddingTop: '8px' }}>
                  <button
                    disabled={processing}
                    onClick={() => {
                      setRejectingItem(r);
                      setRejectReason('');
                    }}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '6px',
                      backgroundColor: '#FFFFFF',
                      color: '#DC2626',
                      border: '1px solid #FCA5A5',
                      padding: '8px 16px',
                      borderRadius: '8px',
                      fontWeight: 600,
                      cursor: 'pointer',
                    }}
                  >
                    <X size={16} /> Từ chối
                  </button>
                  <button
                    disabled={processing}
                    onClick={() => handleApprove(r)}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '6px',
                      backgroundColor: '#16A34A',
                      color: '#FFFFFF',
                      border: 'none',
                      padding: '8px 20px',
                      borderRadius: '8px',
                      fontWeight: 700,
                      cursor: 'pointer',
                      boxShadow: '0 2px 6px rgba(22, 163, 74, 0.25)',
                    }}
                  >
                    <Check size={16} /> Phê duyệt đơn
                  </button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {/* REJECT MODAL */}
      {rejectingItem && (
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
              borderRadius: '16px',
              maxWidth: '480px',
              width: '100%',
              padding: '24px',
              boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', color: '#DC2626', marginBottom: '12px' }}>
              <AlertTriangle size={24} />
              <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700 }}>Từ chối đơn xin nghỉ</h3>
            </div>
            <p style={{ fontSize: '14px', color: '#475569', marginBottom: '16px' }}>
              Học sinh: <strong>{rejectingItem.studentName}</strong>. Vui lòng nhập lý do từ chối để gửi thông báo cho phụ huynh:
            </p>

            <textarea
              rows={3}
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder="Ví dụ: Tuần này có bài kiểm tra khảo sát chất lượng, phụ huynh vui lòng cho con đi học..."
              style={{
                width: '100%',
                padding: '10px',
                borderRadius: '8px',
                border: '1px solid #CBD5E1',
                fontSize: '14px',
                marginBottom: '16px',
                outline: 'none',
              }}
              required
            />

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                type="button"
                onClick={() => setRejectingItem(null)}
                style={{ padding: '8px 16px', borderRadius: '8px', border: '1px solid #CBD5E1', background: '#F8FAFC', cursor: 'pointer', fontWeight: 600 }}
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                disabled={processing}
                onClick={handleConfirmReject}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                  padding: '8px 18px',
                  borderRadius: '8px',
                  border: 'none',
                  backgroundColor: '#DC2626',
                  color: '#FFFFFF',
                  fontWeight: 700,
                  cursor: 'pointer',
                }}
              >
                <MessageSquare size={16} /> Xác nhận từ chối
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
