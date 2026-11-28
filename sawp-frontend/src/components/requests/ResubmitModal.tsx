import { useState, useEffect } from 'react';
import { AlertCircle, Send, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import { DirectFileUploader } from '../common/DirectFileUploader';
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
  const [numberOfCopies, setNumberOfCopies] = useState<number>(1);
  const [additionalNotes, setAdditionalNotes] = useState('');
  const [attachmentUrl, setAttachmentUrl] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (request) {
      setReason(request.classTransfer?.transferReason || '');
      setPurpose(request.documentRequest?.purpose || '');
      setNumberOfCopies(request.documentRequest?.numberOfCopies || 1);
      setAdditionalNotes('');
      setAttachmentUrl(request.attachmentUrl || '');
    }
  }, [request]);

  if (!request) return null;

  const isClassTransfer = request.requestType === 'ClassTransfer';
  const isOtherRequest = request.requestType === 'Other';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isClassTransfer && reason.trim().length < 5) {
      toast.error('Vui lòng nhập lý do xin chuyển lớp cập nhật (tối thiểu 5 ký tự).');
      return;
    }
    if (!isClassTransfer && purpose.trim().length < 5) {
      toast.error(
        isOtherRequest
          ? 'Vui lòng nhập nội dung đề xuất / kiến nghị cập nhật (tối thiểu 5 ký tự).'
          : 'Vui lòng nhập mục đích xin cấp giấy tờ cập nhật (tối thiểu 5 ký tự).'
      );
      return;
    }
    if (!isClassTransfer && !isOtherRequest && (numberOfCopies < 1 || numberOfCopies > 10)) {
      toast.error('Số lượng bản sao phải từ 1 đến 10 bản.');
      return;
    }

    try {
      setLoading(true);
      const res = await administrativeRequestApi.resubmitRequest(request.id, {
        transferReason: isClassTransfer ? reason.trim() : undefined,
        purpose: !isClassTransfer ? purpose.trim() : undefined,
        numberOfCopies: !isClassTransfer && !isOtherRequest ? numberOfCopies : undefined,
        additionalNotes: additionalNotes.trim() || undefined,
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
          ) : isOtherRequest ? (
            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Nội dung đề xuất / kiến nghị (Cập nhật rõ ràng): <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 90, borderRadius: 10, padding: '10px 12px' }}
                value={purpose}
                onChange={(e) => setPurpose(e.target.value)}
                placeholder="Cập nhật nội dung đơn đề xuất / kiến nghị theo hướng dẫn của giáo vụ..."
                required
              />
            </div>
          ) : (
            <>
              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                  Mục đích xin cấp giấy tờ (Cập nhật rõ ràng): <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <textarea
                  className="input"
                  style={{ width: '100%', minHeight: 80, borderRadius: 10, padding: '10px 12px' }}
                  value={purpose}
                  onChange={(e) => setPurpose(e.target.value)}
                  placeholder="Ghi rõ nộp cơ quan nào, lý do xin cấp..."
                  required
                />
              </div>

              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                  Số bản cần cấp (1 - 10 bản): <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <input
                  type="number"
                  min={1}
                  max={10}
                  className="input"
                  style={{ width: 140, borderRadius: 10, padding: '8px 12px' }}
                  value={numberOfCopies}
                  onChange={(e) => setNumberOfCopies(Number(e.target.value))}
                  required
                />
              </div>
            </>
          )}

          <div>
            <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
              Ý kiến / Ghi chú giải trình bổ sung (tùy chọn):
            </label>
            <input
              type="text"
              className="input"
              style={{ width: '100%', borderRadius: 10, padding: '8px 12px' }}
              value={additionalNotes}
              onChange={(e) => setAdditionalNotes(e.target.value)}
              placeholder="Gửi lời nhắn hoặc ghi chú thêm cho Giáo vụ..."
            />
          </div>

          <DirectFileUploader
            value={attachmentUrl}
            onChange={setAttachmentUrl}
            label="Tệp đính kèm bổ sung (PDF, JPG, PNG):"
            hint="Chấp nhận tệp PDF, JPG, PNG tối đa 10MB."
          />

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
