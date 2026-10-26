import { useState, useEffect } from 'react';
import { ArrowRight, CheckCircle2, FileText, Send, Users, XCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import type { AdministrativeRequestSummary } from '../../types/request.types';

interface BoardDecisionModalProps {
  open: boolean;
  onClose: () => void;
  request: AdministrativeRequestSummary | null;
  onSuccess: () => void;
}

export function BoardDecisionModal({
  open,
  onClose,
  request,
  onSuccess,
}: BoardDecisionModalProps) {
  const [decision, setDecision] = useState<'approve' | 'reject'>('approve');
  const [notes, setNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (request) {
      setDecision('approve');
      setNotes('');
    }
  }, [request]);

  if (!request) return null;

  const isTransfer = request.requestType === 'ClassTransfer';
  const transfer = request.classTransfer;
  const doc = request.documentRequest;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (decision === 'reject' && !notes.trim()) {
      toast.error('Bắt buộc phải nhập lý do từ chối để thông báo tới phụ huynh.');
      return;
    }

    try {
      setSubmitting(true);
      const isApproved = decision === 'approve';
      const res = await administrativeRequestApi.processBoardDecision(request.id, {
        isApproved,
        decisionNotes: notes.trim() || undefined,
      });

      if (res.success) {
        toast.success(
          isApproved
            ? isTransfer
              ? 'Đã duyệt đơn chuyển lớp! Hồ sơ đã được chuyển tới Giáo vụ để thực thi phân lớp.'
              : 'Đã phê duyệt và phát hành văn bản có chữ ký số điện tử!'
            : 'Đã từ chối đơn yêu cầu và ghi vết nhật ký kiểm toán (Audit Log).'
        );
        onSuccess();
        onClose();
      } else {
        toast.error(res.message || 'Không thể xử lý quyết định');
      }
    } catch {
      toast.error('Đã xảy ra lỗi khi gửi quyết định.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title={isTransfer ? 'Ban Giám Hiệu Xét Duyệt Chuyển Lớp' : 'Ban Giám Hiệu Phê Duyệt Cấp Văn Bản'}
      badgeText={request.requestNumber}
      badgeColor={isTransfer ? 'mint' : 'violet'}
      maxWidth="lg"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Thông tin học sinh & Phụ huynh */}
        <div
          style={{
            backgroundColor: '#F8FAFC',
            border: '2px solid #1E293B',
            borderRadius: 14,
            padding: 16,
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
            gap: 16,
          }}
        >
          <div>
            <div style={{ fontSize: 12, fontWeight: 800, color: '#64748B' }}>HỌC SINH ĐỀ NGHỊ:</div>
            <div style={{ fontSize: 16, fontWeight: 800, color: '#0F172A', marginTop: 2 }}>
              {request.studentName} ({request.studentCode})
            </div>
            <div style={{ fontSize: 13, color: '#475569', marginTop: 4 }}>
              Lớp hiện tại: <strong>{request.currentClassName || 'Chính khóa'}</strong>
            </div>
            <div style={{ fontSize: 13, color: '#64748B', marginTop: 2 }}>
              Phụ huynh nộp: <strong>{request.requesterName}</strong>
            </div>
          </div>

          <div>
            <div style={{ fontSize: 12, fontWeight: 800, color: '#64748B' }}>
              {isTransfer ? 'LỘ TRÌNH CHUYỂN LỚP:' : 'LOẠI VĂN BẢN ĐỀ NGHỊ:'}
            </div>

            {isTransfer && transfer && (
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 4 }}>
                  <span style={{ padding: '3px 8px', backgroundColor: '#E2E8F0', borderRadius: 6, fontWeight: 700, fontSize: 13 }}>
                    Lớp cũ: {transfer.fromClassName}
                  </span>
                  <ArrowRight size={16} color="#64748B" />
                  <span style={{ padding: '3px 8px', backgroundColor: '#D1FAE5', color: '#065F46', borderRadius: 6, fontWeight: 800, fontSize: 13, border: '1px solid #059669' }}>
                    Lớp mới: {transfer.toClassName}
                  </span>
                </div>
                <div style={{ marginTop: 6, display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 12, fontWeight: 700, color: '#15803D' }}>
                  <Users size={14} /> Sĩ số hiện tại: {transfer.targetClassCapacity ?? 0}/{transfer.targetClassMaxCapacity ?? 35} học sinh
                </div>
              </div>
            )}

            {!isTransfer && doc && (
              <div>
                <div style={{ fontSize: 15, fontWeight: 800, color: '#6B21A8', marginTop: 4 }}>
                  {doc.documentType} ({doc.numberOfCopies} bản)
                </div>
                <div style={{ fontSize: 13, color: '#334155', marginTop: 2 }}>
                  Mục đích: {doc.purpose}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Ý kiến thẩm định của Giáo vụ */}
        <div
          style={{
            backgroundColor: '#EFF6FF',
            border: '2px solid #3B82F6',
            borderRadius: 12,
            padding: 14,
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 800, color: '#1D4ED8', marginBottom: 4 }}>
            Ý KIẾN THẨM ĐỊNH & ĐỀ XUẤT CỦA BỘ PHẬN GIÁO VỤ:
          </div>
          <div style={{ fontSize: 14, color: '#1E40AF', lineHeight: 1.5, fontStyle: 'italic' }}>
            "{isTransfer ? transfer?.staffRecommendation || 'Hồ sơ đầy đủ, đủ điều kiện chuyển lớp.' : doc?.staffEndorsement || 'Hồ sơ xác thực hợp lệ.'}"
          </div>
        </div>

        {/* Nếu là đơn giấy tờ: Hiển thị nội dung dự thảo */}
        {!isTransfer && doc?.documentContentDraft && (
          <div
            style={{
              backgroundColor: '#FFFFFF',
              border: '2px solid #E2E8F0',
              borderRadius: 12,
              padding: 14,
            }}
          >
            <div style={{ fontSize: 12, fontWeight: 800, color: '#475569', marginBottom: 6, display: 'flex', alignItems: 'center', gap: 6 }}>
              <FileText size={15} /> DỰ THẢO VĂN BẢN SẼ KÝ PHÁT HÀNH:
            </div>
            <pre
              style={{
                margin: 0,
                padding: 10,
                backgroundColor: '#F8FAFC',
                borderRadius: 8,
                fontSize: 12,
                fontFamily: 'monospace',
                whiteSpace: 'pre-wrap',
                lineHeight: 1.5,
                color: '#1E293B',
                maxHeight: 140,
                overflowY: 'auto',
              }}
            >
              {doc.documentContentDraft}
            </pre>
          </div>
        )}

        {/* Lựa chọn quyết định */}
        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div>
            <div style={{ fontSize: 13, fontWeight: 800, color: '#1E293B', marginBottom: 8 }}>
              QUYẾT ĐỊNH CỦA BAN GIÁM HIỆU: <span style={{ color: '#EF4444' }}>*</span>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
              <label
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 10,
                  padding: '12px 16px',
                  borderRadius: 12,
                  border: decision === 'approve' ? '2.5px solid #16A34A' : '2px solid #E2E8F0',
                  backgroundColor: decision === 'approve' ? '#F0FDF4' : '#FFFFFF',
                  boxShadow: decision === 'approve' ? '3px 3px 0 #16A34A' : 'none',
                  cursor: 'pointer',
                  fontWeight: 800,
                  color: decision === 'approve' ? '#15803D' : '#64748B',
                }}
              >
                <input
                  type="radio"
                  name="decision"
                  checked={decision === 'approve'}
                  onChange={() => setDecision('approve')}
                  style={{ width: 18, height: 18, accentColor: '#16A34A' }}
                />
                <CheckCircle2 size={20} color="#16A34A" />
                PHÊ DUYỆT ĐỀ XUẤT
              </label>

              <label
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 10,
                  padding: '12px 16px',
                  borderRadius: 12,
                  border: decision === 'reject' ? '2.5px solid #EF4444' : '2px solid #E2E8F0',
                  backgroundColor: decision === 'reject' ? '#FEF2F2' : '#FFFFFF',
                  boxShadow: decision === 'reject' ? '3px 3px 0 #EF4444' : 'none',
                  cursor: 'pointer',
                  fontWeight: 800,
                  color: decision === 'reject' ? '#B91C1C' : '#64748B',
                }}
              >
                <input
                  type="radio"
                  name="decision"
                  checked={decision === 'reject'}
                  onChange={() => setDecision('reject')}
                  style={{ width: 18, height: 18, accentColor: '#EF4444' }}
                />
                <XCircle size={20} color="#EF4444" />
                TỪ CHỐI ĐỀ XUẤT
              </label>
            </div>
          </div>

          <div>
            <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
              {decision === 'approve' ? 'Ý kiến chỉ đạo / Ghi chú phê duyệt (Tùy chọn):' : 'Lý do từ chối (Bắt buộc - sẽ ghi vào Audit Log & gửi phụ huynh):'}
              {decision === 'reject' && <span style={{ color: '#EF4444' }}> *</span>}
            </label>
            <textarea
              className="input"
              style={{
                width: '100%',
                minHeight: 80,
                borderRadius: 10,
                padding: '10px 12px',
                borderColor: decision === 'reject' ? '#EF4444' : undefined,
              }}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder={
                decision === 'approve'
                  ? 'Ví dụ: Đồng ý chuyển lớp, giao Giáo vụ thực hiện / Đồng ý phát hành văn bản...'
                  : 'Vui lòng nêu rõ lý do từ chối (ví dụ: sĩ số vượt quá quy định, điều kiện chưa phù hợp)...'
              }
              required={decision === 'reject'}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 6 }}>
            <button
              type="button"
              className="btn btn--outline"
              onClick={onClose}
              disabled={submitting}
            >
              Đóng
            </button>
            <button
              type="submit"
              className="btn btn--primary"
              disabled={submitting}
              style={{
                backgroundColor: decision === 'approve' ? '#16A34A' : '#DC2626',
                borderColor: '#1E293B',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 6,
              }}
            >
              <Send size={16} /> {submitting ? 'Đang lưu...' : decision === 'approve' ? 'Xác nhận Phê Duyệt' : 'Xác nhận Từ Chối'}
            </button>
          </div>
        </form>
      </div>
    </GeometricModal>
  );
}
