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

      if (request.requiresAmendment) {
        setActiveAction('amendment');
      } else {
        setActiveAction('endorse');
      }

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
    const lowerType = (docType || '').toLowerCase();

    if (lowerType.includes('chuyển trường')) {
      content = `Ban Giám Hiệu Trường Tiểu học SAWP đồng ý cho em ${studentName} (Mã định danh: ${studentCode}), hiện là học sinh lớp ${request?.currentClassName || 'chính khóa'} năm học 2026 - 2027, được giải quyết thủ tục chuyển trường theo nguyện vọng của gia đình.\n\nLý do chuyển trường: ${purpose || 'Chuyển nơi cư trú / nguyện vọng của gia đình'}.\n\nNhà trường xác nhận: Học sinh hoàn thành tốt nhiệm vụ học tập và rèn luyện, hồ sơ học bạ đầy đủ, hợp lệ theo quy định. Kính đề nghị Ban Giám Hiệu trường chuyển đến tạo điều kiện tiếp nhận học sinh theo quy định của Bộ Giáo dục và Đào tạo.`;
    } else if (lowerType.includes('bảng điểm') || lowerType.includes('rèn luyện')) {
      content = `Chứng nhận kết quả học tập và rèn luyện của học sinh ${studentName} (Mã định danh: ${studentCode}) - Lớp ${request?.currentClassName || 'chính khóa'}:\n- Đánh giá định kỳ các môn học: Môn Tiếng Việt (Hoàn thành Tốt), Môn Toán (Hoàn thành Tốt), Môn Ngoại ngữ (Hoàn thành Tốt), các môn học và hoạt động giáo dục khác (Hoàn thành).\n- Đánh giá sự hình thành và phát triển năng lực, phẩm chất: Tự phục vụ, tự quản tốt; Hợp tác tốt; Chăm chỉ, trung thực, có tinh thần trách nhiệm.\n- Đánh giá chung: Hoàn thành xuất sắc nhiệm vụ năm học 2026 - 2027.\n\nMục đích cấp giấy: ${purpose || 'Bổ sung hồ sơ học sinh'}.`;
    } else if (lowerType.includes('hoàn thành chương trình')) {
      content = `Xác nhận học sinh ${studentName} (Mã định danh: ${studentCode}), học sinh lớp ${request?.currentClassName || 'chính khóa'} tại Trường Tiểu học SAWP, đã hoàn thành đầy đủ chương trình các môn học và hoạt động giáo dục theo quy định của Bộ Giáo dục và Đào tạo trong năm học 2026 - 2027, đủ điều kiện xét công nhận hoàn thành chương trình lớp học / cấp học.\n\nMục đích cấp: ${purpose || 'Bổ sung hồ sơ học tập'}.`;
    } else {
      content = `Xác nhận em ${studentName} (Mã định danh: ${studentCode}) hiện đang là học sinh chính khóa lớp ${request?.currentClassName || 'chính khóa'} tại Trường Tiểu học SAWP trong năm học 2026 - 2027.\n\nTrong thời gian theo học tại trường, học sinh luôn chấp hành tốt nội quy nhà trường, có phẩm chất đạo đức tốt và tham gia đầy đủ các hoạt động giáo dục toàn diện.\n\nGiấy này được cấp theo đề nghị của phụ huynh nhằm mục đích: ${purpose || 'Bổ sung hồ sơ theo quy định'}.`;
    }
    setDraftContent(content);
  };

  if (!request || request.requestType !== 'Document' || !request.documentRequest) {
    return null;
  }

  const isAwaitingParent = !!request.requiresAmendment;
  const isAlreadyUnderReview = request.status === 'UnderReview';
  const isClosed = request.status === 'Approved' || request.status === 'Rejected';

  const doc = request.documentRequest;

  const handleEndorse = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isAwaitingParent) {
      toast.error('Hồ sơ đang chờ phụ huynh bổ sung. Chưa thể trình Ban Giám Hiệu.');
      return;
    }
    if (isAlreadyUnderReview) {
      toast.error('Hồ sơ này đã được trình Ban Giám Hiệu xem xét.');
      return;
    }
    if (isClosed) {
      toast.error('Hồ sơ này đã hoàn tất xử lý.');
      return;
    }
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

        {/* Cảnh báo trạng thái bổ sung hồ sơ */}
        {isAwaitingParent && (
          <div
            style={{
              backgroundColor: '#FFFBEB',
              border: '2px solid #F59E0B',
              borderRadius: 12,
              padding: '12px 16px',
              display: 'flex',
              alignItems: 'center',
              gap: 12,
            }}
          >
            <AlertCircle size={22} color="#D97706" style={{ flexShrink: 0 }} />
            <div>
              <div style={{ fontWeight: 800, color: '#B45309', fontSize: 13 }}>
                HỒ SƠ ĐANG CHỜ PHỤ HUYNH BỔ SUNG
              </div>
              <div style={{ fontSize: 13, color: '#92400E', marginTop: 2 }}>
                Giáo vụ đã gửi yêu cầu bổ sung thông tin. Phụ huynh chưa nộp lại nên bạn <strong>không thể gửi lên Ban Giám Hiệu</strong> lúc này để tránh ghi đè quy trình.
              </div>
            </div>
          </div>
        )}

        {/* Segmented Buttons chọn hành động */}
        <div style={{ display: 'flex', gap: 10, borderBottom: '2px solid #E2E8F0', paddingBottom: 12 }}>
          <button
            type="button"
            disabled={isAwaitingParent || isAlreadyUnderReview || isClosed}
            onClick={() => setActiveAction('endorse')}
            title={isAwaitingParent ? 'Hồ sơ đang chờ phụ huynh bổ sung, chưa thể trình BGH' : undefined}
            style={{
              padding: '8px 16px',
              borderRadius: 10,
              fontWeight: 700,
              fontSize: 13,
              cursor: isAwaitingParent || isAlreadyUnderReview || isClosed ? 'not-allowed' : 'pointer',
              opacity: isAwaitingParent || isAlreadyUnderReview || isClosed ? 0.5 : 1,
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
            disabled={isAlreadyUnderReview || isClosed}
            onClick={() => setActiveAction('amendment')}
            style={{
              padding: '8px 16px',
              borderRadius: 10,
              fontWeight: 700,
              fontSize: 13,
              cursor: isAlreadyUnderReview || isClosed ? 'not-allowed' : 'pointer',
              opacity: isAlreadyUnderReview || isClosed ? 0.5 : 1,
              border: activeAction === 'amendment' ? '2px solid #1E293B' : '2px solid transparent',
              backgroundColor: activeAction === 'amendment' ? '#FEF3C7' : '#F1F5F9',
              color: activeAction === 'amendment' ? '#92400E' : '#64748B',
              boxShadow: activeAction === 'amendment' ? '2px 2px 0 #1E293B' : 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            <AlertCircle size={16} /> {isAwaitingParent ? 'Xem / Cập nhật yêu cầu bổ sung' : 'Yêu cầu phụ huynh bổ sung'}
          </button>
        </div>

        {/* Form Soạn dự thảo & Trình BGH */}
        {activeAction === 'endorse' && (
          <form onSubmit={handleEndorse} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                <div>
                  <label style={{ fontWeight: 700, fontSize: 13, display: 'block' }}>
                    Nội dung dự thảo văn bản chính thức: <span style={{ color: '#EF4444' }}>*</span>
                  </label>
                  <span style={{ fontSize: 11, color: '#64748B' }}>
                    (Ý kiến xác nhận & phê duyệt của Ban Giám Hiệu được in vào văn bản chính thức)
                  </span>
                </div>
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
