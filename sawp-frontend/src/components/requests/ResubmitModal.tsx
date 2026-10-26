import { useState, useEffect } from 'react';
import { AlertCircle, FileUp, Send, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import type { AdministrativeRequestSummary } from '../../types/request.types';

interface ResubmitModalProps {
  open: boolean;
  onClose: () => void;
  request: AdministrativeRequestSummary | null;
  onSuccess: () => void;
}

export function ResubmitModal({ open, onClose, request, onSuccess }: ResubmitModalProps) {
  const [reason, setReason] = useState('');
  const [purpose, setPurpose] = useState('');
  const [attachmentUrl, setAttachmentUrl] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (request) {
      setReason(request.classTransfer?.transferReason || '');
      setPurpose(request.documentRequest?.purpose || '');
      setAttachmentUrl(request.attachmentUrl || '');
    }
  }, [request]);

  if (!request) return null;

  const isClassTransfer = request.requestType === 'ClassTransfer';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isClassTransfer && !reason.trim()) {
      toast.error('Vui lòng nhập lý do xin chuyển lớp cập nhật.');
      return;
    }
    if (!isClassTransfer && !purpose.trim()) {
      toast.error('Vui lòng nhập mục đích xin cấp giấy tờ cập nhật.');
      return;
    }

    try {
      setLoading(true);
      const res = await administrativeRequestApi.resubmitRequest(request.id, {
        transferReason: isClassTransfer ? reason.trim() : undefined,
        purpose: !isClassTransfer ? purpose.trim() : undefined,
        attachmentUrl: attachmentUrl.trim() || undefined,
      });

      if (res.success) {
        toast.success('Đã nộp lại hồ sơ thành công! Giáo vụ sẽ kiểm tra lại.');
        onSuccess();
        onClose();
      } else {
        toast.error(res.message || 'Không thể nộp lại hồ sơ');
      }
    } catch {
      toast.error('Đã xảy ra lỗi khi gửi lại thông tin.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Bổ sung hồ sơ & Nộp lại"
      badgeText={request.requestNumber}
      badgeColor="amber"
      maxWidth="md"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {/* Yêu cầu từ Giáo vụ */}
        <div
          style={{
            backgroundColor: '#FFFBEB',
            border: '2px solid #F59E0B',
            borderRadius: 12,
            padding: '12px 16px',
            display: 'flex',
            alignItems: 'flex-start',
            gap: 12,
          }}
        >
          <AlertCircle size={22} color="#D97706" style={{ flexShrink: 0, marginTop: 2 }} />
          <div>
            <div style={{ fontWeight: 700, color: '#92400E', fontSize: 13, marginBottom: 4 }}>
              YÊU CẦU BỔ SUNG TỪ GIÁO VỤ:
            </div>
            <div style={{ color: '#78350F', fontSize: 14, lineHeight: 1.5 }}>
              {request.amendmentNotes || 'Vui lòng kiểm tra lại thông tin và bổ sung tài liệu minh chứng cần thiết.'}
            </div>
          </div>
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {isClassTransfer ? (
            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Lý do xin chuyển lớp (Cập nhật hoặc giải trình thêm): <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 90, borderRadius: 10, padding: '10px 12px' }}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="Nhập lý do chuyển lớp chi tiết theo hướng dẫn của giáo vụ..."
                required
              />
            </div>
          ) : (
            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Mục đích xin cấp giấy tờ (Cập nhật rõ ràng): <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 90, borderRadius: 10, padding: '10px 12px' }}
                value={purpose}
                onChange={(e) => setPurpose(e.target.value)}
                placeholder="Ghi rõ nộp cơ quan nào, lý do xin cấp..."
                required
              />
            </div>
          )}

          <div>
            <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
              Liên kết tệp đính kèm bổ sung (Ảnh/File Drive/Minh chứng):
            </label>
            <div style={{ position: 'relative' }}>
              <input
                type="text"
                className="input"
                style={{ width: '100%', borderRadius: 10, padding: '10px 12px 10px 38px' }}
                value={attachmentUrl}
                onChange={(e) => setAttachmentUrl(e.target.value)}
                placeholder="https://drive.google.com/file/... hoặc link ảnh tài liệu"
              />
              <FileUp size={18} color="#64748B" style={{ position: 'absolute', left: 12, top: 12 }} />
            </div>
            <span style={{ fontSize: 12, color: '#64748B', marginTop: 4, display: 'block' }}>
              💡 Phụ huynh có thể upload tài liệu lên Google Drive/Dropbox và dán link chia sẻ vào đây.
            </span>
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
            <button
              type="button"
              className="btn btn--outline"
              onClick={onClose}
              disabled={loading}
              style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
            >
              <X size={16} /> Đóng
            </button>
            <button
              type="submit"
              className="btn btn--primary"
              disabled={loading}
              style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
            >
              <Send size={16} /> {loading ? 'Đang gửi...' : 'Xác nhận & Nộp lại'}
            </button>
          </div>
        </form>
      </div>
    </GeometricModal>
  );
}
