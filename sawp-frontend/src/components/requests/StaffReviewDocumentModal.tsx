import { useState, useEffect } from 'react';
import { AlertCircle, CheckCircle2, FileText, Lock, Send, XCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { AttachmentViewer } from '../common/AttachmentViewer';
import { DirectFileUploader } from '../common/DirectFileUploader';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import { todayIso as getTodayIso, toLocalDateIso } from '../../utils/date';
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
  const [activeAction, setActiveAction] = useState<'endorse' | 'direct_issue' | 'amendment' | 'reject'>('endorse');
  const [endorsement, setEndorsement] = useState('');
  const [documentFileUrl, setDocumentFileUrl] = useState('');
  const [amendmentNotes, setAmendmentNotes] = useState('');
  const [certificateNumber, setCertificateNumber] = useState('');
  const todayIso = getTodayIso();
  const [issueDate, setIssueDate] = useState(todayIso);
  const [isPaperCopy, setIsPaperCopy] = useState(false);
  const [staffNotes, setStaffNotes] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (request?.documentRequest) {
      setEndorsement(
        request.documentRequest.staffEndorsement ||
        'Hồ sơ học sinh đầy đủ, đã xác thực thông tin theo học tại nhà trường.'
      );
      setDocumentFileUrl(
        request.documentRequest.issuedDocumentUrl && !request.documentRequest.issuedDocumentUrl.includes('/download-document')
          ? request.documentRequest.issuedDocumentUrl
          : ''
      );
      setAmendmentNotes(request.amendmentNotes || '');
      setCertificateNumber(`SAWP-GXN-${new Date().getFullYear()}-${request.id.toString().padStart(4, '0')}`);
      setIsPaperCopy(request.documentRequest.isPaperCopyRequested || false);
      setIssueDate(todayIso);
      setStaffNotes('');
      setRejectReason('');

      if (request.requiresAmendment) {
        setActiveAction('amendment');
      } else {
        setActiveAction('endorse');
      }
    }
  }, [request, todayIso]);

  if (!request || (request.requestType !== 'Document' && request.requestType !== 'Other') || !request.documentRequest) {
    return null;
  }

  const isOther = request.requestType === 'Other';
  const isAwaitingParent = !!request.requiresAmendment;
  const isReturnedByBoard = request.status === 'ReturnedToStaff' || request.status === 'NeedMoreInfo';
  const isAlreadyUnderReview = request.status === 'UnderReview';
  const isClosed = request.status === 'Approved' || request.status === 'Rejected' || request.status === 'Completed';

  const sensitiveTypesRequiringBoard = [
    'chuyển trường',
    'phúc khảo',
    'miễn, giảm',
    'miễn giảm',
    'hỗ trợ chi phí',
  ];
  const isBoardRequired =
    !isOther &&
    sensitiveTypesRequiringBoard.some((t) =>
      request.documentRequest?.documentType?.toLowerCase().includes(t.toLowerCase())
    );

  const doc = request.documentRequest;
  const submissionDateIso = toLocalDateIso(request.submissionDate) || undefined;

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
    if (!endorsement.trim() || endorsement.trim().length < 5) {
      toast.error('Vui lòng nhập ghi chú / ý kiến xác thực của giáo vụ (tối thiểu 5 ký tự).');
      return;
    }
    if (!isOther && !documentFileUrl.trim()) {
      toast.error('Vui lòng tải lên tệp văn bản (PDF hoặc Ảnh) trước khi trình Ban Giám Hiệu xem xét.');
      return;
    }

    try {
      setSubmitting(true);
      const res = await administrativeRequestApi.endorseDocument(request.id, {
        staffEndorsement: endorsement.trim(),
        documentFileUrl: documentFileUrl.trim() || undefined,
      });

      if (res.success) {
        toast.success('Đã gửi hồ sơ và tệp đính kèm lên Ban Giám Hiệu phê duyệt!');
        onSuccess();
        onClose();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể gửi hồ sơ lên BGH.');
      }
    } catch (err: any) {
      const errorMsg =
        err?.response?.data?.errors?.[0] ||
        err?.response?.data?.message ||
        err?.message ||
        'Đã xảy ra lỗi khi gửi hồ sơ lên BGH.';
      toast.error(errorMsg);
    } finally {
      setSubmitting(false);
    }
  };

  const handleRequestAmendment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!amendmentNotes.trim() || amendmentNotes.trim().length < 5) {
      toast.error('Vui lòng nhập nội dung yêu cầu bổ sung (tối thiểu 5 ký tự).');
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
        toast.error(res.errors?.[0] || res.message || 'Không thể gửi yêu cầu');
      }
    } catch (err: any) {
      const errorMsg =
        err?.response?.data?.errors?.[0] ||
        err?.response?.data?.message ||
        err?.message ||
        'Đã xảy ra lỗi khi gửi yêu cầu bổ sung.';
      toast.error(errorMsg);
    } finally {
      setSubmitting(false);
    }
  };

  const handleDirectIssue = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isClosed) {
      toast.error('Hồ sơ này đã hoàn tất xử lý.');
      return;
    }
    if (isBoardRequired) {
      toast.error('Loại văn bản này thuộc thẩm quyền của Ban Giám Hiệu. Giáo vụ không thể phát hành trực tiếp.');
      return;
    }
    if (!issueDate) {
      toast.error('Vui lòng chọn ngày cấp / phản hồi.');
      return;
    }
    if (issueDate > todayIso) {
      toast.error('Ngày cấp văn bản không được là ngày trong tương lai.');
      return;
    }
    if (submissionDateIso && issueDate < submissionDateIso) {
      toast.error('Ngày cấp văn bản không thể trước ngày phụ huynh nộp đơn.');
      return;
    }
    if (!isOther && !documentFileUrl.trim()) {
      toast.error('Vui lòng tải lên tệp văn bản đã xác nhận/đóng dấu (PDF hoặc Ảnh) để phát hành trực tiếp.');
      return;
    }
    if (isOther && !documentFileUrl.trim() && (!staffNotes.trim() || staffNotes.trim().length < 5)) {
      toast.error('Vui lòng nhập ghi chú phản hồi (tối thiểu 5 ký tự) hoặc đính kèm tệp kết quả.');
      return;
    }

    try {
      setSubmitting(true);
      const res = await administrativeRequestApi.directIssue(request.id, {
        certificateNumber: certificateNumber.trim() || undefined,
        issuedDate: issueDate,
        documentFileUrl: documentFileUrl.trim() || undefined,
        isPaperCopy,
        staffNotes: staffNotes.trim() || undefined,
      });

      if (res.success) {
        toast.success(res.message || (isOther ? 'Đã phản hồi & hoàn tất đơn thành công!' : 'Đã phát hành văn bản trực tiếp thành công!'));
        onSuccess();
        onClose();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể phát hành văn bản');
      }
    } catch (err: any) {
      const errorMsg =
        err?.response?.data?.errors?.[0] ||
        err?.response?.data?.message ||
        err?.message ||
        'Đã xảy ra lỗi khi phát hành văn bản.';
      toast.error(errorMsg);
    } finally {
      setSubmitting(false);
    }
  };

  const handleReject = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isClosed) {
      toast.error('Hồ sơ này đã hoàn tất xử lý.');
      return;
    }
    if (!rejectReason.trim() || rejectReason.trim().length < 5) {
      toast.error('Vui lòng nhập lý do từ chối (tối thiểu 5 ký tự).');
      return;
    }

    try {
      setSubmitting(true);
      const res = await administrativeRequestApi.staffReject(request.id, rejectReason.trim());

      if (res.success) {
        toast.success(res.message || 'Đã từ chối đơn thành công!');
        onSuccess();
        onClose();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể từ chối đơn');
      }
    } catch (err: any) {
      const errorMsg =
        err?.response?.data?.errors?.[0] ||
        err?.response?.data?.message ||
        err?.message ||
        'Đã xảy ra lỗi khi từ chối tiếp nhận đơn.';
      toast.error(errorMsg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title={isOther ? 'Xử lý Loại Đơn Khác / Kiến Nghị' : 'Thẩm định Đơn Xin Cấp Giấy Tờ / Chứng Nhận'}
      badgeText={request.requestNumber}
      badgeColor={isOther ? 'violet' : 'pink'}
      maxWidth="lg"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Thông tin học sinh & Yêu cầu */}
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
              Lớp: <strong>{request.currentClassName || 'Chưa xếp lớp'}</strong> | Phụ huynh: <strong>{request.requesterName}</strong>
            </div>
          </div>

          <div>
            <div style={{ fontSize: 12, fontWeight: 800, color: '#64748B' }}>
              {isOther ? 'TIÊU ĐỀ ĐƠN KHÁC:' : 'LOẠI VĂN BẢN ĐỀ NGHỊ:'}
            </div>
            <div style={{ fontSize: 15, fontWeight: 800, color: isOther ? '#4F46E5' : '#9D174D', marginTop: 2 }}>
              {doc.documentType} {!isOther && `(${doc.numberOfCopies} bản)`}
            </div>
            <div style={{ fontSize: 13, color: '#334155', marginTop: 4 }}>
              <strong>{isOther ? 'Nội dung:' : 'Mục đích:'}</strong> {doc.purpose}
            </div>
            <AttachmentViewer url={request.attachmentUrl} compact />
          </div>
        </div>

        {/* Cảnh báo BGH yêu cầu Giáo vụ chỉnh sửa / trình lại */}
        {isReturnedByBoard && request.schoolBoardNotes && (
          <div
            style={{
              backgroundColor: '#FFF7ED',
              border: '2px solid #EA580C',
              borderRadius: 12,
              padding: '12px 16px',
              display: 'flex',
              alignItems: 'flex-start',
              gap: 12,
            }}
          >
            <AlertCircle size={22} color="#EA580C" style={{ flexShrink: 0, marginTop: 2 }} />
            <div>
              <div style={{ fontWeight: 800, color: '#9A3412', fontSize: 13 }}>
                BAN GIÁM HIỆU YÊU CẦU GIÁO VỤ CHỈNH SỬA & TRÌNH LẠI
              </div>
              <div style={{ fontSize: 13, color: '#7C2D12', marginTop: 4 }}>
                &quot;{request.schoolBoardNotes}&quot;
              </div>
            </div>
          </div>
        )}

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
                Giáo vụ đã gửi yêu cầu bổ sung thông tin. Phụ huynh chưa nộp lại nên bạn <strong>không thể gửi lên Ban Giám Hiệu</strong> lúc này.
              </div>
            </div>
          </div>
        )}

        {/* Segmented Buttons chọn hành động */}
        <div style={{ display: 'flex', gap: 10, borderBottom: '2px solid #E2E8F0', paddingBottom: 12, flexWrap: 'wrap' }}>
          <button
            type="button"
            disabled={isAwaitingParent || isAlreadyUnderReview || isClosed}
            onClick={() => setActiveAction('endorse')}
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
            <FileText size={16} /> {isReturnedByBoard ? 'Gửi lại Ban Giám Hiệu' : 'Tải tệp & Trình BGH'}
          </button>

          <button
            type="button"
            disabled={isAwaitingParent || isAlreadyUnderReview || isClosed || isBoardRequired}
            onClick={() => {
              if (isBoardRequired) {
                toast.error('Loại văn bản này thuộc thẩm quyền của Ban Giám Hiệu. Giáo vụ vui lòng trình BGH ký duyệt.');
                return;
              }
              setActiveAction('direct_issue');
            }}
            style={{
              padding: '8px 16px',
              borderRadius: 10,
              fontWeight: 700,
              fontSize: 13,
              cursor: isAwaitingParent || isAlreadyUnderReview || isClosed || isBoardRequired ? 'not-allowed' : 'pointer',
              opacity: isAwaitingParent || isAlreadyUnderReview || isClosed || isBoardRequired ? 0.5 : 1,
              border: activeAction === 'direct_issue' ? '2px solid #1E293B' : '2px solid transparent',
              backgroundColor: activeAction === 'direct_issue' ? '#DCFCE7' : '#F1F5F9',
              color: activeAction === 'direct_issue' ? '#15803D' : '#64748B',
              boxShadow: activeAction === 'direct_issue' ? '2px 2px 0 #1E293B' : 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            {isBoardRequired ? <Lock size={15} /> : <CheckCircle2 size={16} />}
            {isBoardRequired
              ? 'BGH ký duyệt (Không cấp trực tiếp)'
              : isOther
                ? 'Phản hồi & Hoàn tất trực tiếp'
                : 'Phát hành trực tiếp'}
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

          <button
            type="button"
            disabled={isAlreadyUnderReview || isClosed}
            onClick={() => setActiveAction('reject')}
            style={{
              padding: '8px 16px',
              borderRadius: 10,
              fontWeight: 700,
              fontSize: 13,
              cursor: isAlreadyUnderReview || isClosed ? 'not-allowed' : 'pointer',
              opacity: isAlreadyUnderReview || isClosed ? 0.5 : 1,
              border: activeAction === 'reject' ? '2px solid #1E293B' : '2px solid transparent',
              backgroundColor: activeAction === 'reject' ? '#FEE2E2' : '#F1F5F9',
              color: activeAction === 'reject' ? '#991B1B' : '#64748B',
              boxShadow: activeAction === 'reject' ? '2px 2px 0 #1E293B' : 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            <XCircle size={16} /> Từ chối tiếp nhận
          </button>
        </div>

        {/* Form Trình Ban Giám Hiệu (Tải tệp PDF/Ảnh + Ghi chú) */}
        {activeAction === 'endorse' && (
          <form onSubmit={handleEndorse} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div
              style={{
                backgroundColor: '#EFF6FF',
                border: '1.5px solid #93C5FD',
                borderRadius: 10,
                padding: '10px 14px',
                fontSize: 13,
                color: '#1E40AF',
              }}
            >
              📎 <strong>Quy trình trình ký BGH:</strong> Giáo vụ tải lên tệp văn bản (PDF hoặc Ảnh) cần Ban Giám Hiệu xem xét. Ban Giám Hiệu sẽ tải về, gắn chữ ký số và tải lên bản chính thức khi phê duyệt.
            </div>

            <DirectFileUploader
              value={documentFileUrl}
              onChange={setDocumentFileUrl}
              label={
                isOther
                  ? 'Tệp văn bản / tài liệu gửi Ban Giám Hiệu (Tùy chọn):'
                  : 'Tệp văn bản / tài liệu gửi Ban Giám Hiệu (PDF, JPG, PNG) (* Bắt buộc):'
              }
              hint={
                isOther
                  ? 'Đính kèm tài liệu PDF/Ảnh nếu cần Ban Giám Hiệu xem xét.'
                  : 'Bắt buộc tải lên bản mềm văn bản (PDF/Ảnh) để Ban Giám Hiệu tải về kiểm tra và gắn chữ ký số.'
              }
            />

            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Ghi chú & Ý kiến thẩm định của Giáo vụ gửi BGH: <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 85, borderRadius: 10, padding: '10px 12px' }}
                value={endorsement}
                onChange={(e) => setEndorsement(e.target.value)}
                placeholder="Nhập nhận xét về hồ sơ học sinh, nội dung cần BGH xem xét và ghi chú đính kèm..."
                required
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 6 }}>
              <button type="button" className="btn btn--outline" onClick={onClose} disabled={submitting}>
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
                <Send size={16} /> {submitting ? 'Đang gửi...' : isReturnedByBoard ? 'Gửi lại Ban Giám Hiệu' : 'Trình Ban Giám Hiệu duyệt'}
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
              <button type="button" className="btn btn--outline" onClick={onClose} disabled={submitting}>
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

        {/* Form Phát hành / Phản hồi trực tiếp */}
        {activeAction === 'direct_issue' && (
          <form onSubmit={handleDirectIssue} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div
              style={{
                backgroundColor: '#F0FDF4',
                border: '1.5px solid #86EFAC',
                borderRadius: 10,
                padding: '10px 14px',
                fontSize: 13,
                color: '#166534',
              }}
            >
              💡 <strong>{isOther ? 'Giải quyết trực tiếp:' : 'Phát hành trực tiếp:'}</strong> Tải lên tệp văn bản (PDF/Ảnh đã xác nhận) và/hoặc ghi chú phản hồi để gửi trực tiếp cho phụ huynh.
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 14 }}>
              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                  Số hiệu văn bản / Số tham chiếu:
                </label>
                <input
                  type="text"
                  className="input"
                  style={{ width: '100%', borderRadius: 10, padding: '10px 12px', fontSize: 13, fontWeight: 700 }}
                  value={certificateNumber}
                  onChange={(e) => setCertificateNumber(e.target.value)}
                  placeholder="Ví dụ: SAWP-GXN-2026-0045"
                />
              </div>

              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                  Ngày cấp / phản hồi: <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <input
                  type="date"
                  className="input"
                  style={{ width: '100%', borderRadius: 10, padding: '10px 12px', fontSize: 13 }}
                  value={issueDate}
                  min={submissionDateIso}
                  max={todayIso}
                  onChange={(e) => setIssueDate(e.target.value)}
                  required
                />
              </div>
            </div>

            {!isOther && (
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 10,
                  padding: '10px 14px',
                  backgroundColor: '#F8FAFC',
                  borderRadius: 10,
                  border: '1.5px solid #CBD5E1',
                }}
              >
                <input
                  type="checkbox"
                  id="staffPaperCopy"
                  checked={isPaperCopy}
                  onChange={(e) => setIsPaperCopy(e.target.checked)}
                  style={{ width: 18, height: 18, cursor: 'pointer' }}
                />
                <label htmlFor="staffPaperCopy" style={{ fontSize: 13, fontWeight: 700, color: '#1E293B', cursor: 'pointer' }}>
                  Xác nhận phụ huynh đăng ký nhận bản giấy tại phòng Giáo vụ
                </label>
              </div>
            )}

            <DirectFileUploader
              value={documentFileUrl}
              onChange={setDocumentFileUrl}
              label={
                isOther
                  ? 'Tệp văn bản / kết quả gửi Phụ huynh (Tùy chọn):'
                  : 'Tệp văn bản đã xác nhận gửi Phụ huynh (PDF, JPG, PNG) (* Bắt buộc):'
              }
              hint={
                isOther
                  ? 'Đính kèm tệp PDF/Ảnh kết quả (nếu có).'
                  : 'Bắt buộc tải lên tệp PDF hoặc ảnh bản scan giấy tờ đã xác nhận để phụ huynh tải về.'
              }
            />

            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Ghi chú phản hồi gửi Phụ huynh:
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 80, borderRadius: 10, padding: '10px 12px' }}
                value={staffNotes}
                onChange={(e) => setStaffNotes(e.target.value)}
                placeholder="Nhập nội dung phản hồi hoặc hướng dẫn nhận bản giấy tại phòng Giáo vụ..."
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 6 }}>
              <button type="button" className="btn btn--outline" onClick={onClose} disabled={submitting}>
                Đóng
              </button>
              <button
                type="submit"
                className="btn btn--primary"
                disabled={submitting}
                style={{
                  backgroundColor: '#059669',
                  borderColor: '#1E293B',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 6,
                }}
              >
                <CheckCircle2 size={16} /> {submitting ? 'Đang xử lý...' : isOther ? 'Gửi phản hồi & Hoàn tất' : 'Phát hành trực tiếp ngay'}
              </button>
            </div>
          </form>
        )}

        {/* Form Từ chối tiếp nhận */}
        {activeAction === 'reject' && (
          <form onSubmit={handleReject} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Lý do từ chối tiếp nhận đơn: <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 90, borderRadius: 10, padding: '10px 12px' }}
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
                placeholder="Nêu rõ lý do đơn không hợp lệ (tối thiểu 5 ký tự)..."
                required
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 6 }}>
              <button type="button" className="btn btn--outline" onClick={onClose} disabled={submitting}>
                Đóng
              </button>
              <button
                type="submit"
                className="btn btn--primary"
                disabled={submitting}
                style={{
                  backgroundColor: '#EF4444',
                  borderColor: '#1E293B',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 6,
                }}
              >
                <XCircle size={16} /> {submitting ? 'Đang xử lý...' : 'Xác nhận từ chối đơn'}
              </button>
            </div>
          </form>
        )}
      </div>
    </GeometricModal>
  );
}
