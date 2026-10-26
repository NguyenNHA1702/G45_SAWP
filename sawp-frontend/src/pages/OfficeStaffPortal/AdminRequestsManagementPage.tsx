import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Download,
  FileCheck,
  Info,
  RefreshCw,
  Search,
  UserCheck,
  XCircle,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import { StaffReviewTransferModal } from '../../components/requests/StaffReviewTransferModal';
import { StaffReviewDocumentModal } from '../../components/requests/StaffReviewDocumentModal';
import type { AdministrativeRequestSummary } from '../../types/request.types';

export default function AdminRequestsManagementPage() {
  const [requests, setRequests] = useState<AdministrativeRequestSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [executingId, setExecutingId] = useState<number | null>(null);

  // Filters
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('ALL');
  const [statusFilter, setStatusFilter] = useState<string>('ALL');

  // Modals
  const [selectedTransferReq, setSelectedTransferReq] = useState<AdministrativeRequestSummary | null>(null);
  const [transferModalOpen, setTransferModalOpen] = useState(false);

  const [selectedDocReq, setSelectedDocReq] = useState<AdministrativeRequestSummary | null>(null);
  const [docModalOpen, setDocModalOpen] = useState(false);

  const fetchRequests = useCallback(async () => {
    try {
      setLoading(true);
      const res = await administrativeRequestApi.getOfficeRequests();
      if (res.success && Array.isArray(res.data)) {
        setRequests(res.data);
      }
    } catch {
      toast.error('Không thể tải danh sách đơn hành chính.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchRequests();
  }, [fetchRequests]);

  // Thực thi chuyển lớp (Execute Apply Class Transfer)
  const handleExecuteTransfer = async (req: AdministrativeRequestSummary) => {
    if (!window.confirm(`Xác nhận thực thi chuyển lớp cho học sinh ${req.studentName} sang lớp ${req.classTransfer?.toClassName}? Hệ thống sẽ tự động gán lớp mới và lưu vết lịch sử.`)) {
      return;
    }

    try {
      setExecutingId(req.id);
      const res = await administrativeRequestApi.executeClassTransfer(req.id);
      if (res.success) {
        toast.success(res.message || 'Đã áp dụng chuyển lớp thành công cho học sinh!');
        fetchRequests();
      } else {
        toast.error(res.message || 'Không thể thực thi chuyển lớp');
      }
    } catch {
      toast.error('Đã xảy ra lỗi khi thực thi chuyển lớp.');
    } finally {
      setExecutingId(null);
    }
  };

  // Tải văn bản phát hành
  const handleDownloadDoc = async (req: AdministrativeRequestSummary) => {
    try {
      const fileName = `Giay_Xac_Nhan_${req.studentCode || 'HS'}_${req.requestNumber}.txt`;
      await administrativeRequestApi.downloadIssuedDocument(req.id, fileName);
      toast.success('Đã tải văn bản chính thức thành công!');
    } catch {
      toast.error('Không thể tải văn bản. Vui lòng thử lại sau.');
    }
  };

  // Thống kê nhanh
  const stats = useMemo(() => {
    const total = requests.length;
    const submitted = requests.filter((r) => r.status === 'Submitted' && !r.requiresAmendment).length;
    const amendment = requests.filter((r) => r.requiresAmendment).length;
    const underReview = requests.filter((r) => r.status === 'UnderReview').length;
    const pendingExecution = requests.filter(
      (r) => r.requestType === 'ClassTransfer' && r.schoolBoardApprovedAt && !r.classTransfer?.isExecuted
    ).length;
    const completed = requests.filter((r) => r.status === 'Approved' && (r.requestType !== 'ClassTransfer' || r.classTransfer?.isExecuted)).length;

    return { total, submitted, amendment, underReview, pendingExecution, completed };
  }, [requests]);

  // Lọc danh sách
  const filteredRequests = useMemo(() => {
    return requests.filter((r) => {
      // Search
      const search = searchTerm.toLowerCase().trim();
      const matchSearch =
        !search ||
        r.requestNumber.toLowerCase().includes(search) ||
        (r.studentName && r.studentName.toLowerCase().includes(search)) ||
        (r.studentCode && r.studentCode.toLowerCase().includes(search)) ||
        (r.requesterName && r.requesterName.toLowerCase().includes(search));

      // Type filter
      const matchType = typeFilter === 'ALL' || r.requestType === typeFilter;

      // Status filter
      let matchStatus = true;
      if (statusFilter === 'AMENDMENT') {
        matchStatus = r.requiresAmendment;
      } else if (statusFilter === 'PENDING_EXECUTION') {
        matchStatus = r.requestType === 'ClassTransfer' && !!r.schoolBoardApprovedAt && !r.classTransfer?.isExecuted;
      } else if (statusFilter !== 'ALL') {
        matchStatus = r.status === statusFilter && !r.requiresAmendment;
      }

      return matchSearch && matchType && matchStatus;
    });
  }, [requests, searchTerm, typeFilter, statusFilter]);

  return (
    <div style={{ maxWidth: 1200, margin: '0 auto', padding: '16px 20px 48px' }}>
      {/* Header bar */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 16,
          marginBottom: 20,
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
                backgroundColor: '#DCFCE7',
                color: '#16A34A',
                borderRadius: 12,
                border: '2px solid #1E293B',
                boxShadow: '2px 2px 0 #1E293B',
              }}
            >
              📑
            </span>
            Xử Lý Đơn Hành Chính (Giáo Vụ)
          </h1>
          <p style={{ margin: 0, color: '#64748B', fontSize: 14 }}>
            Thẩm định hồ sơ học sinh, kiểm tra sĩ số, soạn dự thảo văn bản và đề xuất Ban Giám Hiệu phê duyệt.
          </p>
        </div>

        <button
          type="button"
          className="btn btn--outline"
          onClick={fetchRequests}
          disabled={loading}
          style={{ display: 'inline-flex', alignItems: 'center', gap: 6, padding: '8px 16px' }}
        >
          <RefreshCw size={15} className={loading ? 'animate-spin' : ''} /> Tải lại dữ liệu
        </button>
      </div>

      {/* Stats Widgets */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))',
          gap: 12,
          marginBottom: 24,
        }}
      >
        <div
          style={{
            backgroundColor: '#FFFFFF',
            border: '2px solid #1E293B',
            borderRadius: 14,
            padding: 14,
            boxShadow: '3px 3px 0 #1E293B',
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 700, color: '#64748B' }}>TỔNG SỐ ĐƠN</div>
          <div style={{ fontSize: 24, fontWeight: 900, color: '#1E293B', marginTop: 2 }}>
            {stats.total}
          </div>
        </div>

        <div
          style={{
            backgroundColor: '#EFF6FF',
            border: '2px solid #1E293B',
            borderRadius: 14,
            padding: 14,
            boxShadow: '3px 3px 0 #1E293B',
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 700, color: '#1D4ED8' }}>CHỜ THẨM ĐỊNH</div>
          <div style={{ fontSize: 24, fontWeight: 900, color: '#1E40AF', marginTop: 2 }}>
            {stats.submitted}
          </div>
        </div>

        <div
          style={{
            backgroundColor: '#FEF3C7',
            border: '2px solid #1E293B',
            borderRadius: 14,
            padding: 14,
            boxShadow: '3px 3px 0 #1E293B',
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 700, color: '#B45309' }}>CẦN BỔ SUNG</div>
          <div style={{ fontSize: 24, fontWeight: 900, color: '#92400E', marginTop: 2 }}>
            {stats.amendment}
          </div>
        </div>

        <div
          style={{
            backgroundColor: '#F3E8FF',
            border: '2px solid #1E293B',
            borderRadius: 14,
            padding: 14,
            boxShadow: '3px 3px 0 #1E293B',
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 700, color: '#7E22CE' }}>ĐANG TRÌNH BGH</div>
          <div style={{ fontSize: 24, fontWeight: 900, color: '#6B21A8', marginTop: 2 }}>
            {stats.underReview}
          </div>
        </div>

        <div
          style={{
            backgroundColor: '#D1FAE5',
            border: '2px solid #1E293B',
            borderRadius: 14,
            padding: 14,
            boxShadow: '3px 3px 0 #1E293B',
          }}
        >
          <div style={{ fontSize: 12, fontWeight: 700, color: '#047857' }}>CHỜ THỰC THI CHUYỂN</div>
          <div style={{ fontSize: 24, fontWeight: 900, color: '#065F46', marginTop: 2 }}>
            {stats.pendingExecution}
          </div>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div
        style={{
          backgroundColor: '#FFFFFF',
          borderRadius: 16,
          border: '2px solid #1E293B',
          boxShadow: '4px 4px 0 #1E293B',
          padding: 16,
          marginBottom: 20,
          display: 'flex',
          flexWrap: 'wrap',
          gap: 12,
          alignItems: 'center',
        }}
      >
        {/* Search */}
        <div style={{ flex: '1 1 280px', position: 'relative' }}>
          <input
            type="text"
            className="input"
            style={{ width: '100%', borderRadius: 10, padding: '10px 12px 10px 38px', fontSize: 13 }}
            placeholder="Tìm theo tên học sinh, mã đơn, phụ huynh..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
          <Search size={16} color="#64748B" style={{ position: 'absolute', left: 12, top: 12 }} />
        </div>

        {/* Filter Type */}
        <div style={{ minWidth: 160 }}>
          <select
            className="input"
            style={{ width: '100%', borderRadius: 10, padding: '9px 12px', fontSize: 13, fontWeight: 600 }}
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
          >
            <option value="ALL">Tất cả loại đơn</option>
            <option value="ClassTransfer">Đơn xin chuyển lớp</option>
            <option value="Document">Đơn xin cấp giấy tờ</option>
          </select>
        </div>

        {/* Filter Status */}
        <div style={{ minWidth: 190 }}>
          <select
            className="input"
            style={{ width: '100%', borderRadius: 10, padding: '9px 12px', fontSize: 13, fontWeight: 600 }}
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
          >
            <option value="ALL">Tất cả trạng thái</option>
            <option value="Submitted">Chờ thẩm định</option>
            <option value="AMENDMENT">Yêu cầu bổ sung hồ sơ</option>
            <option value="UnderReview">Đang trình BGH duyệt</option>
            <option value="PENDING_EXECUTION">BGH đã duyệt (Chờ thực thi)</option>
            <option value="Approved">Đã hoàn tất</option>
            <option value="Rejected">Không phê duyệt</option>
          </select>
        </div>
      </div>

      {/* Requests Table */}
      <div
        style={{
          backgroundColor: '#FFFFFF',
          borderRadius: 16,
          border: '2px solid #1E293B',
          boxShadow: '4px 4px 0 #1E293B',
          overflow: 'hidden',
        }}
      >
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
            <thead>
              <tr style={{ backgroundColor: '#F8FAFC', borderBottom: '2px solid #1E293B' }}>
                <th style={{ padding: '12px 16px', fontSize: 13, fontWeight: 800, color: '#1E293B' }}>MÃ ĐƠN & NGÀY</th>
                <th style={{ padding: '12px 16px', fontSize: 13, fontWeight: 800, color: '#1E293B' }}>HỌC SINH & LỚP</th>
                <th style={{ padding: '12px 16px', fontSize: 13, fontWeight: 800, color: '#1E293B' }}>LOẠI ĐƠN & CHI TIẾT</th>
                <th style={{ padding: '12px 16px', fontSize: 13, fontWeight: 800, color: '#1E293B' }}>TRẠNG THÁI</th>
                <th style={{ padding: '12px 16px', fontSize: 13, fontWeight: 800, color: '#1E293B', textAlign: 'right' }}>THAO TÁC</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={5} style={{ padding: '36px', textAlign: 'center', color: '#64748B', fontWeight: 600 }}>
                    ⏳ Đang tải danh sách đơn hành chính...
                  </td>
                </tr>
              ) : filteredRequests.length === 0 ? (
                <tr>
                  <td colSpan={5} style={{ padding: '36px', textAlign: 'center', color: '#64748B' }}>
                    Không có đơn hành chính nào phù hợp với bộ lọc hiện tại.
                  </td>
                </tr>
              ) : (
                filteredRequests.map((req) => {
                  const isTransfer = req.requestType === 'ClassTransfer';
                  const isPendingExecution =
                    isTransfer && !!req.schoolBoardApprovedAt && !req.classTransfer?.isExecuted;

                  return (
                    <tr
                      key={req.id}
                      style={{
                        borderBottom: '1.5px solid #F1F5F9',
                        backgroundColor: isPendingExecution ? '#F0FDF4' : '#FFFFFF',
                      }}
                    >
                      {/* Cột 1: Mã đơn & Ngày */}
                      <td style={{ padding: '14px 16px', verticalAlign: 'top' }}>
                        <div style={{ fontWeight: 800, fontSize: 14, color: '#1E293B' }}>
                          {req.requestNumber}
                        </div>
                        <div style={{ fontSize: 12, color: '#64748B', marginTop: 2 }}>
                          {new Date(req.submissionDate).toLocaleDateString('vi-VN')}
                        </div>
                      </td>

                      {/* Cột 2: Học sinh & Lớp */}
                      <td style={{ padding: '14px 16px', verticalAlign: 'top' }}>
                        <div style={{ fontWeight: 800, fontSize: 14, color: '#0F172A' }}>
                          {req.studentName}
                        </div>
                        <div style={{ fontSize: 12, color: '#475569', marginTop: 2 }}>
                          Mã: <strong>{req.studentCode}</strong> | Lớp hiện tại: <strong>{req.currentClassName || 'Chưa xếp'}</strong>
                        </div>
                        <div style={{ fontSize: 12, color: '#64748B', marginTop: 2 }}>
                          PH: {req.requesterName}
                        </div>
                      </td>

                      {/* Cột 3: Loại đơn & Chi tiết */}
                      <td style={{ padding: '14px 16px', verticalAlign: 'top' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
                          <span
                            style={{
                              padding: '2px 8px',
                              borderRadius: 6,
                              fontSize: 11,
                              fontWeight: 800,
                              backgroundColor: isTransfer ? '#D1FAE5' : '#FCE7F3',
                              color: isTransfer ? '#065F46' : '#9D174D',
                              border: '1px solid #1E293B',
                            }}
                          >
                            {isTransfer ? 'Chuyển Lớp' : 'Cấp Giấy Tờ'}
                          </span>
                        </div>

                        {isTransfer && req.classTransfer && (
                          <div style={{ fontSize: 13, color: '#1E293B' }}>
                            Xin chuyển sang:{' '}
                            <strong style={{ color: '#047857' }}>{req.classTransfer.toClassName}</strong>
                            <div style={{ fontSize: 12, color: '#64748B', marginTop: 2 }}>
                              Lý do: "{req.classTransfer.transferReason}"
                            </div>
                          </div>
                        )}

                        {!isTransfer && req.documentRequest && (
                          <div style={{ fontSize: 13, color: '#1E293B' }}>
                            <strong>{req.documentRequest.documentType}</strong> ({req.documentRequest.numberOfCopies} bản)
                            <div style={{ fontSize: 12, color: '#64748B', marginTop: 2 }}>
                              Mục đích: "{req.documentRequest.purpose}"
                            </div>
                          </div>
                        )}
                      </td>

                      {/* Cột 4: Trạng thái */}
                      <td style={{ padding: '14px 16px', verticalAlign: 'top' }}>
                        {req.requiresAmendment ? (
                          <span
                            style={{
                              backgroundColor: '#FEF3C7',
                              color: '#92400E',
                              border: '1.5px solid #F59E0B',
                              borderRadius: 16,
                              padding: '3px 10px',
                              fontSize: 11,
                              fontWeight: 800,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                            }}
                          >
                            <AlertCircle size={13} color="#D97706" /> CẦN PHỤ HUYNH BỔ SUNG
                          </span>
                        ) : isPendingExecution ? (
                          <span
                            style={{
                              backgroundColor: '#DCFCE7',
                              color: '#15803D',
                              border: '1.5px solid #22C55E',
                              borderRadius: 16,
                              padding: '3px 10px',
                              fontSize: 11,
                              fontWeight: 800,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                            }}
                          >
                            <UserCheck size={13} /> BGH ĐÃ DUYỆT (CHỜ THỰC THI)
                          </span>
                        ) : req.status === 'Submitted' ? (
                          <span
                            style={{
                              backgroundColor: '#EFF6FF',
                              color: '#1E40AF',
                              border: '1.5px solid #3B82F6',
                              borderRadius: 16,
                              padding: '3px 10px',
                              fontSize: 11,
                              fontWeight: 700,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                            }}
                          >
                            <Clock size={13} /> Chờ thẩm định
                          </span>
                        ) : req.status === 'UnderReview' ? (
                          <span
                            style={{
                              backgroundColor: '#F3E8FF',
                              color: '#6B21A8',
                              border: '1.5px solid #A855F7',
                              borderRadius: 16,
                              padding: '3px 10px',
                              fontSize: 11,
                              fontWeight: 700,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                            }}
                          >
                            <Info size={13} /> Đang trình BGH duyệt
                          </span>
                        ) : req.status === 'Approved' ? (
                          <span
                            style={{
                              backgroundColor: '#DCFCE7',
                              color: '#166534',
                              border: '1.5px solid #22C55E',
                              borderRadius: 16,
                              padding: '3px 10px',
                              fontSize: 11,
                              fontWeight: 800,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                            }}
                          >
                            <CheckCircle2 size={13} /> Đã hoàn tất
                          </span>
                        ) : (
                          <span
                            style={{
                              backgroundColor: '#FEE2E2',
                              color: '#991B1B',
                              border: '1.5px solid #EF4444',
                              borderRadius: 16,
                              padding: '3px 10px',
                              fontSize: 11,
                              fontWeight: 800,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                            }}
                          >
                            <XCircle size={13} /> Không được duyệt
                          </span>
                        )}
                      </td>

                      {/* Cột 5: Hành động */}
                      <td style={{ padding: '14px 16px', verticalAlign: 'top', textAlign: 'right' }}>
                        {/* Nếu là đơn chuyển lớp đã được BGH duyệt -> Nút ÁP DỤNG CHUYỂN LỚP */}
                        {isPendingExecution ? (
                          <button
                            type="button"
                            className="btn btn--primary"
                            onClick={() => handleExecuteTransfer(req)}
                            disabled={executingId === req.id}
                            style={{
                              fontSize: 12,
                              padding: '6px 12px',
                              backgroundColor: '#16A34A',
                              borderColor: '#1E293B',
                              boxShadow: '2px 2px 0 #1E293B',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 5,
                            }}
                          >
                            <UserCheck size={14} />
                            {executingId === req.id ? 'Đang thực thi...' : 'Áp dụng chuyển lớp ngay'}
                          </button>
                        ) : req.requiresAmendment ? (
                          <button
                            type="button"
                            className="btn btn--outline"
                            onClick={() => {
                              if (isTransfer) {
                                setSelectedTransferReq(req);
                                setTransferModalOpen(true);
                              } else {
                                setSelectedDocReq(req);
                                setDocModalOpen(true);
                              }
                            }}
                            style={{
                              fontSize: 12,
                              padding: '6px 12px',
                              borderColor: '#F59E0B',
                              backgroundColor: '#FFFBEB',
                              color: '#B45309',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 5,
                            }}
                          >
                            <AlertCircle size={14} color="#D97706" /> Chờ bổ sung
                          </button>
                        ) : req.status === 'Submitted' ? (
                          <button
                            type="button"
                            className="btn btn--primary"
                            onClick={() => {
                              if (isTransfer) {
                                setSelectedTransferReq(req);
                                setTransferModalOpen(true);
                              } else {
                                setSelectedDocReq(req);
                                setDocModalOpen(true);
                              }
                            }}
                            style={{
                              fontSize: 12,
                              padding: '6px 12px',
                              borderColor: '#1E293B',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 5,
                            }}
                          >
                            <FileCheck size={14} /> Thẩm định hồ sơ
                          </button>
                        ) : req.status === 'Approved' && !isTransfer ? (
                          <button
                            type="button"
                            className="btn btn--outline"
                            onClick={() => handleDownloadDoc(req)}
                            style={{
                              fontSize: 12,
                              padding: '6px 12px',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 5,
                            }}
                          >
                            <Download size={14} /> Tải văn bản
                          </button>
                        ) : req.status === 'UnderReview' ? (
                          <button
                            type="button"
                            className="btn btn--outline"
                            onClick={() => {
                              if (isTransfer) {
                                setSelectedTransferReq(req);
                                setTransferModalOpen(true);
                              } else {
                                setSelectedDocReq(req);
                                setDocModalOpen(true);
                              }
                            }}
                            style={{ fontSize: 12, padding: '5px 10px', color: '#6B21A8' }}
                          >
                            Xem đề xuất
                          </button>
                        ) : (
                          <span style={{ fontSize: 12, color: '#94A3B8' }}>Đã đóng hồ sơ</span>
                        )}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal thẩm định Chuyển lớp */}
      <StaffReviewTransferModal
        open={transferModalOpen}
        onClose={() => setTransferModalOpen(false)}
        request={selectedTransferReq}
        onSuccess={fetchRequests}
      />

      {/* Modal thẩm định Giấy tờ */}
      <StaffReviewDocumentModal
        open={docModalOpen}
        onClose={() => setDocModalOpen(false)}
        request={selectedDocReq}
        onSuccess={fetchRequests}
      />
    </div>
  );
}
