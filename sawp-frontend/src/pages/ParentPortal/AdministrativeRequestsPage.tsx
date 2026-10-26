import { useState, useEffect, useCallback } from 'react';
import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  Clock,
  Download,
  FileText,
  FileUp,
  GraduationCap,
  History,
  Info,
  RefreshCw,
  Send,
  XCircle,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { useParentChild } from '../../contexts/ParentChildContext';
import { ChildSelectorPill } from '../../components/parent/ChildSelectorPill';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import { classApi } from '../../services/implementations/classApi';
import { ResubmitModal } from '../../components/requests/ResubmitModal';
import type { AdministrativeRequestSummary } from '../../types/request.types';
import type { ClassSummaryDto } from '../../types/class.types';

export default function AdministrativeRequestsPage() {
  const { activeChild } = useParentChild();

  const [activeTab, setActiveTab] = useState<'list' | 'transfer' | 'document'>('list');
  const [requests, setRequests] = useState<AdministrativeRequestSummary[]>([]);
  const [classes, setClasses] = useState<ClassSummaryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Form Chuyển lớp
  const [targetClassId, setTargetClassId] = useState<number | ''>('');
  const [transferReason, setTransferReason] = useState('');
  const [transferAttachment, setTransferAttachment] = useState('');

  // Form Giấy tờ
  const [documentType, setDocumentType] = useState('Giấy xác nhận học sinh đang theo học');
  const [numberOfCopies, setNumberOfCopies] = useState<number>(1);
  const [documentPurpose, setDocumentPurpose] = useState('');
  const [documentAttachment, setDocumentAttachment] = useState('');

  // Modal nộp lại khi có Amendment
  const [selectedRequestForResubmit, setSelectedRequestForResubmit] = useState<AdministrativeRequestSummary | null>(null);
  const [resubmitOpen, setResubmitOpen] = useState(false);

  // Lấy danh sách đơn của con
  const fetchMyRequests = useCallback(async () => {
    if (!activeChild?.studentId) return;
    try {
      setLoading(true);
      const res = await administrativeRequestApi.getMyRequests(activeChild.studentId);
      if (res.success && Array.isArray(res.data)) {
        setRequests(res.data);
      }
    } catch {
      toast.error('Không thể tải danh sách đơn hành chính.');
    } finally {
      setLoading(false);
    }
  }, [activeChild?.studentId]);

  // Lấy danh sách lớp để chọn khi xin chuyển lớp
  const fetchClasses = useCallback(async () => {
    try {
      const res = await classApi.getClasses();
      if (res.success && Array.isArray(res.data)) {
        setClasses(res.data);
      }
    } catch {
      // Bỏ qua lỗi ngầm
    }
  }, []);

  useEffect(() => {
    fetchMyRequests();
    fetchClasses();
  }, [fetchMyRequests, fetchClasses]);

  // Xử lý nộp đơn chuyển lớp
  const handleSubmitTransfer = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeChild?.studentId) {
      toast.error('Vui lòng chọn học sinh trước khi nộp đơn.');
      return;
    }
    if (!targetClassId) {
      toast.error('Vui lòng chọn lớp muốn chuyển tới.');
      return;
    }
    if (!transferReason.trim()) {
      toast.error('Vui lòng nhập lý do chuyển lớp.');
      return;
    }

    try {
      setSubmitting(true);
      const res = await administrativeRequestApi.submitClassTransfer({
        studentId: activeChild.studentId,
        targetClassId: Number(targetClassId),
        transferReason: transferReason.trim(),
        attachmentUrl: transferAttachment.trim() || undefined,
      });

      if (res.success) {
        toast.success(res.message || 'Đã gửi đơn xin chuyển lớp thành công!');
        setTransferReason('');
        setTransferAttachment('');
        setTargetClassId('');
        setActiveTab('list');
        fetchMyRequests();
      } else {
        toast.error(res.message || 'Không thể gửi đơn chuyển lớp');
      }
    } catch {
      toast.error('Đã xảy ra lỗi khi gửi đơn chuyển lớp.');
    } finally {
      setSubmitting(false);
    }
  };

  // Xử lý nộp đơn xin giấy tờ
  const handleSubmitDocument = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeChild?.studentId) {
      toast.error('Vui lòng chọn học sinh trước khi nộp đơn.');
      return;
    }
    if (!documentPurpose.trim()) {
      toast.error('Vui lòng nhập mục đích xin cấp giấy tờ.');
      return;
    }

    try {
      setSubmitting(true);
      const res = await administrativeRequestApi.submitDocument({
        studentId: activeChild.studentId,
        documentType: documentType.trim(),
        numberOfCopies: Number(numberOfCopies),
        purpose: documentPurpose.trim(),
        attachmentUrl: documentAttachment.trim() || undefined,
      });

      if (res.success) {
        toast.success(res.message || 'Đã gửi đơn xin cấp giấy tờ thành công!');
        setDocumentPurpose('');
        setDocumentAttachment('');
        setNumberOfCopies(1);
        setActiveTab('list');
        fetchMyRequests();
      } else {
        toast.error(res.message || 'Không thể gửi đơn xin cấp giấy tờ');
      }
    } catch {
      toast.error('Đã xảy ra lỗi khi gửi đơn xin cấp giấy tờ.');
    } finally {
      setSubmitting(false);
    }
  };

  // Tải văn bản đã phát hành
  const handleDownload = async (req: AdministrativeRequestSummary) => {
    try {
      const fileName = `Giay_Xac_Nhan_${req.studentCode || 'HS'}_${req.requestNumber}.txt`;
      await administrativeRequestApi.downloadIssuedDocument(req.id, fileName);
      toast.success('Đã tải văn bản chính thức thành công!');
    } catch {
      toast.error('Không thể tải văn bản. Vui lòng thử lại sau.');
    }
  };

  // Lọc các lớp đích phù hợp (khác lớp hiện tại của con)
  const availableTargetClasses = classes.filter(
    (c) => c.className !== activeChild?.currentClassName && c.status === 'Active'
  );

  return (
    <div style={{ maxWidth: 1100, margin: '0 auto', padding: '16px 20px 48px' }}>
      {/* Header bar */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 16,
          marginBottom: 24,
        }}
      >
        <div>
          <h1
            style={{
              fontSize: 26,
              fontWeight: 800,
              color: '#1E293B',
              margin: '0 0 6px',
              display: 'flex',
              alignItems: 'center',
              gap: 10,
            }}
          >
            <span
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                width: 40,
                height: 40,
                backgroundColor: '#E0E7FF',
                color: '#4F46E5',
                borderRadius: 12,
                border: '2px solid #1E293B',
                boxShadow: '2px 2px 0 #1E293B',
              }}
            >
              📄
            </span>
            Đơn Hành Chính & Giấy Tờ Học Sinh
          </h1>
          <p style={{ margin: 0, color: '#64748B', fontSize: 14 }}>
            Gửi yêu cầu xin chuyển lớp hoặc xin cấp các loại giấy xác nhận, chứng nhận điện tử từ Nhà trường.
          </p>
        </div>

        {/* Pill chọn con */}
        <div>
          <ChildSelectorPill />
        </div>
      </div>

      {/* Tab bar (Segmented controls Playful Geometric) */}
      <div
        style={{
          display: 'flex',
          gap: 8,
          backgroundColor: '#F1F5F9',
          padding: 6,
          borderRadius: 16,
          border: '2px solid #1E293B',
          boxShadow: '4px 4px 0 #1E293B',
          marginBottom: 24,
          flexWrap: 'wrap',
        }}
      >
        <button
          type="button"
          onClick={() => setActiveTab('list')}
          style={{
            flex: '1 1 auto',
            minWidth: 180,
            padding: '10px 16px',
            borderRadius: 12,
            border: activeTab === 'list' ? '2px solid #1E293B' : '2px solid transparent',
            backgroundColor: activeTab === 'list' ? '#FFFFFF' : 'transparent',
            boxShadow: activeTab === 'list' ? '2px 2px 0 #1E293B' : 'none',
            fontWeight: 700,
            fontSize: 14,
            cursor: 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 8,
            color: activeTab === 'list' ? '#1E293B' : '#64748B',
            transition: 'all 0.15s ease',
          }}
        >
          <History size={18} />
          Danh sách đơn đã gửi
          {requests.length > 0 && (
            <span
              style={{
                backgroundColor: '#6366F1',
                color: '#FFFFFF',
                borderRadius: 20,
                padding: '2px 8px',
                fontSize: 12,
                fontWeight: 800,
              }}
            >
              {requests.length}
            </span>
          )}
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('transfer')}
          style={{
            flex: '1 1 auto',
            minWidth: 180,
            padding: '10px 16px',
            borderRadius: 12,
            border: activeTab === 'transfer' ? '2px solid #1E293B' : '2px solid transparent',
            backgroundColor: activeTab === 'transfer' ? '#ECFDF5' : 'transparent',
            boxShadow: activeTab === 'transfer' ? '2px 2px 0 #1E293B' : 'none',
            fontWeight: 700,
            fontSize: 14,
            cursor: 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 8,
            color: activeTab === 'transfer' ? '#065F46' : '#64748B',
            transition: 'all 0.15s ease',
          }}
        >
          <GraduationCap size={18} />
          Xin Chuyển Lớp
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('document')}
          style={{
            flex: '1 1 auto',
            minWidth: 180,
            padding: '10px 16px',
            borderRadius: 12,
            border: activeTab === 'document' ? '2px solid #1E293B' : '2px solid transparent',
            backgroundColor: activeTab === 'document' ? '#FDF2F8' : 'transparent',
            boxShadow: activeTab === 'document' ? '2px 2px 0 #1E293B' : 'none',
            fontWeight: 700,
            fontSize: 14,
            cursor: 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: 8,
            color: activeTab === 'document' ? '#9D174D' : '#64748B',
            transition: 'all 0.15s ease',
          }}
        >
          <FileText size={18} />
          Xin Cấp Giấy Tờ / Chứng Nhận
        </button>
      </div>

      {/* ======================================================== */}
      {/* TAB 1: DANH SÁCH ĐƠN ĐÃ GỬI                              */}
      {/* ======================================================== */}
      {activeTab === 'list' && (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h2 style={{ fontSize: 18, fontWeight: 800, color: '#1E293B', margin: 0 }}>
              Tiến trình giải quyết hồ sơ ({requests.length})
            </h2>
            <button
              type="button"
              className="btn btn--outline"
              onClick={fetchMyRequests}
              disabled={loading}
              style={{ fontSize: 13, padding: '6px 12px', display: 'inline-flex', alignItems: 'center', gap: 6 }}
            >
              <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Làm mới
            </button>
          </div>

          {loading ? (
            <div
              style={{
                backgroundColor: '#FFFFFF',
                borderRadius: 16,
                border: '2px solid #1E293B',
                boxShadow: '4px 4px 0 #1E293B',
                padding: '48px 24px',
                textAlign: 'center',
                color: '#64748B',
                fontWeight: 600,
              }}
            >
              ⏳ Đang tải dữ liệu hồ sơ của con...
            </div>
          ) : requests.length === 0 ? (
            <div
              style={{
                backgroundColor: '#FFFFFF',
                borderRadius: 16,
                border: '2px dashed #94A3B8',
                padding: '48px 24px',
                textAlign: 'center',
              }}
            >
              <div style={{ fontSize: 44, marginBottom: 12 }}>📭</div>
              <h3 style={{ fontSize: 16, fontWeight: 700, color: '#1E293B', margin: '0 0 6px' }}>
                Chưa có yêu cầu hành chính nào
              </h3>
              <p style={{ fontSize: 14, color: '#64748B', maxWidth: 440, margin: '0 auto 20px' }}>
                Học sinh chưa có đơn xin chuyển lớp hoặc đơn xin cấp giấy tờ nào được ghi nhận. Phụ huynh có thể tạo đơn mới ngay phía trên!
              </p>
              <div style={{ display: 'flex', gap: 12, justifyContent: 'center' }}>
                <button
                  type="button"
                  className="btn btn--primary"
                  onClick={() => setActiveTab('transfer')}
                  style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
                >
                  <GraduationCap size={16} /> Tạo đơn chuyển lớp
                </button>
                <button
                  type="button"
                  className="btn btn--outline"
                  onClick={() => setActiveTab('document')}
                  style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}
                >
                  <FileText size={16} /> Xin cấp giấy tờ
                </button>
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              {requests.map((req) => {
                const isTransfer = req.requestType === 'ClassTransfer';

                return (
                  <div
                    key={req.id}
                    style={{
                      backgroundColor: '#FFFFFF',
                      borderRadius: 16,
                      border: '2px solid #1E293B',
                      boxShadow: '4px 4px 0 #1E293B',
                      padding: 20,
                      position: 'relative',
                    }}
                  >
                    {/* Header đơn */}
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'flex-start',
                        flexWrap: 'wrap',
                        gap: 12,
                        paddingBottom: 12,
                        borderBottom: '2px dashed #E2E8F0',
                        marginBottom: 14,
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                        <span
                          style={{
                            padding: '4px 10px',
                            borderRadius: 8,
                            fontSize: 12,
                            fontWeight: 800,
                            border: '1.5px solid #1E293B',
                            backgroundColor: isTransfer ? '#D1FAE5' : '#FCE7F3',
                            color: isTransfer ? '#065F46' : '#9D174D',
                          }}
                        >
                          {isTransfer ? 'Chuyển Lớp' : 'Cấp Giấy Tờ'}
                        </span>
                        <span style={{ fontWeight: 800, fontSize: 15, color: '#1E293B' }}>
                          Mã đơn: {req.requestNumber}
                        </span>
                        <span style={{ fontSize: 13, color: '#64748B' }}>
                          (Học sinh: <strong>{req.studentName}</strong>)
                        </span>
                      </div>

                      {/* Badge trạng thái */}
                      <div>
                        {req.requiresAmendment ? (
                          <span
                            style={{
                              backgroundColor: '#FEF3C7',
                              color: '#92400E',
                              border: '2px solid #F59E0B',
                              borderRadius: 20,
                              padding: '4px 12px',
                              fontSize: 12,
                              fontWeight: 800,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 6,
                            }}
                          >
                            <AlertCircle size={14} color="#D97706" />
                            CẦN BỔ SUNG HỒ SƠ
                          </span>
                        ) : req.status === 'Submitted' ? (
                          <span
                            style={{
                              backgroundColor: '#E0E7FF',
                              color: '#3730A3',
                              border: '1.5px solid #1E293B',
                              borderRadius: 20,
                              padding: '4px 12px',
                              fontSize: 12,
                              fontWeight: 700,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 6,
                            }}
                          >
                            <Clock size={14} /> Chờ giáo vụ tiếp nhận
                          </span>
                        ) : req.status === 'UnderReview' ? (
                          <span
                            style={{
                              backgroundColor: '#F3E8FF',
                              color: '#6B21A8',
                              border: '1.5px solid #1E293B',
                              borderRadius: 20,
                              padding: '4px 12px',
                              fontSize: 12,
                              fontWeight: 700,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 6,
                            }}
                          >
                            <Info size={14} /> Đang trình BGH xem xét
                          </span>
                        ) : req.status === 'Approved' ? (
                          <span
                            style={{
                              backgroundColor: '#DCFCE7',
                              color: '#15803D',
                              border: '1.5px solid #1E293B',
                              borderRadius: 20,
                              padding: '4px 12px',
                              fontSize: 12,
                              fontWeight: 800,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 6,
                            }}
                          >
                            <CheckCircle2 size={14} /> Đã phê duyệt hoàn tất
                          </span>
                        ) : (
                          <span
                            style={{
                              backgroundColor: '#FEE2E2',
                              color: '#991B1B',
                              border: '1.5px solid #1E293B',
                              borderRadius: 20,
                              padding: '4px 12px',
                              fontSize: 12,
                              fontWeight: 800,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 6,
                            }}
                          >
                            <XCircle size={14} /> Không được chấp thuận
                          </span>
                        )}
                      </div>
                    </div>

                    {/* Nội dung chi tiết đơn */}
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
                      {isTransfer && req.classTransfer && (
                        <div>
                          <div style={{ fontSize: 13, color: '#64748B', marginBottom: 4 }}>Lộ trình chuyển lớp:</div>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontWeight: 700, fontSize: 14 }}>
                            <span style={{ padding: '3px 8px', backgroundColor: '#F1F5F9', borderRadius: 6 }}>
                              Lớp hiện tại: {req.classTransfer.fromClassName}
                            </span>
                            <ArrowRight size={16} color="#64748B" />
                            <span style={{ padding: '3px 8px', backgroundColor: '#E0E7FF', color: '#4338CA', borderRadius: 6 }}>
                              Lớp xin chuyển: {req.classTransfer.toClassName}
                            </span>
                          </div>
                          <div style={{ marginTop: 8, fontSize: 13, color: '#334155' }}>
                            <strong>Lý do:</strong> {req.classTransfer.transferReason}
                          </div>
                        </div>
                      )}

                      {!isTransfer && req.documentRequest && (
                        <div>
                          <div style={{ fontSize: 13, color: '#64748B', marginBottom: 4 }}>Loại tài liệu yêu cầu:</div>
                          <div style={{ fontWeight: 800, fontSize: 14, color: '#0F172A' }}>
                            {req.documentRequest.documentType} ({req.documentRequest.numberOfCopies} bản)
                          </div>
                          <div style={{ marginTop: 6, fontSize: 13, color: '#334155' }}>
                            <strong>Mục đích:</strong> {req.documentRequest.purpose}
                          </div>
                        </div>
                      )}

                      <div>
                        <div style={{ fontSize: 13, color: '#64748B', marginBottom: 4 }}>Thông tin thời gian:</div>
                        <div style={{ fontSize: 13, color: '#334155' }}>
                          Ngày nộp: <strong>{new Date(req.submissionDate).toLocaleDateString('vi-VN')}</strong>
                        </div>
                        {req.processedDate && (
                          <div style={{ fontSize: 13, color: '#334155', marginTop: 4 }}>
                            Ngày xử lý: <strong>{new Date(req.processedDate).toLocaleDateString('vi-VN')}</strong>
                          </div>
                        )}
                        {req.attachmentUrl && (
                          <div style={{ marginTop: 6 }}>
                            <a
                              href={req.attachmentUrl}
                              target="_blank"
                              rel="noopener noreferrer"
                              style={{ fontSize: 12, color: '#2563EB', textDecoration: 'underline' }}
                            >
                              📎 Xem tệp minh chứng đính kèm
                            </a>
                          </div>
                        )}
                      </div>
                    </div>

                    {/* Hộp thông báo Bổ sung hồ sơ (Nếu có yêu cầu Amendment) */}
                    {req.requiresAmendment && (
                      <div
                        style={{
                          marginTop: 16,
                          backgroundColor: '#FFFBEB',
                          border: '2px solid #F59E0B',
                          borderRadius: 12,
                          padding: 14,
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          flexWrap: 'wrap',
                          gap: 12,
                        }}
                      >
                        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
                          <AlertCircle size={20} color="#D97706" style={{ flexShrink: 0, marginTop: 2 }} />
                          <div>
                            <div style={{ fontWeight: 800, color: '#92400E', fontSize: 13 }}>
                              GIÁO VỤ YÊU CẦU BỔ SUNG:
                            </div>
                            <div style={{ fontSize: 13, color: '#78350F', marginTop: 2 }}>
                              {req.amendmentNotes || 'Vui lòng bổ sung hồ sơ và gửi lại theo yêu cầu.'}
                            </div>
                          </div>
                        </div>
                        <button
                          type="button"
                          className="btn btn--primary"
                          onClick={() => {
                            setSelectedRequestForResubmit(req);
                            setResubmitOpen(true);
                          }}
                          style={{
                            fontSize: 13,
                            padding: '8px 14px',
                            backgroundColor: '#F59E0B',
                            borderColor: '#1E293B',
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 6,
                          }}
                        >
                          <Send size={15} /> Bổ sung hồ sơ & Gửi lại
                        </button>
                      </div>
                    )}

                    {/* Hộp kết quả Thành công (Chuyển lớp đã áp dụng hoặc Giấy tờ đã ký phát hành) */}
                    {req.status === 'Approved' && (
                      <div
                        style={{
                          marginTop: 16,
                          backgroundColor: '#F0FDF4',
                          border: '2px solid #22C55E',
                          borderRadius: 12,
                          padding: 14,
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          flexWrap: 'wrap',
                          gap: 12,
                        }}
                      >
                        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                          <CheckCircle2 size={22} color="#16A34A" />
                          <div>
                            <div style={{ fontWeight: 800, color: '#166534', fontSize: 13 }}>
                              {isTransfer ? 'CHÚC MỪNG: HỌC SINH ĐÃ ĐƯỢC CHUYỂN LỚP THÀNH CÔNG!' : 'VĂN BẢN ĐÃ ĐƯỢC KÝ DUYỆT & PHÁT HÀNH!'}
                            </div>
                            <div style={{ fontSize: 13, color: '#14532D', marginTop: 2 }}>
                              {isTransfer
                                ? `Học sinh đã chính thức được chuyển sang Lớp ${req.classTransfer?.toClassName}. Quý phụ huynh vui lòng theo dõi thời khóa biểu mới.`
                                : `Số hiệu: ${req.documentRequest?.certificateNumber || 'SAWP-OFFICIAL'} - ${req.documentRequest?.digitalSignatureInfo || 'Đã ký số'}`}
                            </div>
                          </div>
                        </div>

                        {!isTransfer && (
                          <button
                            type="button"
                            className="btn btn--primary"
                            onClick={() => handleDownload(req)}
                            style={{
                              fontSize: 13,
                              padding: '8px 14px',
                              backgroundColor: '#16A34A',
                              borderColor: '#1E293B',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 6,
                            }}
                          >
                            <Download size={15} /> Tải văn bản điện tử
                          </button>
                        )}
                      </div>
                    )}

                    {/* Hộp lý do Từ chối */}
                    {req.status === 'Rejected' && (
                      <div
                        style={{
                          marginTop: 16,
                          backgroundColor: '#FEF2F2',
                          border: '2px solid #EF4444',
                          borderRadius: 12,
                          padding: 14,
                        }}
                      >
                        <div style={{ fontWeight: 800, color: '#991B1B', fontSize: 13, marginBottom: 2 }}>
                          LÝ DO KHÔNG ĐƯỢC PHÊ DUYỆT:
                        </div>
                        <div style={{ fontSize: 13, color: '#7F1D1D' }}>
                          {req.schoolBoardNotes || req.reviewerNotes || 'Hồ sơ chưa đáp ứng tiêu chí xét duyệt của Ban Giám Hiệu.'}
                        </div>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* ======================================================== */}
      {/* TAB 2: TẠO ĐƠN XIN CHUYỂN LỚP                            */}
      {/* ======================================================== */}
      {activeTab === 'transfer' && (
        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: 16,
            border: '2px solid #1E293B',
            boxShadow: '4px 4px 0 #1E293B',
            padding: 28,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 18 }}>
            <span style={{ fontSize: 24 }}>🏫</span>
            <div>
              <h2 style={{ fontSize: 20, fontWeight: 800, color: '#1E293B', margin: 0 }}>
                Lập đơn xin chuyển lớp học
              </h2>
              <p style={{ margin: 0, color: '#64748B', fontSize: 13 }}>
                Đơn sẽ được bộ phận Giáo vụ kiểm tra sĩ số, điều kiện lớp học và trình Ban Giám Hiệu phê duyệt.
              </p>
            </div>
          </div>

          {/* Card học sinh đang chọn */}
          <div
            style={{
              backgroundColor: '#ECFDF5',
              border: '2px solid #059669',
              borderRadius: 12,
              padding: '12px 18px',
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              flexWrap: 'wrap',
              gap: 12,
              marginBottom: 20,
            }}
          >
            <div>
              <span style={{ fontSize: 12, fontWeight: 800, color: '#047857' }}>HỌC SINH ĐANG CHỌN:</span>
              <div style={{ fontWeight: 800, fontSize: 16, color: '#064E3B' }}>
                {activeChild?.fullName || 'Chưa chọn học sinh'} (Mã: {activeChild?.studentCode})
              </div>
            </div>
            <div style={{ textAlign: 'right' }}>
              <span style={{ fontSize: 12, fontWeight: 800, color: '#047857' }}>LỚP HIỆN TẠI:</span>
              <div style={{ fontWeight: 800, fontSize: 16, color: '#064E3B' }}>
                {activeChild?.currentClassName || 'Chưa phân lớp'}
              </div>
            </div>
          </div>

          <form onSubmit={handleSubmitTransfer} style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 14, marginBottom: 6 }}>
                Chọn lớp muốn chuyển tới: <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <select
                className="input"
                style={{ width: '100%', borderRadius: 10, padding: '10px 14px', fontSize: 14, fontWeight: 600 }}
                value={targetClassId}
                onChange={(e) => setTargetClassId(Number(e.target.value))}
                required
              >
                <option value="">-- Chọn lớp mục tiêu --</option>
                {availableTargetClasses.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.className} — Khối {c.grade} (Sức chứa tối đa: {c.capacity} học sinh)
                  </option>
                ))}
              </select>
              <span style={{ fontSize: 12, color: '#64748B', marginTop: 4, display: 'block' }}>
                💡 Hệ thống sẽ tự động rà soát sĩ số lớp đích để đảm bảo không vượt quá định mức cho phép.
              </span>
            </div>

            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 14, marginBottom: 6 }}>
                Lý do xin chuyển lớp: <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 110, borderRadius: 10, padding: '12px 14px' }}
                value={transferReason}
                onChange={(e) => setTransferReason(e.target.value)}
                placeholder="Vui lòng trình bày rõ lý do (ví dụ: thuận tiện đưa đón cùng anh/chị, chuyển đổi nơi cư trú, phù hợp định hướng phát triển của con...)"
                required
              />
            </div>

            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 14, marginBottom: 6 }}>
                Tệp minh chứng đính kèm (Liên kết ảnh/PDF nếu có):
              </label>
              <div style={{ position: 'relative' }}>
                <input
                  type="text"
                  className="input"
                  style={{ width: '100%', borderRadius: 10, padding: '10px 14px 10px 38px' }}
                  value={transferAttachment}
                  onChange={(e) => setTransferAttachment(e.target.value)}
                  placeholder="Dán đường dẫn ảnh giấy tờ minh chứng hoặc file Drive..."
                />
                <FileUp size={18} color="#64748B" style={{ position: 'absolute', left: 12, top: 12 }} />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 10 }}>
              <button
                type="button"
                className="btn btn--outline"
                onClick={() => setActiveTab('list')}
                disabled={submitting}
              >
                Quay lại
              </button>
              <button
                type="submit"
                className="btn btn--primary"
                disabled={submitting}
                style={{ display: 'inline-flex', alignItems: 'center', gap: 8, padding: '10px 24px' }}
              >
                <Send size={18} /> {submitting ? 'Đang gửi...' : 'Gửi đơn xin chuyển lớp'}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* ======================================================== */}
      {/* TAB 3: TẠO ĐƠN XIN CẤP GIẤY TỜ                           */}
      {/* ======================================================== */}
      {activeTab === 'document' && (
        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: 16,
            border: '2px solid #1E293B',
            boxShadow: '4px 4px 0 #1E293B',
            padding: 28,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 18 }}>
            <span style={{ fontSize: 24 }}>📑</span>
            <div>
              <h2 style={{ fontSize: 20, fontWeight: 800, color: '#1E293B', margin: 0 }}>
                Xin cấp giấy xác nhận / chứng nhận học sinh
              </h2>
              <p style={{ margin: 0, color: '#64748B', fontSize: 13 }}>
                Sau khi được Ban Giám Hiệu phê duyệt, văn bản sẽ được ký số điện tử và có thể tải trực tiếp về máy.
              </p>
            </div>
          </div>

          {/* Card học sinh */}
          <div
            style={{
              backgroundColor: '#FDF2F8',
              border: '2px solid #DB2777',
              borderRadius: 12,
              padding: '12px 18px',
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              flexWrap: 'wrap',
              gap: 12,
              marginBottom: 20,
            }}
          >
            <div>
              <span style={{ fontSize: 12, fontWeight: 800, color: '#9D174D' }}>HỌC SINH ĐANG CHỌN:</span>
              <div style={{ fontWeight: 800, fontSize: 16, color: '#831843' }}>
                {activeChild?.fullName || 'Chưa chọn học sinh'} (Mã: {activeChild?.studentCode})
              </div>
            </div>
            <div style={{ textAlign: 'right' }}>
              <span style={{ fontSize: 12, fontWeight: 800, color: '#9D174D' }}>LỚP HIỆN TẠI:</span>
              <div style={{ fontWeight: 800, fontSize: 16, color: '#831843' }}>
                {activeChild?.currentClassName || 'Chưa phân lớp'}
              </div>
            </div>
          </div>

          <form onSubmit={handleSubmitDocument} style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: 14, marginBottom: 6 }}>
                  Loại giấy tờ / chứng nhận cần cấp: <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <select
                  className="input"
                  style={{ width: '100%', borderRadius: 10, padding: '10px 14px', fontSize: 14, fontWeight: 600 }}
                  value={documentType}
                  onChange={(e) => setDocumentType(e.target.value)}
                  required
                >
                  <option value="Giấy xác nhận học sinh đang theo học">Giấy xác nhận học sinh đang theo học</option>
                  <option value="Bảng điểm rèn luyện & học tập">Bảng điểm rèn luyện & học tập</option>
                  <option value="Giấy giới thiệu chuyển trường">Giấy giới thiệu chuyển trường</option>
                  <option value="Giấy xác nhận hoàn thành chương trình">Giấy xác nhận hoàn thành chương trình</option>
                </select>
              </div>

              <div>
                <label style={{ display: 'block', fontWeight: 700, fontSize: 14, marginBottom: 6 }}>
                  Số lượng bản in: <span style={{ color: '#EF4444' }}>*</span>
                </label>
                <input
                  type="number"
                  min="1"
                  max="10"
                  className="input"
                  style={{ width: '100%', borderRadius: 10, padding: '10px 14px', fontSize: 14 }}
                  value={numberOfCopies}
                  onChange={(e) => setNumberOfCopies(Number(e.target.value))}
                  required
                />
              </div>
            </div>

            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 14, marginBottom: 6 }}>
                Mục đích sử dụng văn bản: <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <textarea
                className="input"
                style={{ width: '100%', minHeight: 90, borderRadius: 10, padding: '12px 14px' }}
                value={documentPurpose}
                onChange={(e) => setDocumentPurpose(e.target.value)}
                placeholder="Ví dụ: Bổ sung hồ sơ xin trợ cấp phúc lợi tại cơ quan phụ huynh, làm thẻ xe buýt học sinh..."
                required
              />
            </div>

            <div>
              <label style={{ display: 'block', fontWeight: 700, fontSize: 14, marginBottom: 6 }}>
                Tệp đính kèm liên quan (Ảnh/Tài liệu tham chiếu nếu có):
              </label>
              <div style={{ position: 'relative' }}>
                <input
                  type="text"
                  className="input"
                  style={{ width: '100%', borderRadius: 10, padding: '10px 14px 10px 38px' }}
                  value={documentAttachment}
                  onChange={(e) => setDocumentAttachment(e.target.value)}
                  placeholder="Dán đường dẫn ảnh giấy tờ hoặc tệp minh chứng..."
                />
                <FileUp size={18} color="#64748B" style={{ position: 'absolute', left: 12, top: 12 }} />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 10 }}>
              <button
                type="button"
                className="btn btn--outline"
                onClick={() => setActiveTab('list')}
                disabled={submitting}
              >
                Quay lại
              </button>
              <button
                type="submit"
                className="btn btn--primary"
                disabled={submitting}
                style={{ display: 'inline-flex', alignItems: 'center', gap: 8, padding: '10px 24px' }}
              >
                <Send size={18} /> {submitting ? 'Đang gửi...' : 'Gửi đơn xin cấp giấy tờ'}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Modal nộp lại hồ sơ khi có yêu cầu Amendment */}
      <ResubmitModal
        open={resubmitOpen}
        onClose={() => setResubmitOpen(false)}
        request={selectedRequestForResubmit}
        onSuccess={fetchMyRequests}
      />
    </div>
  );
}
