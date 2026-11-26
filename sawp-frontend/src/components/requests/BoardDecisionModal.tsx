import { useState, useEffect } from 'react';
import { ArrowRight, CheckCircle2, FileText, RotateCcw, Send, Users, XCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { AttachmentViewer } from '../common/AttachmentViewer';
import { DirectFileUploader } from '../common/DirectFileUploader';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import { formatDDMMYYYY, formatDDMMYYYYHHMM, getNextMondayIso, todayIso as getTodayIso } from '../../utils/date';
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
  const [decision, setDecision] = useState<'approve' | 'return_to_staff' | 'reject'>('approve');
  const [notes, setNotes] = useState('');
  const [signedDocumentUrl, setSignedDocumentUrl] = useState('');
  const [officialEffectiveDate, setOfficialEffectiveDate] = useState<string>('');
  const [submitting, setSubmitting] = useState(false);

  const todayIso = getTodayIso();

  useEffect(() => {
    if (request) {
      setDecision('approve');
      setNotes('');
      setSignedDocumentUrl('');
      if (request.classTransfer?.effectiveDate) {
        const eff = request.classTransfer.effectiveDate.slice(0, 10);
        setOfficialEffectiveDate(eff >= todayIso ? eff : getNextMondayIso());
      } else {
        setOfficialEffectiveDate(getNextMondayIso());
      }
    }
  }, [request, todayIso]);

  if (!request) return null;

  const isTransfer = request.requestType === 'ClassTransfer';
  const isOther = request.requestType === 'Other';
  const transfer = request.classTransfer;
  const doc = request.documentRequest;
  const isReturnedByBoard = request.status === 'ReturnedToStaff' || request.status === 'NeedMoreInfo';
  const isAlreadyProcessed =
    request.status === 'Approved' ||
    request.status === 'Rejected' ||
    request.status === 'Completed' ||
    isReturnedByBoard ||
    request.schoolBoardApprovedAt != null;

  const staffUploadedDocUrl =
    doc?.issuedDocumentUrl && !doc.issuedDocumentUrl.includes('/download-document')
      ? doc.issuedDocumentUrl
      : undefined;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (decision === 'reject' && (!notes.trim() || notes.trim().length < 5)) {
      toast.error('Bắt buộc phải nhập lý do từ chối (tối thiểu 5 ký tự) để thông báo tới phụ huynh.');
      return;
    }

    if (decision === 'return_to_staff' && (!notes.trim() || notes.trim().length < 5)) {
      toast.error('Vui lòng nhập nội dung yêu cầu Giáo vụ chỉnh sửa / gửi lại (tối thiểu 5 ký tự).');
      return;
    }

    if (decision === 'approve' && !isTransfer && !isOther && !signedDocumentUrl.trim()) {
      toast.error('Vui lòng tải lên tệp văn bản đã gắn chữ ký số / đóng dấu (PDF hoặc Ảnh) trước khi phê duyệt phát hành.');
      return;
    }

    if (decision === 'approve' && isTransfer) {
      if (!officialEffectiveDate) {
        toast.error('Vui lòng chọn ngày có hiệu lực chuyển lớp.');
        return;
      }
      if (officialEffectiveDate < todayIso) {
        toast.error('Ngày có hiệu lực chuyển lớp không được là ngày trong quá khứ.');
        return;
      }
    }

    try {
      setSubmitting(true);
      const isApproved = decision === 'approve';
      const returnToStaff = decision === 'return_to_staff';

      const res = await administrativeRequestApi.processBoardDecision(request.id, {
        isApproved,
        returnToStaff,
        decisionNotes: notes.trim() || undefined,
        signedDocumentUrl: isApproved && !isTransfer ? signedDocumentUrl.trim() || undefined : undefined,
        effectiveDate: isApproved && isTransfer ? officialEffectiveDate || undefined : undefined,
      });

      if (res.success) {
        if (returnToStaff) {
          toast.success('Đã trả hồ sơ về cho bộ phận Giáo vụ chỉnh sửa và trình lại!');
        } else if (isApproved) {
          toast.success(
            isTransfer
              ? 'Đã duyệt đơn chuyển lớp! Hồ sơ đã được chuyển tới Giáo vụ để thực thi phân lớp.'
              : isOther
                ? 'Đã phê duyệt đơn kiến nghị / yêu cầu khác!'
                : 'Đã phê duyệt và phát hành văn bản chính thức!'
          );
        } else {
          toast.success('Đã từ chối đơn yêu cầu và ghi vết nhật ký kiểm toán (Audit Log).');
        }
        onSuccess();
        onClose();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể xử lý quyết định');
      }
    } catch (err: any) {
      const errorMsg =
        err?.response?.data?.errors?.[0] ||
        err?.response?.data?.message ||
        err?.message ||
        'Đã xảy ra lỗi khi gửi quyết định.';
      toast.error(errorMsg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title={
        isTransfer
          ? 'Ban Giám Hiệu Xét Duyệt Chuyển Lớp'
          : isOther
            ? 'Ban Giám Hiệu Xét Duyệt Loại Đơn Khác'
            : 'Ban Giám Hiệu Phê Duyệt Cấp Văn Bản'
      }
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
              Lớp hiện tại: <strong>{request.currentClassName || 'Chưa xếp lớp'}</strong>
            </div>
            <div style={{ fontSize: 13, color: '#64748B', marginTop: 2 }}>
              Phụ huynh nộp: <strong>{request.requesterName}</strong>
            </div>
          </div>

          <div>
            <div style={{ fontSize: 12, fontWeight: 800, color: '#64748B' }}>
              {isTransfer ? 'LỘ TRÌNH CHUYỂN LỚP:' : isOther ? 'TIÊU ĐỀ ĐƠN KHÁC:' : 'LOẠI VĂN BẢN ĐỀ NGHỊ:'}
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
                {transfer.effectiveDate && (
                  <div style={{ marginTop: 6, fontSize: 12, color: '#1E293B' }}>
                    📅 <strong>Ngày Giáo vụ đề xuất hiệu lực:</strong>{' '}
                    <span style={{ padding: '2px 8px', backgroundColor: '#FEF3C7', color: '#92400E', borderRadius: 6, fontWeight: 700 }}>
                      {formatDDMMYYYY(transfer.effectiveDate)}
                    </span>
                  </div>
                )}
              </div>
            )}

            {!isTransfer && doc && (
              <div>
                <div style={{ fontSize: 15, fontWeight: 800, color: '#6B21A8', marginTop: 4 }}>
                  {doc.documentType} {!isOther && `(${doc.numberOfCopies} bản)`}
                </div>
                <div style={{ fontSize: 13, color: '#334155', marginTop: 2 }}>
                  <strong>{isOther ? 'Nội dung:' : 'Mục đích:'}</strong> {doc.purpose}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Ý kiến thẩm định của Giáo vụ + Tệp minh chứng Phụ huynh */}
        <div
          style={{
            backgroundColor: '#EFF6FF',
            border: '2px solid #3B82F6',
            borderRadius: 12,
            padding: 14,
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 800, color: '#1D4ED8', marginBottom: 4 }}>
            GHI CHÚ & Ý KIẾN THẨM ĐỊNH CỦA BỘ PHẬN GIÁO VỤ:
          </div>
          <div style={{ fontSize: 14, color: '#1E40AF', lineHeight: 1.5, fontStyle: 'italic' }}>
            &quot;{isTransfer ? transfer?.staffRecommendation || 'Hồ sơ đầy đủ, đủ điều kiện chuyển lớp.' : doc?.staffEndorsement || 'Hồ sơ xác thực hợp lệ.'}&quot;
          </div>
          <AttachmentViewer url={request.attachmentUrl} />
        </div>

        {/* Tệp PDF/Ảnh Giáo vụ tải lên để BGH tải về & ký số */}
        {!isTransfer && doc && (
          <div
            style={{
              backgroundColor: '#FFFFFF',
              border: '2px solid #CBD5E1',
              borderRadius: 12,
              padding: 16,
            }}
          >
            <div style={{ fontSize: 12, fontWeight: 800, color: '#334155', marginBottom: 8, display: 'flex', alignItems: 'center', gap: 6 }}>
              <FileText size={15} color="#4F46E5" /> TỆP VĂN BẢN / TÀI LIỆU DO GIÁO VỤ GỬI LÊN (TẢI VỀ ĐỂ KÝ SỐ):
            </div>
            {staffUploadedDocUrl ? (
              <AttachmentViewer url={staffUploadedDocUrl} />
            ) : (
              <div style={{ fontSize: 13, color: '#64748B', fontStyle: 'italic' }}>
                Giáo vụ chưa đính kèm tệp văn bản PDF/ảnh nào cho đơn này. Nếu cần Giáo vụ gửi bổ sung tệp văn bản, BGH có thể chọn &quot;Yêu cầu Giáo vụ trình lại&quot; bên dưới.
              </div>
            )}
          </div>
        )}

        {/* Quyết định / Chỉ đạo của BGH */}
        {isAlreadyProcessed ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div
              style={{
                backgroundColor:
                  request.status === 'Approved' || request.status === 'Completed'
                    ? '#ECFDF5'
                    : isReturnedByBoard
                      ? '#FFF7ED'
                      : '#FEF2F2',
                border: `2px solid ${
                  request.status === 'Approved' || request.status === 'Completed'
                    ? '#059669'
                    : isReturnedByBoard
                      ? '#EA580C'
                      : '#DC2626'
                }`,
                borderRadius: 12,
                padding: '14px 16px',
              }}
            >
              <div
                style={{
                  fontSize: 13,
                  fontWeight: 800,
                  color:
                    request.status === 'Approved' || request.status === 'Completed'
                      ? '#047857'
                      : isReturnedByBoard
                        ? '#9A3412'
                        : '#B91C1C',
                  marginBottom: 4,
                }}
              >
                {request.status === 'Approved' || request.status === 'Completed'
                  ? 'KẾT QUẢ: BAN GIÁM HIỆU ĐÃ PHÊ DUYỆT'
                  : isReturnedByBoard
                    ? 'TRẠNG THÁI: ĐÃ YÊU CẦU GIÁO VỤ CHỈNH SỬA & TRÌNH LẠI'
                    : 'KẾT QUẢ: BAN GIÁM HIỆU ĐÃ TỪ CHỐI'}
              </div>
              <div style={{ fontSize: 13, color: '#1E293B', fontWeight: 600, lineHeight: 1.5 }}>
                &quot;{request.schoolBoardNotes || request.reviewerNotes || 'Đã xử lý.'}&quot;
              </div>
              {request.schoolBoardApprovedAt && (
                <div style={{ fontSize: 11, color: '#64748B', marginTop: 6 }}>
                  Thời gian phê duyệt: {formatDDMMYYYYHHMM(request.schoolBoardApprovedAt)}
                </div>
              )}
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 6 }}>
              <button type="button" className="btn btn--outline" onClick={onClose}>
                Đóng
              </button>
            </div>
          </div>
        ) : (
          <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div>
              <div style={{ fontSize: 13, fontWeight: 800, color: '#1E293B', marginBottom: 8 }}>
                QUYẾT ĐỊNH CỦA BAN GIÁM HIỆU: <span style={{ color: '#EF4444' }}>*</span>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 10 }}>
                <label
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 8,
                    padding: '11px 14px',
                    borderRadius: 12,
                    border: decision === 'approve' ? '2.5px solid #16A34A' : '2px solid #E2E8F0',
                    backgroundColor: decision === 'approve' ? '#F0FDF4' : '#FFFFFF',
                    boxShadow: decision === 'approve' ? '3px 3px 0 #16A34A' : 'none',
                    cursor: 'pointer',
                    fontWeight: 800,
                    fontSize: 13,
                    color: decision === 'approve' ? '#15803D' : '#64748B',
                  }}
                >
                  <input
                    type="radio"
                    name="decision"
                    checked={decision === 'approve'}
                    onChange={() => setDecision('approve')}
                    style={{ width: 16, height: 16, accentColor: '#16A34A' }}
                  />
                  <CheckCircle2 size={18} color="#16A34A" />
                  PHÊ DUYỆT
                </label>

                {!isTransfer && (
                  <label
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: 8,
                      padding: '11px 14px',
                      borderRadius: 12,
                      border: decision === 'return_to_staff' ? '2.5px solid #EA580C' : '2px solid #E2E8F0',
                      backgroundColor: decision === 'return_to_staff' ? '#FFF7ED' : '#FFFFFF',
                      boxShadow: decision === 'return_to_staff' ? '3px 3px 0 #EA580C' : 'none',
                      cursor: 'pointer',
                      fontWeight: 800,
                      fontSize: 13,
                      color: decision === 'return_to_staff' ? '#9A3412' : '#64748B',
                    }}
                  >
                    <input
                      type="radio"
                      name="decision"
                      checked={decision === 'return_to_staff'}
                      onChange={() => setDecision('return_to_staff')}
                      style={{ width: 16, height: 16, accentColor: '#EA580C' }}
                    />
                    <RotateCcw size={18} color="#EA580C" />
                    YÊU CẦU GIÁO VỤ TRÌNH LẠI
                  </label>
                )}

                <label
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 8,
                    padding: '11px 14px',
                    borderRadius: 12,
                    border: decision === 'reject' ? '2.5px solid #EF4444' : '2px solid #E2E8F0',
                    backgroundColor: decision === 'reject' ? '#FEF2F2' : '#FFFFFF',
                    boxShadow: decision === 'reject' ? '3px 3px 0 #EF4444' : 'none',
                    cursor: 'pointer',
                    fontWeight: 800,
                    fontSize: 13,
                    color: decision === 'reject' ? '#B91C1C' : '#64748B',
                  }}
                >
                  <input
                    type="radio"
                    name="decision"
                    checked={decision === 'reject'}
                    onChange={() => setDecision('reject')}
                    style={{ width: 16, height: 16, accentColor: '#EF4444' }}
                  />
                  <XCircle size={18} color="#EF4444" />
                  TỪ CHỐI
                </label>
              </div>
            </div>

            {decision === 'approve' && isTransfer && (
              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                  Ngày có hiệu lực chính thức: <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <input
                  type="date"
                  className="input"
                  style={{ width: '100%', borderRadius: 10, padding: '10px 12px', fontSize: 14 }}
                  value={officialEffectiveDate}
                  onChange={(e) => setOfficialEffectiveDate(e.target.value)}
                  min={todayIso}
                  required
                />
                <div style={{ fontSize: 12, color: '#64748B', marginTop: 4 }}>
                  Mặc định theo ngày đề xuất của Giáo vụ ({formatDDMMYYYY(officialEffectiveDate)}). Ban Giám Hiệu có thể điều chỉnh ngày bắt đầu có hiệu lực (không được chọn ngày quá khứ).
                </div>
              </div>
            )}

            {decision === 'approve' && !isTransfer && (
              <DirectFileUploader
                value={signedDocumentUrl}
                onChange={setSignedDocumentUrl}
                label={
                  isOther
                    ? 'Tải lên tệp kết quả / văn bản phản hồi đã ký (Tùy chọn):'
                    : 'Tải lên bản văn bản đã gắn chữ ký số / đóng dấu để phát hành (* Bắt buộc):'
                }
                hint={
                  isOther
                    ? 'Tùy chọn đính kèm tệp PDF/Ảnh phản hồi nếu cần gửi phụ huynh.'
                    : 'Bắt buộc tải lên tệp văn bản chính thức đã ký số (PDF/Ảnh). Phụ huynh sẽ nhận đúng tệp này sau khi phê duyệt.'
                }
              />
            )}

            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 13, marginBottom: 6 }}>
                {decision === 'approve'
                  ? 'Ý kiến chỉ đạo / Ghi chú phê duyệt (tùy chọn):'
                  : decision === 'return_to_staff'
                    ? 'Nội dung yêu cầu Giáo vụ chỉnh sửa / gửi lại:'
                    : 'Lý do từ chối yêu cầu:'}{' '}
                {decision !== 'approve' && <span style={{ color: '#EF4444' }}>*</span>}
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 85, borderRadius: 10, padding: '10px 12px' }}
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder={
                  decision === 'approve'
                    ? 'Nhập ý kiến chỉ đạo hoặc lời nhắn kèm theo...'
                    : decision === 'return_to_staff'
                      ? 'Nêu rõ sai sót hoặc thông tin/tệp tin Giáo vụ cần chỉnh sửa lại trước khi trình lại BGH (tối thiểu 5 ký tự)...'
                      : 'Nêu rõ lý do từ chối để thông báo cho phụ huynh (tối thiểu 5 ký tự)...'
                }
                required={decision !== 'approve'}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 6 }}>
              <button type="button" className="btn btn--outline" onClick={onClose} disabled={submitting}>
                Hủy
              </button>
              <button
                type="submit"
                className="btn btn--primary"
                disabled={submitting}
                style={{
                  backgroundColor:
                    decision === 'approve'
                      ? '#16A34A'
                      : decision === 'return_to_staff'
                        ? '#EA580C'
                        : '#DC2626',
                  borderColor: '#1E293B',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 6,
                }}
              >
                <Send size={15} />
                {submitting
                  ? 'Đang xử lý...'
                  : decision === 'approve'
                    ? 'Xác nhận Phê duyệt'
                    : decision === 'return_to_staff'
                      ? 'Trả về Giáo vụ chỉnh sửa'
                      : 'Xác nhận Từ chối'}
              </button>
            </div>
          </form>
        )}
      </div>
    </GeometricModal>
  );
}
