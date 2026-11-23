import { useState, useEffect } from 'react';
import { AlertCircle, CheckCircle2, Eye, FileText, Lock, Send, XCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { AttachmentViewer } from '../common/AttachmentViewer';
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
  const [activeAction, setActiveAction] = useState<'endorse' | 'direct_issue' | 'amendment' | 'reject'>('endorse');
  const [endorsement, setEndorsement] = useState('');
  const [draftContent, setDraftContent] = useState('');
  const [amendmentNotes, setAmendmentNotes] = useState('');
  const [certificateNumber, setCertificateNumber] = useState('');
  const [issueDate, setIssueDate] = useState(new Date().toISOString().split('T')[0]);
  const [digitalSignatureInfo, setDigitalSignatureInfo] = useState('Phòng Giáo Vụ - Trường Tiểu học SAWP');
  const [isPaperCopy, setIsPaperCopy] = useState(false);
  const [staffNotes, setStaffNotes] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (request?.documentRequest) {
      setEndorsement(
        request.documentRequest.staffEndorsement ||
          'Hồ sơ học sinh đầy đủ, xác thực thông tin theo học chính khóa tại nhà trường.'
      );
      setAmendmentNotes(request.amendmentNotes || '');
      setCertificateNumber(`SAWP-GXN-${new Date().getFullYear()}-${request.id.toString().padStart(4, '0')}`);
      setIsPaperCopy(request.documentRequest.isPaperCopyRequested || false);

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
    } else if (lowerType.includes('phúc khảo')) {
      content = `Văn phòng Trường Tiểu học SAWP tiếp nhận đề nghị phúc khảo bài kiểm tra định kỳ của học sinh ${studentName} (Mã định danh: ${studentCode}) - Lớp ${request?.currentClassName || 'chính khóa'}.\n\nNội dung đề nghị phúc khảo: ${purpose || 'Xem lại bài kiểm tra định kỳ môn học'}.\n\nBan Giám Hiệu nhà trường sẽ thành lập Hội đồng chấm phúc khảo theo đúng Quy chế của Bộ GD&ĐT và thông báo kết quả chính thức tới phụ huynh trong thời hạn 05 ngày làm việc.`;
    } else if (lowerType.includes('miễn, giảm') || lowerType.includes('miễn giảm') || lowerType.includes('hỗ trợ chi phí')) {
      content = `Trường Tiểu học SAWP xác nhận học sinh ${studentName} (Mã định danh: ${studentCode}), hiện là học sinh lớp ${request?.currentClassName || 'chính khóa'} năm học 2026 - 2027, thuộc diện hồ sơ đề nghị xem xét chế độ miễn, giảm các khoản đóng góp / hỗ trợ chi phí học tập theo quy định hiện hành của Nhà nước và Nhà trường.\n\nCăn cứ đề nghị và minh chứng kèm theo: ${purpose || 'Chính sách an sinh xã hội / đối tượng ưu đãi theo quy định'}.`;
    } else if (lowerType.includes('học bạ') || lowerType.includes('trích lục')) {
      content = `Trường Tiểu học SAWP chứng thực trích lục sao y từ Sổ đăng bộ và Học bạ gốc đang lưu trữ tại trường của học sinh ${studentName} (Mã định danh: ${studentCode}), học sinh lớp ${request?.currentClassName || 'chính khóa'}.\n\nBản sao trích lục này có giá trị sử dụng tương đương bản gốc để hoàn thiện thủ tục: ${purpose || 'Bổ sung / cấp lại hồ sơ học tập'}.`;
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

  const sensitiveTypesRequiringBoard = [
    'chuyển trường',
    'phúc khảo',
    'miễn, giảm',
    'miễn giảm',
    'hỗ trợ chi phí',
  ];
  const isBoardRequired = sensitiveTypesRequiringBoard.some((t) =>
    request.documentRequest?.documentType?.toLowerCase().includes(t.toLowerCase())
  );

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
    } catch (err: any) {
      const errorMsg =
        err?.response?.data?.message ||
        err?.response?.data?.errors?.[0] ||
        err?.message ||
        'Đã xảy ra lỗi khi gửi dự thảo lên BGH.';
      toast.error(errorMsg);
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
    } catch (err: any) {
      const errorMsg =
        err?.response?.data?.message ||
        err?.response?.data?.errors?.[0] ||
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
    if (!certificateNumber.trim()) {
      toast.error('Vui lòng nhập số hiệu văn bản / số tham chiếu.');
      return;
    }

    try {
      setSubmitting(true);
      const res = await administrativeRequestApi.directIssue(request.id, {
        certificateNumber: certificateNumber.trim(),
        issuedDate: issueDate,
        digitalSignatureInfo: digitalSignatureInfo.trim(),
        documentContent: draftContent.trim(),
        isPaperCopy,
        staffNotes: staffNotes.trim(),
      });

      if (res.success) {
        toast.success(res.message || 'Đã phát hành văn bản trực tiếp thành công!');
        onSuccess();
        onClose();
      } else {
        toast.error(res.message || 'Không thể phát hành văn bản');
      }
    } catch (err: any) {
      const errorMsg =
        err?.response?.data?.message ||
        err?.response?.data?.errors?.[0] ||
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
    if (!rejectReason.trim()) {
      toast.error('Vui lòng nhập lý do từ chối.');
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
        toast.error(res.message || 'Không thể từ chối đơn');
      }
    } catch (err: any) {
      const errorMsg =
        err?.response?.data?.message ||
        err?.response?.data?.errors?.[0] ||
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
            <AttachmentViewer url={request.attachmentUrl} compact />
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
        <div style={{ display: 'flex', gap: 10, borderBottom: '2px solid #E2E8F0', paddingBottom: 12, flexWrap: 'wrap' }}>
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
            <FileText size={16} /> Soạn dự thảo & Trình BGH
          </button>

          <button
            type="button"
            disabled={isAwaitingParent || isAlreadyUnderReview || isClosed || isBoardRequired}
            onClick={() => {
              if (isBoardRequired) {
                toast.error('Loại văn bản này thuộc thẩm quyền của Ban Giám Hiệu. Giáo vụ vui lòng chọn tab "Soạn dự thảo & Trình BGH".');
                return;
              }
              setActiveAction('direct_issue');
            }}
            title={isBoardRequired ? 'Loại thủ tục này bắt buộc Ban Giám Hiệu ký duyệt' : isAwaitingParent ? 'Hồ sơ đang chờ phụ huynh bổ sung' : undefined}
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
            {isBoardRequired ? 'BGH ký duyệt (Không cấp trực tiếp)' : 'Phát hành trực tiếp'}
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
            <XCircle size={16} /> Từ chối tiếp nhận (Không hợp lệ)
          </button>
        </div>

        {/* Form Soạn dự thảo & Trình BGH */}
        {activeAction === 'endorse' && (
          <form onSubmit={handleEndorse} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div>
              <div style={{ marginBottom: 6 }}>
                <label style={{ fontWeight: 700, fontSize: 13, display: 'block' }}>
                  Nội dung dự thảo văn bản chính thức: <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <span style={{ fontSize: 11, color: '#64748B' }}>
                  (Soạn thảo nội dung xác nhận để in vào văn bản chính thức cho phụ huynh tải về)
                </span>
              </div>

              <textarea
                className="input"
                style={{
                  width: '100%',
                  minHeight: 120,
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

              {/* Bản xem trước văn bản phát hành chính thức */}
              <div style={{ marginTop: 10, backgroundColor: '#F8FAFC', border: '1.5px solid #CBD5E1', borderRadius: 10, padding: 14 }}>
                <div style={{ fontSize: 12, fontWeight: 800, color: '#334155', marginBottom: 8, display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Eye size={14} color="#4F46E5" /> XEM TRƯỚC VĂN BẢN KHI PHÁT HÀNH:
                </div>
                <div style={{ backgroundColor: '#FFFFFF', border: '1px solid #E2E8F0', borderRadius: 8, padding: 16, fontFamily: 'Times New Roman, serif', color: '#000000', fontSize: 13, lineHeight: 1.5 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid #E2E8F0', paddingBottom: 10, marginBottom: 12 }}>
                    <div style={{ textAlign: 'center' }}>
                      <div style={{ fontSize: 11 }}>UBND THÀNH PHỐ HÀ NỘI</div>
                      <div style={{ fontWeight: 800, fontSize: 12 }}>TRƯỜNG TIỂU HỌC SAWP</div>
                      <div style={{ fontSize: 11, marginTop: 4 }}>Số: <em>{request.documentRequest?.certificateNumber || 'SAWP-2026-XXXX'}</em></div>
                    </div>
                    <div style={{ textAlign: 'center' }}>
                      <div style={{ fontWeight: 800, fontSize: 12 }}>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
                      <div style={{ fontWeight: 700, fontSize: 12 }}>Độc lập - Tự do - Hạnh phúc</div>
                      <div style={{ fontSize: 11, fontStyle: 'italic', marginTop: 4 }}>Hà Nội, ngày {new Date().getDate()} tháng {new Date().getMonth() + 1} năm {new Date().getFullYear()}</div>
                    </div>
                  </div>
                  <div style={{ textAlign: 'center', fontWeight: 800, fontSize: 15, margin: '10px 0', textTransform: 'uppercase' }}>
                    {doc.documentType}
                  </div>
                  <div style={{ margin: '8px 0', fontSize: 13 }}>
                    <div>• <strong>Học sinh:</strong> {request.studentName} (Mã định danh: {request.studentCode})</div>
                    <div>• <strong>Lớp:</strong> {request.currentClassName || 'Chính khóa'} - Trường Tiểu học SAWP</div>
                    <div>• <strong>Mục đích:</strong> {doc.purpose} ({doc.numberOfCopies} bản chính)</div>
                  </div>
                  <div style={{ backgroundColor: '#F1F5F9', padding: '10px 12px', borderRadius: 6, margin: '10px 0', whiteSpace: 'pre-wrap', fontStyle: 'italic' }}>
                    {draftContent || '(Nội dung xác nhận cụ thể sẽ hiển thị tại đây)'}
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 12, textAlign: 'center' }}>
                    <div style={{ width: 200 }}>
                      <div style={{ fontWeight: 800, fontSize: 12 }}>TM. BAN GIÁM HIỆU</div>
                      <div style={{ fontWeight: 700, fontSize: 12 }}>HIỆU TRƯỞNG</div>
                      <div style={{ marginTop: 18, fontSize: 11, color: '#059669', border: '1px solid #10B981', padding: '4px 8px', borderRadius: 6, backgroundColor: '#ECFDF5' }}>
                        ✓ KÝ SỐ ĐIỆN TỬ SAWP
                      </div>
                    </div>
                  </div>
                </div>
              </div>
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

        {/* Form Phát hành trực tiếp */}
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
              💡 <strong>Phát hành trực tiếp:</strong> Giáo vụ được quyền phát hành trực tiếp các giấy tờ hành chính thông thường (như Giấy xác nhận đang theo học) theo mẫu chuẩn mà không bắt buộc phải trình qua Ban Giám Hiệu.
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 14 }}>
              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                  Số hiệu văn bản / Số tham chiếu: <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <input
                  type="text"
                  className="input"
                  style={{ width: '100%', borderRadius: 10, padding: '10px 12px', fontSize: 13, fontWeight: 700 }}
                  value={certificateNumber}
                  onChange={(e) => setCertificateNumber(e.target.value)}
                  placeholder="Ví dụ: SAWP-GXN-2026-0045"
                  required
                />
              </div>

              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                  Ngày cấp văn bản: <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <input
                  type="date"
                  className="input"
                  style={{ width: '100%', borderRadius: 10, padding: '10px 12px', fontSize: 13 }}
                  value={issueDate}
                  onChange={(e) => setIssueDate(e.target.value)}
                  required
                />
              </div>
            </div>

            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Thông tin xác thực phát hành:
              </label>
              <input
                type="text"
                className="input"
                style={{ width: '100%', borderRadius: 10, padding: '10px 12px', fontSize: 13 }}
                value={digitalSignatureInfo}
                onChange={(e) => setDigitalSignatureInfo(e.target.value)}
              />
            </div>

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

            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Nội dung xác nhận theo mẫu chuẩn của trường: <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{
                  width: '100%',
                  minHeight: 110,
                  borderRadius: 10,
                  padding: '12px 14px',
                  fontFamily: 'monospace',
                  fontSize: 13,
                  lineHeight: 1.5,
                }}
                value={draftContent}
                onChange={(e) => setDraftContent(e.target.value)}
                placeholder="Nhập nội dung xác nhận..."
                required
              />

              {/* Bản xem trước văn bản phát hành trực tiếp */}
              <div style={{ marginTop: 10, backgroundColor: '#F8FAFC', border: '1.5px solid #CBD5E1', borderRadius: 10, padding: 14 }}>
                <div style={{ fontSize: 12, fontWeight: 800, color: '#334155', marginBottom: 8, display: 'flex', alignItems: 'center', gap: 6 }}>
                  <Eye size={14} color="#059669" /> XEM TRƯỚC VĂN BẢN KHI PHÁT HÀNH TRỰC TIẾP:
                </div>
                <div style={{ backgroundColor: '#FFFFFF', border: '1px solid #E2E8F0', borderRadius: 8, padding: 16, fontFamily: 'Times New Roman, serif', color: '#000000', fontSize: 13, lineHeight: 1.5 }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid #E2E8F0', paddingBottom: 10, marginBottom: 12 }}>
                    <div style={{ textAlign: 'center' }}>
                      <div style={{ fontSize: 11 }}>UBND THÀNH PHỐ HÀ NỘI</div>
                      <div style={{ fontWeight: 800, fontSize: 12 }}>TRƯỜNG TIỂU HỌC SAWP</div>
                      <div style={{ fontSize: 11, marginTop: 4 }}>Số: <em>{certificateNumber || 'SAWP-GXN-2026-XXXX'}</em></div>
                    </div>
                    <div style={{ textAlign: 'center' }}>
                      <div style={{ fontWeight: 800, fontSize: 12 }}>CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
                      <div style={{ fontWeight: 700, fontSize: 12 }}>Độc lập - Tự do - Hạnh phúc</div>
                      <div style={{ fontSize: 11, fontStyle: 'italic', marginTop: 4 }}>Hà Nội, ngày {new Date(issueDate).getDate()} tháng {new Date(issueDate).getMonth() + 1} năm {new Date(issueDate).getFullYear()}</div>
                    </div>
                  </div>
                  <div style={{ textAlign: 'center', fontWeight: 800, fontSize: 15, margin: '10px 0', textTransform: 'uppercase' }}>
                    {doc.documentType}
                  </div>
                  <div style={{ margin: '8px 0', fontSize: 13 }}>
                    <div>• <strong>Học sinh:</strong> {request.studentName} (Mã định danh: {request.studentCode})</div>
                    <div>• <strong>Lớp:</strong> {request.currentClassName || 'Chính khóa'} - Trường Tiểu học SAWP</div>
                    <div>• <strong>Mục đích:</strong> {doc.purpose} ({doc.numberOfCopies} bản chính)</div>
                  </div>
                  <div style={{ backgroundColor: '#F0FDF4', padding: '10px 12px', borderRadius: 6, margin: '10px 0', whiteSpace: 'pre-wrap', fontStyle: 'italic', border: '1px solid #BBF7D0' }}>
                    {draftContent || '(Nội dung xác nhận cụ thể sẽ hiển thị tại đây)'}
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 12, textAlign: 'center' }}>
                    <div style={{ width: 220 }}>
                      <div style={{ fontWeight: 800, fontSize: 12 }}>TL. HIỆU TRƯỞNG</div>
                      <div style={{ fontWeight: 700, fontSize: 12 }}>VĂN PHÒNG GIÁO VỤ</div>
                      <div style={{ marginTop: 14, fontSize: 11, color: '#047857', border: '1.5px solid #059669', padding: '6px 8px', borderRadius: 6, backgroundColor: '#ECFDF5', fontWeight: 700 }}>
                        ✓ VĂN PHÒNG GIÁO VỤ SAWP<br />
                        XÁC THỰC BẢN CHÍNH<br />
                        <span style={{ fontSize: 10, fontWeight: 400 }}>Ngày: {issueDate}</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Ghi chú nội bộ của Giáo vụ (nếu có):
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 70, borderRadius: 10, padding: '10px 12px' }}
                value={staffNotes}
                onChange={(e) => setStaffNotes(e.target.value)}
                placeholder="Ghi chú thêm về việc phát hành hoặc thời gian hẹn phụ huynh nhận bản giấy..."
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
                  backgroundColor: '#059669',
                  borderColor: '#1E293B',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 6,
                }}
              >
                <CheckCircle2 size={16} /> {submitting ? 'Đang phát hành...' : 'Phát hành trực tiếp ngay'}
              </button>
            </div>
          </form>
        )}

        {/* Form Từ chối tiếp nhận (Không hợp lệ) */}
        {activeAction === 'reject' && (
          <form onSubmit={handleReject} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                Lý do từ chối tiếp nhận đơn xin giấy tờ: <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 90, borderRadius: 10, padding: '10px 12px' }}
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
                placeholder="Nêu rõ lý do đơn không hợp lệ, mục đích sử dụng không phù hợp hoặc thông tin sai lệch..."
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
