import { useState, useEffect } from 'react';
import { AlertCircle, FileText, Send, Sparkles } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import type { AdministrativeRequestSummary } from '../../types/request.types';

interface StaffReviewDocumentModalProps {
  open: boolean;
  onClose: () => void;
  request: AdministrativeRequestSummary | null;
  onSuccess: () => void;
}

export function StaffReviewDocumentModal({
  open,
  onClose,
  request,
  onSuccess,
}: StaffReviewDocumentModalProps) {
  const [activeAction, setActiveAction] = useState<'endorse' | 'amendment'>('endorse');
  const [endorsement, setEndorsement] = useState('');
  const [draftContent, setDraftContent] = useState('');
  const [amendmentNotes, setAmendmentNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (request?.documentRequest) {
      setEndorsement(
        request.documentRequest.staffEndorsement ||
          'Hồ sơ học sinh đầy đủ, xác thực thông tin theo học chính khóa tại nhà trường.'
      );
      setAmendmentNotes(request.amendmentNotes || '');

      // Tự động sinh nội dung dự thảo ban đầu nếu chưa có
      if (request.documentRequest.documentContentDraft) {
        setDraftContent(request.documentRequest.documentContentDraft);
      } else {
        applyTemplate(request.documentRequest.documentType, request.studentName || 'Học sinh', request.studentCode || 'HS', request.documentRequest.purpose);
      }
    }
  }, [request]);

  const applyTemplate = (docType: string, studentName: string, studentCode: string, purpose: string) => {
    let content = '';
    if (docType.includes('đang theo học') || docType.includes('xác nhận')) {
      content = `CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nĐộc lập - Tự do - Hạnh phúc\n------------------------\nGIẤY XÁC NHẬN HỌC SINH\n\nHiệu trưởng Trường Tiểu học SAWP trân trọng xác nhận:\n- Em: ${studentName}\n- Mã học sinh: ${studentCode}\n- Hiện đang là học sinh lớp ${request?.currentClassName || 'chính khóa'} tại Trường Tiểu học SAWP năm học 2026 - 2027.\n- Hạnh kiểm: Đạt tiêu chuẩn rèn luyện Tốt.\n\nGiấy này được cấp theo đề nghị của phụ huynh nhằm mục đích: ${purpose}.`;
    } else if (docType.includes('Bảng điểm') || docType.includes('rèn luyện')) {
      content = `PHIẾU ĐÁNH GIÁ KẾT QUẢ RÈN LUYỆN & HỌC TẬP\n\nHọc sinh: ${studentName} - Mã số: ${studentCode}\nLớp: ${request?.currentClassName || '1A'}\n- Môn Toán: Hoàn thành Tốt\n- Môn Tiếng Việt: Hoàn thành Tốt\n- Năng lực phẩm chất: Tích cực, gương mẫu trong học tập.\n\nMục đích sử dụng: ${purpose}.`;
    } else {
      content = `GIẤY GIỚI THIỆU CHUYỂN TRƯỜNG\n\nBan Giám Hiệu Trường Tiểu học SAWP trân trọng giới thiệu:\nEm ${studentName} (Mã: ${studentCode}) đang học tại trường.\nLý do: ${purpose}.\nĐề nghị Quý trường tạo điều kiện tiếp nhận.`;
    }
    setDraftContent(content);
  };

  if (!request || request.requestType !== 'Document' || !request.documentRequest) {
    return null;
  }

  const doc = request.documentRequest;

  const handleEndorse = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!endorsement.trim()) {
      toast.error('Vui lòng nhập ý kiến xác thực của giáo vụ.');
      return;
    }
    if (!draftContent.trim()) {
      toast.error('Vui lòng soạn thảo nội dung dự thảo văn bản.');
      return;
    }

    try {
      setSubmitting(true);
      const res = await administrativeRequestApi.endorseDocument(request.id, {
        staffEndorsement: endorsement.trim(),
        documentContentDraft: draftContent.trim(),
      });

      if (res.success) {
        toast.success('Đã gửi dự thảo văn bản lên Ban Giám Hiệu phê duyệt!');
        onSuccess();
        onClose();
      } else {
        toast.error(res.message || 'Không thể gửi dự thảo');
      }
    } catch {
      toast.error('Đã xảy ra lỗi khi gửi dự thảo lên BGH.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleRequestAmendment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!amendmentNotes.trim()) {
      toast.error('Vui lòng nhập nội dung yêu cầu bổ sung.');
      return;
    }

    try {
      setSubmitting(true);
      const res = await administrativeRequestApi.requestAmendment(request.id, {
        amendmentNotes: amendmentNotes.trim(),
      });

      if (res.success) {
        toast.success('Đã gửi yêu cầu bổ sung thông tin tới phụ huynh!');
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
      title="Thẩm định Đơn Xin Cấp Giấy Tờ / Chứng Nhận"
      badgeText={request.requestNumber}
      badgeColor="pink"
      maxWidth="lg"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Thông tin học sinh & Yêu cầu cấp */}
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
            <div style={{ fontSize: 12, fontWeight: 800, color: '#64748B' }}>HỌC SINH ĐƯỢC ĐỀ NGHỊ:</div>
            <div style={{ fontSize: 16, fontWeight: 800, color: '#0F172A', marginTop: 2 }}>
              {request.studentName} ({request.studentCode})
            </div>
            <div style={{ fontSize: 13, color: '#475569', marginTop: 4 }}>
              Lớp: <strong>{request.currentClassName || 'Chính khóa'}</strong> | Phụ huynh: <strong>{request.requesterName}</strong>
            </div>
          </div>

          <div>
            <div style={{ fontSize: 12, fontWeight: 800, color: '#64748B' }}>LOẠI VĂN BẢN ĐỀ NGHỊ:</div>
            <div style={{ fontSize: 15, fontWeight: 800, color: '#9D174D', marginTop: 2 }}>
              {doc.documentType} ({doc.numberOfCopies} bản in)
            </div>
            <div style={{ fontSize: 13, color: '#334155', marginTop: 4 }}>
              <strong>Mục đích:</strong> {doc.purpose}
            </div>
            {request.attachmentUrl && (
              <div style={{ marginTop: 6 }}>
                <a
                  href={request.attachmentUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  style={{ fontSize: 12, color: '#2563EB', textDecoration: 'underline' }}
                >
                  📎 Xem tài liệu đính kèm của phụ huynh
                </a>
              </div>
            )}
          </div>
        </div>

        {/* Segmented Buttons chọn hành động */}
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
              backgroundColor: activeAction === 'endorse' ? '#FCE7F3' : '#F1F5F9',
              color: activeAction === 'endorse' ? '#9D174D' : '#64748B',
              boxShadow: activeAction === 'endorse' ? '2px 2px 0 #1E293B' : 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            <FileText size={16} /> Soạn dự thảo & Trình BGH duyệt
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
            <AlertCircle size={16} /> Yêu cầu phụ huynh bổ sung
          </button>
        </div>

        {/* Form Soạn dự thảo & Trình BGH */}
        {activeAction === 'endorse' && (
          <form onSubmit={handleEndorse} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                <label style={{ fontWeight: 700, fontSize: 13 }}>
                  Nội dung dự thảo văn bản chính thức: <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <div style={{ display: 'flex', gap: 6 }}>
                  <button
                    type="button"
                    onClick={() => applyTemplate(doc.documentType, request.studentName || 'Học sinh', request.studentCode || 'HS', doc.purpose)}
                    style={{
                      fontSize: 12,
                      padding: '3px 8px',
                      borderRadius: 6,
                      backgroundColor: '#F1F5F9',
                      border: '1px solid #CBD5E1',
                      cursor: 'pointer',
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: 4,
                      color: '#475569',
                    }}
                  >
                    <Sparkles size={13} /> Nạp lại mẫu chuẩn
                  </button>
                </div>
              </div>

              <textarea
                className="input"
                style={{
                  width: '100%',
                  minHeight: 140,
                  borderRadius: 10,
                  padding: '12px 14px',
                  fontFamily: 'monospace',
                  fontSize: 13,
                  lineHeight: 1.5,
                }}
                value={draftContent}
                onChange={(e) => setDraftContent(e.target.value)}
                required
              />
            </div>

            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Ý kiến xác nhận & thẩm định của Giáo vụ gửi BGH: <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 70, borderRadius: 10, padding: '10px 12px' }}
                value={endorsement}
                onChange={(e) => setEndorsement(e.target.value)}
                placeholder="Nhập nhận xét về hồ sơ học sinh và lý do xác nhận đề xuất cấp..."
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
                  backgroundColor: '#DB2777',
                  borderColor: '#1E293B',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 6,
                }}
              >
                <Send size={16} /> {submitting ? 'Đang gửi...' : 'Gửi dự thảo lên Ban Giám Hiệu'}
              </button>
            </div>
          </form>
        )}

        {/* Form Yêu cầu bổ sung */}
        {activeAction === 'amendment' && (
          <form onSubmit={handleRequestAmendment} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Nội dung yêu cầu phụ huynh bổ sung thông tin / tài liệu: <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 90, borderRadius: 10, padding: '10px 12px' }}
                value={amendmentNotes}
                onChange={(e) => setAmendmentNotes(e.target.value)}
                placeholder="Ví dụ: Vui lòng ghi rõ cơ quan tiếp nhận giấy xác nhận, hoặc nộp thêm bản chụp giấy khai sinh..."
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
