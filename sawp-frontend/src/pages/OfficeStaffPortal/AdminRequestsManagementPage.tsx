import { useState, useEffect, useCallback } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Clock,
  Download,
  FileCheck,
  History,
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
import { RequestHistoryModal } from '../../components/requests/RequestHistoryModal';
import { PaginationBar } from '../../components/common/PaginationBar';
import { openProtectedFile } from '../../utils/fileViewer';
import { formatDDMMYYYY, formatDDMMYYYYHHMM } from '../../utils/date';
import type { AdministrativeRequestStats, AdministrativeRequestSummary } from '../../types/request.types';

export default function AdminRequestsManagementPage() {
  const [requests, setRequests] = useState<AdministrativeRequestSummary[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(15);
  const [serverStats, setServerStats] = useState<AdministrativeRequestStats | null>(null);
  const [loading, setLoading] = useState(false);
  const [executingId, setExecutingId] = useState<number | null>(null);

  // Filters
  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('ALL');
  const [statusFilter, setStatusFilter] = useState<string>('ALL');

  // Modals
  const [selectedTransferReq, setSelectedTransferReq] = useState<AdministrativeRequestSummary | null>(null);
  const [transferModalOpen, setTransferModalOpen] = useState(false);

  const [selectedDocReq, setSelectedDocReq] = useState<AdministrativeRequestSummary | null>(null);
  const [docModalOpen, setDocModalOpen] = useState(false);

  const [selectedHistoryRequest, setSelectedHistoryRequest] = useState<{ id: number; requestNumber: string } | null>(null);
  const [historyModalOpen, setHistoryModalOpen] = useState(false);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setDebouncedSearch(searchTerm);
      setPageIndex(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchTerm]);

  const fetchRequests = useCallback(async () => {
    try {
      setLoading(true);
      const res = await administrativeRequestApi.getOfficeRequestsPaged({
        requestType: typeFilter,
        status: statusFilter,
        search: debouncedSearch || undefined,
        pageIndex,
        pageSize,
      });
      if (res.success && res.data) {
        setRequests(res.data.items);
        setTotalCount(res.data.totalCount);
        setServerStats(res.data.stats);
      }
    } catch {
      toast.error('Không thể tải danh sách đơn hành chính.');
    } finally {
      setLoading(false);
    }
  }, [typeFilter, statusFilter, debouncedSearch, pageIndex, pageSize]);

  useEffect(() => {
    fetchRequests();
  }, [fetchRequests]);

  // Thực thi chuyển lớp (Execute Apply Class Transfer)
  const handleExecuteTransfer = async (req: AdministrativeRequestSummary) => {
    const isFuture = req.classTransfer?.effectiveDate && new Date(req.classTransfer.effectiveDate).getTime() > new Date().setHours(23, 59, 59, 999);
    const effDateStr = req.classTransfer?.effectiveDate ? new Date(req.classTransfer.effectiveDate).toLocaleDateString('vi-VN') : '';

    let confirmMsg = `Xác nhận thực thi chuyển lớp cho học sinh ${req.studentName} sang lớp ${req.classTransfer?.toClassName}? Hệ thống sẽ tự động gán lớp mới và hoàn tất hồ sơ.`;
    if (isFuture) {
      confirmMsg = `⚠️ LƯU Ý NGÀY HIỆU LỰC:\nĐơn này có ngày hiệu lực mong muốn là ngày ${effDateStr} (Chưa đến hạn).\n\nBạn có chắc chắn muốn thực thi điều chuyển SỚM ngay hôm nay không?`;
    }

    if (!window.confirm(confirmMsg)) {
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
    } catch (err: any) {
      const errorMsg =
        err?.response?.data?.message ||
        err?.response?.data?.errors?.[0] ||
        err?.message ||
        'Đã xảy ra lỗi khi thực thi chuyển lớp.';
      toast.error(errorMsg);
    } finally {
      setExecutingId(null);
    }
  };

  // Tải văn bản phát hành
  const handleDownloadDoc = async (req: AdministrativeRequestSummary) => {
    const issuedUrl = req.documentRequest?.issuedDocumentUrl;
    if (!issuedUrl) {
      toast.error('Đơn này chưa có tệp văn bản đính kèm.');
      return;
    }
    if (!issuedUrl.includes('/download-document')) {
      await openProtectedFile(issuedUrl, true);
      return;
    }
    try {
      const fileName = `Giay_Xac_Nhan_${req.studentCode || 'HS'}_${req.requestNumber}.doc`;
      await administrativeRequestApi.downloadIssuedDocument(req.id, fileName);
      toast.success('Đã tải văn bản chính thức thành công!');
    } catch {
      toast.error('Không thể tải văn bản. Vui lòng thử lại sau.');
    }
  };

  // Thống kê nhanh (server-aggregated across all pages)
  const stats = {
    total: serverStats?.totalCount ?? totalCount,
    submitted: serverStats?.submittedCount ?? 0,
    amendment: serverStats?.needMoreInfoCount ?? 0,
    underReview: serverStats?.underReviewCount ?? 0,
    pendingExecution: serverStats?.pendingExecutionCount ?? 0,
    completed: (serverStats?.completedCount ?? 0) + Math.max(0, (serverStats?.approvedCount ?? 0) - (serverStats?.pendingExecutionCount ?? 0)),
  };

  const filteredRequests = requests;

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
            <option value="Other">Loại đơn khác</option>
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
            <option value="ReturnedToStaff">BGH yêu cầu trình lại</option>
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
                  const isOther = req.requestType === 'Other';
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
                      <td style={{ padding: '14px 16px', verticalAlign: 'top', whiteSpace: 'nowrap' }}>
                        <div style={{ fontWeight: 800, fontSize: 14, color: '#1E293B' }}>
                          {req.requestNumber}
                        </div>
                        <div style={{ fontSize: 12, color: '#64748B', marginTop: 2 }}>
                          {formatDDMMYYYYHHMM(req.submissionDate)}
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
                              backgroundColor: isTransfer ? '#D1FAE5' : isOther ? '#FEF3C7' : '#FCE7F3',
                              color: isTransfer ? '#065F46' : isOther ? '#92400E' : '#9D174D',
                              border: '1px solid #1E293B',
                              whiteSpace: 'nowrap',
                            }}
                          >
                            {isTransfer ? 'Chuyển Lớp' : isOther ? 'Loại Đơn Khác' : 'Cấp Giấy Tờ'}
                          </span>
                        </div>

                        {isTransfer && req.classTransfer && (
                          <div style={{ fontSize: 13, color: '#1E293B' }}>
                            Xin chuyển sang:{' '}
                            <strong style={{ color: '#047857' }}>{req.classTransfer.toClassName}</strong>
                            {req.classTransfer.effectiveDate && (
                              <div style={{ fontSize: 12, color: '#B45309', marginTop: 2, fontWeight: 700 }}>
                                📅 Hiệu lực: {formatDDMMYYYY(req.classTransfer.effectiveDate)}
                              </div>
                            )}
                            <div style={{ fontSize: 12, color: '#64748B', marginTop: 2 }}>
                              Lý do: "{req.classTransfer.transferReason}"
                            </div>
                          </div>
                        )}

                        {!isTransfer && req.documentRequest && (
                          <div style={{ fontSize: 13, color: '#1E293B' }}>
                            <strong>{req.documentRequest.documentType}</strong>
                            {!isOther && ` (${req.documentRequest.numberOfCopies} bản)`}
                            <div style={{ fontSize: 12, color: '#64748B', marginTop: 2 }}>
                              {isOther ? 'Nội dung:' : 'Mục đích:'} "{req.documentRequest.purpose}"
                            </div>
                          </div>
                        )}
                      </td>

                      {/* Cột 4: Trạng thái */}
                      <td style={{ padding: '14px 16px', verticalAlign: 'top', whiteSpace: 'nowrap' }}>
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
                              whiteSpace: 'nowrap',
                            }}
                          >
                            <AlertCircle size={13} color="#D97706" /> Cần PH bổ sung
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
                              whiteSpace: 'nowrap',
                            }}
                          >
                            <UserCheck size={13} /> BGH đã duyệt (Chờ thực thi)
                          </span>
                        ) : req.status === 'ReturnedToStaff' || req.status === 'NeedMoreInfo' ? (
                          <span
                            style={{
                              backgroundColor: '#FFF7ED',
                              color: '#9A3412',
                              border: '1.5px solid #EA580C',
                              borderRadius: 16,
                              padding: '3px 10px',
                              fontSize: 11,
                              fontWeight: 800,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                              whiteSpace: 'nowrap',
                            }}
                          >
                            <AlertCircle size={13} color="#EA580C" /> BGH yêu cầu trình lại
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
                        ) : req.status === 'Completed' || (req.status === 'Approved' && (req.requestType !== 'ClassTransfer' || req.classTransfer?.isExecuted)) ? (
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
                            <CheckCircle2 size={13} />{' '}
                            {req.requestType === 'ClassTransfer'
                              ? 'Đã chuyển lớp'
                              : isOther
                                ? 'Đã giải quyết'
                                : 'Đã phát hành'}
                          </span>
                        ) : req.status === 'Approved' ? (
                          <span
                            style={{
                              backgroundColor: '#E0E7FF',
                              color: '#3730A3',
                              border: '1.5px solid #6366F1',
                              borderRadius: 16,
                              padding: '3px 10px',
                              fontSize: 11,
                              fontWeight: 800,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                            }}
                          >
                            <CheckCircle2 size={13} /> BGH đã duyệt
                          </span>
                        ) : req.status === 'Cancelled' ? (
                          <span
                            style={{
                              backgroundColor: '#F1F5F9',
                              color: '#475569',
                              border: '1.5px solid #94A3B8',
                              borderRadius: 16,
                              padding: '3px 10px',
                              fontSize: 11,
                              fontWeight: 700,
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                            }}
                          >
                            <XCircle size={13} /> Đã hủy đơn
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
                      <td style={{ padding: '14px 16px', verticalAlign: 'top', textAlign: 'right', whiteSpace: 'nowrap' }}>
                        <div style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'flex-end', gap: 8, flexWrap: 'nowrap' }}>
                          <button
                            type="button"
                            onClick={() => {
                              setSelectedHistoryRequest({ id: req.id, requestNumber: req.requestNumber });
                              setHistoryModalOpen(true);
                            }}
                            style={{
                              fontSize: 12,
                              fontWeight: 600,
                              padding: '6px 10px',
                              borderRadius: 8,
                              border: '1px solid #CBD5E1',
                              backgroundColor: '#FFFFFF',
                              color: '#334155',
                              cursor: 'pointer',
                              display: 'inline-flex',
                              alignItems: 'center',
                              gap: 4,
                              whiteSpace: 'nowrap',
                            }}
                            title="Xem toàn bộ lịch sử tiến trình xử lý đơn này"
                          >
                            <History size={13} /> Lịch sử
                          </button>

                          {/* Nếu là đơn chuyển lớp đã được BGH duyệt -> Nút ÁP DỤNG CHUYỂN LỚP */}
                          {isPendingExecution ? (
                            <button
                              type="button"
                              className="btn btn--primary"
                              onClick={() => handleExecuteTransfer(req)}
                              disabled={executingId === req.id}
                              style={{
                                fontSize: 12,
                                fontWeight: 700,
                                padding: '6px 11px',
                                borderRadius: 8,
                                backgroundColor: req.classTransfer?.effectiveDate && new Date(req.classTransfer.effectiveDate).getTime() > new Date().setHours(23, 59, 59, 999) ? '#D97706' : '#16A34A',
                                borderColor: '#15803D',
                                boxShadow: 'none',
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: 5,
                                whiteSpace: 'nowrap',
                              }}
                            >
                              <UserCheck size={13} />
                              {executingId === req.id
                                ? 'Đang thực thi...'
                                : req.classTransfer?.effectiveDate && new Date(req.classTransfer.effectiveDate).getTime() > new Date().setHours(23, 59, 59, 999)
                                ? `Chờ hiệu lực (${formatDDMMYYYY(req.classTransfer.effectiveDate)})`
                                : 'Áp dụng chuyển lớp'}
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
                                fontWeight: 700,
                                padding: '6px 11px',
                                borderRadius: 8,
                                borderColor: '#F59E0B',
                                backgroundColor: '#FFFBEB',
                                color: '#B45309',
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: 5,
                                whiteSpace: 'nowrap',
                              }}
                            >
                              <AlertCircle size={13} color="#D97706" /> Chờ bổ sung
                            </button>
                          ) : req.status === 'Submitted' || req.status === 'ReturnedToStaff' || req.status === 'NeedMoreInfo' ? (
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
                                fontWeight: 700,
                                padding: '6px 11px',
                                borderRadius: 8,
                                backgroundColor: req.status === 'ReturnedToStaff' || req.status === 'NeedMoreInfo' ? '#EA580C' : undefined,
                                borderColor: req.status === 'ReturnedToStaff' || req.status === 'NeedMoreInfo' ? '#C2410C' : '#1E293B',
                                boxShadow: 'none',
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: 5,
                                whiteSpace: 'nowrap',
                              }}
                            >
                              <FileCheck size={13} /> {req.status === 'ReturnedToStaff' || req.status === 'NeedMoreInfo' ? 'Sửa & Trình lại' : 'Thẩm định hồ sơ'}
                            </button>
                          ) : (req.status === 'Approved' || req.status === 'Completed') && !isTransfer && !!req.documentRequest?.issuedDocumentUrl ? (
                            <button
                              type="button"
                              className="btn btn--outline"
                              onClick={() => handleDownloadDoc(req)}
                              style={{
                                fontSize: 12,
                                fontWeight: 700,
                                padding: '6px 11px',
                                borderRadius: 8,
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: 5,
                                whiteSpace: 'nowrap',
                              }}
                            >
                              <Download size={13} /> Tải văn bản
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
                              style={{ fontSize: 12, fontWeight: 600, padding: '6px 11px', borderRadius: 8, color: '#6B21A8', whiteSpace: 'nowrap' }}
                            >
                              Xem đề xuất
                            </button>
                          ) : (
                            <span style={{ fontSize: 12, color: '#94A3B8', whiteSpace: 'nowrap' }}>Đã đóng hồ sơ</span>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      <PaginationBar
        pageIndex={pageIndex}
        pageSize={pageSize}
        totalCount={totalCount}
        onPageChange={setPageIndex}
        onPageSizeChange={(s) => {
          setPageSize(s);
          setPageIndex(1);
        }}
        itemLabel="đơn hành chính"
      />

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

      {/* Modal xem lịch sử tiến trình đơn (BR-35 / UC-43) */}
      <RequestHistoryModal
        isOpen={historyModalOpen}
        onClose={() => setHistoryModalOpen(false)}
        requestId={selectedHistoryRequest?.id ?? 0}
        requestNumber={selectedHistoryRequest?.requestNumber ?? ''}
      />
    </div>
  );
}
