import { useState, useEffect } from 'react';
import { AlertCircle, ArrowRight, CheckCircle2, Send, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import type { AdministrativeRequestSummary } from '../../types/request.types';

interface StaffReviewTransferModalProps {
  open: boolean;
  onClose: () => void;
  request: AdministrativeRequestSummary | null;
  onSuccess: () => void;
}

export function StaffReviewTransferModal({
  open,
  onClose,
  request,
  onSuccess,
}: StaffReviewTransferModalProps) {
  const [activeAction, setActiveAction] = useState<'endorse' | 'amendment'>('endorse');
  const [recommendation, setRecommendation] = useState('');
  const [amendmentNotes, setAmendmentNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (request?.classTransfer) {
      setRecommendation(
        request.classTransfer.staffRecommendation ||
          `Đã rà soát học bạ và sĩ số lớp ${request.classTransfer.toClassName}. Đề xuất Ban Giám Hiệu xem xét phê duyệt điều chuyển lớp cho học sinh.`
      );
      setAmendmentNotes(request.amendmentNotes || '');
    }
  }, [request]);

  if (!request || request.requestType !== 'ClassTransfer' || !request.classTransfer) {
    return null;
  }

  const transfer = request.classTransfer;
  const currentCap = transfer.targetClassCapacity ?? 0;
  const maxCap = transfer.targetClassMaxCapacity ?? 35;
  const isFull = currentCap >= maxCap;

  const handleEndorse = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!recommendation.trim()) {
      toast.error('Vui lòng nhập ý kiến đề xuất chuyên môn của giáo vụ.');
      return;
    }

    try {
      setSubmitting(true);
      const res = await administrativeRequestApi.endorseTransfer(request.id, {
        staffRecommendation: recommendation.trim(),
      });

      if (res.success) {
        toast.success('Đã gửi đề xuất chuyển lớp lên Ban Giám Hiệu phê duyệt!');
        onSuccess();
        onClose();
      } else {
        toast.error(res.message || 'Không thể gửi đề xuất');
      }
    } catch {
      toast.error('Đã xảy ra lỗi khi gửi đề xuất lên BGH.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleRequestAmendment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!amendmentNotes.trim()) {
      toast.error('Vui lòng nhập nội dung yêu cầu phụ huynh bổ sung.');
      return;
    }

    try {
      setSubmitting(true);
      const res = await administrativeRequestApi.requestAmendment(request.id, {
        amendmentNotes: amendmentNotes.trim(),
      });

      if (res.success) {
        toast.success('Đã gửi yêu cầu bổ sung hồ sơ tới phụ huynh!');
        onSuccess();
        onClose();
      } else {
        toast.error(res.message || 'Không thể gửi yêu cầu');
      }
    } catch {
      toast.error('Đã xảy ra lỗi khi gửi yêu cầu bổ sung.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Thẩm định Đơn Xin Chuyển Lớp"
      badgeText={request.requestNumber}
      badgeColor="mint"
      maxWidth="lg"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Thông tin học sinh & Lộ trình chuyển */}
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
              Phụ huynh nộp: <strong>{request.requesterName}</strong>
            </div>
            <div style={{ fontSize: 13, color: '#64748B', marginTop: 2 }}>
              Ngày nộp: {new Date(request.submissionDate).toLocaleDateString('vi-VN')}
            </div>
          </div>

          <div>
            <div style={{ fontSize: 12, fontWeight: 800, color: '#64748B' }}>LỘ TRÌNH ĐIỀU CHUYỂN:</div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 4 }}>
              <span
                style={{
                  padding: '4px 10px',
                  backgroundColor: '#E2E8F0',
                  color: '#1E293B',
                  borderRadius: 8,
                  fontWeight: 700,
                  fontSize: 14,
                }}
              >
                Lớp cũ: {transfer.fromClassName}
              </span>
              <ArrowRight size={18} color="#64748B" />
              <span
                style={{
                  padding: '4px 10px',
                  backgroundColor: '#D1FAE5',
                  color: '#065F46',
                  borderRadius: 8,
                  fontWeight: 800,
                  fontSize: 14,
                  border: '1.5px solid #059669',
                }}
              >
                Lớp mới: {transfer.toClassName}
              </span>
            </div>

            {/* Widget kiểm tra sức chứa lớp đích */}
            <div
              style={{
                marginTop: 10,
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                padding: '4px 10px',
                borderRadius: 8,
                backgroundColor: isFull ? '#FEE2E2' : '#DCFCE7',
                border: isFull ? '1.5px solid #EF4444' : '1.5px solid #22C55E',
                fontSize: 12,
                fontWeight: 700,
                color: isFull ? '#991B1B' : '#15803D',
              }}
            >
              <Users size={14} />
              Sĩ số lớp đích: {currentCap}/{maxCap} học sinh {isFull ? '(ĐÃ ĐỦ SĨ SỐ)' : `(Còn ${maxCap - currentCap} chỗ trống)`}
            </div>
          </div>
        </div>

        {/* Lý do phụ huynh & Tệp đính kèm */}
        <div
          style={{
            backgroundColor: '#FFFFFF',
            border: '2px solid #E2E8F0',
            borderRadius: 12,
            padding: 14,
          }}
        >
          <div style={{ fontSize: 13, fontWeight: 700, color: '#334155', marginBottom: 4 }}>
            Lý do phụ huynh trình bày:
          </div>
          <div style={{ fontSize: 14, color: '#1E293B', lineHeight: 1.5 }}>
            "{transfer.transferReason}"
          </div>
          {request.attachmentUrl && (
            <div style={{ marginTop: 8 }}>
              <a
                href={request.attachmentUrl}
                target="_blank"
                rel="noopener noreferrer"
                style={{ fontSize: 13, color: '#2563EB', fontWeight: 600, textDecoration: 'underline' }}
              >
                📎 Mở tệp tài liệu / minh chứng đính kèm của phụ huynh
              </a>
            </div>
          )}
        </div>

        {/* Segmented Buttons chọn hành động: Đề xuất BGH vs Yêu cầu bổ sung */}
        <div style={{ display: 'flex', gap: 10, borderBottom: '2px solid #E2E8F0', paddingBottom: 12 }}>
          <button
            type="button"
            onClick={() => setActiveAction('endorse')}
            style={{
              padding: '8px 16px',
              borderRadius: 10,
              fontWeight: 700,
              fontSize: 13,
              cursor: 'pointer',
              border: activeAction === 'endorse' ? '2px solid #1E293B' : '2px solid transparent',
              backgroundColor: activeAction === 'endorse' ? '#DCFCE7' : '#F1F5F9',
              color: activeAction === 'endorse' ? '#14532D' : '#64748B',
              boxShadow: activeAction === 'endorse' ? '2px 2px 0 #1E293B' : 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            <CheckCircle2 size={16} /> Hồ sơ hợp lệ & Trình BGH duyệt
          </button>

          <button
            type="button"
            onClick={() => setActiveAction('amendment')}
            style={{
              padding: '8px 16px',
              borderRadius: 10,
              fontWeight: 700,
              fontSize: 13,
              cursor: 'pointer',
              border: activeAction === 'amendment' ? '2px solid #1E293B' : '2px solid transparent',
              backgroundColor: activeAction === 'amendment' ? '#FEF3C7' : '#F1F5F9',
              color: activeAction === 'amendment' ? '#92400E' : '#64748B',
              boxShadow: activeAction === 'amendment' ? '2px 2px 0 #1E293B' : 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            <AlertCircle size={16} /> Hồ sơ chưa đủ / Yêu cầu bổ sung
          </button>
        </div>

        {/* Form hành động 1: Trình BGH */}
        {activeAction === 'endorse' && (
          <form onSubmit={handleEndorse} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Ý kiến thẩm định & Đề xuất chuyên môn gửi Ban Giám Hiệu: <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 90, borderRadius: 10, padding: '10px 12px' }}
                value={recommendation}
                onChange={(e) => setRecommendation(e.target.value)}
                placeholder="Nhập nhận xét về tình hình học tập, sĩ số lớp đích và đề xuất..."
                required
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
                disabled={submitting || isFull}
                style={{
                  backgroundColor: isFull ? '#94A3B8' : '#059669',
                  borderColor: '#1E293B',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 6,
                }}
              >
                <Send size={16} /> {submitting ? 'Đang gửi...' : 'Gửi đề xuất lên Ban Giám Hiệu'}
              </button>
            </div>
          </form>
        )}

        {/* Form hành động 2: Yêu cầu bổ sung */}
        {activeAction === 'amendment' && (
          <form onSubmit={handleRequestAmendment} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Nội dung yêu cầu phụ huynh bổ sung / giải trình thêm: <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 90, borderRadius: 10, padding: '10px 12px' }}
                value={amendmentNotes}
                onChange={(e) => setAmendmentNotes(e.target.value)}
                placeholder="Ví dụ: Cần bổ sung giấy xác nhận thay đổi hộ khẩu/nơi ở mới, hoặc giải trình rõ hơn về điều kiện sức khỏe..."
                required
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
                  backgroundColor: '#D97706',
                  borderColor: '#1E293B',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 6,
                }}
              >
                <Send size={16} /> {submitting ? 'Đang gửi...' : 'Gửi thông báo yêu cầu bổ sung'}
              </button>
            </div>
          </form>
        )}
      </div>
    </GeometricModal>
  );
}
