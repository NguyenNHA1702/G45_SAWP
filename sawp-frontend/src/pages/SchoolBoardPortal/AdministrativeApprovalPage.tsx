import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  ArrowRight,
  CheckCircle2,
  Clock,
  Download,
  Eye,
  RefreshCw,
  Search,
  ShieldCheck,
  Users,
  XCircle,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { administrativeRequestApi } from '../../services/implementations/administrativeRequestApi';
import { BoardDecisionModal } from '../../components/requests/BoardDecisionModal';
import type { AdministrativeRequestSummary } from '../../types/request.types';

export default function AdministrativeApprovalPage() {
  const [requests, setRequests] = useState<AdministrativeRequestSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('ALL');
  const [statusFilter, setStatusFilter] = useState<string>('UnderReview');

  const [selectedReq, setSelectedReq] = useState<AdministrativeRequestSummary | null>(null);
  const [decisionModalOpen, setDecisionModalOpen] = useState(false);

  const fetchRequests = useCallback(async () => {
    try {
      setLoading(true);
      // Fetch all board-relevant requests so statistics and quick tab switching stay accurate
      const res = await administrativeRequestApi.getBoardRequests(undefined, 'ALL');
      if (res.success && Array.isArray(res.data)) {
        setRequests(res.data);
      }
    } catch {
      toast.error('Không thể tải danh sách đề xuất trình Ban Giám Hiệu.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchRequests();
  }, [fetchRequests]);

  // Thống kê nhanh
  const stats = useMemo(() => {
    const total = requests.length;
    const pending = requests.filter((r) => r.status === 'UnderReview').length;
    const approved = requests.filter(
      (r) => r.schoolBoardApprovedAt != null || r.status === 'Approved'
    ).length;
    const rejected = requests.filter((r) => r.status === 'Rejected').length;
    const transferCount = requests.filter((r) => r.requestType === 'ClassTransfer').length;
    const documentCount = requests.filter((r) => r.requestType === 'Document').length;
    return { total, pending, approved, rejected, transferCount, documentCount };
  }, [requests]);

  // Tải văn bản phát hành (nếu có)
  const handleDownloadDoc = async (req: AdministrativeRequestSummary) => {
    try {
      const fileName = `Giay_Xac_Nhan_${req.studentCode || 'HS'}_${req.requestNumber}.doc`;
      await administrativeRequestApi.downloadIssuedDocument(req.id, fileName);
      toast.success('Đã tải văn bản chính thức thành công!');
    } catch {
      toast.error('Không thể tải văn bản. Vui lòng thử lại sau.');
    }
  };

  // Lọc danh sách theo search, typeFilter, statusFilter
  const filteredRequests = useMemo(() => {
    return requests.filter((r) => {
      const search = searchTerm.toLowerCase().trim();
      const matchSearch =
        !search ||
        r.requestNumber.toLowerCase().includes(search) ||
        (r.studentName && r.studentName.toLowerCase().includes(search)) ||
        (r.studentCode && r.studentCode.toLowerCase().includes(search)) ||
        (r.requesterName && r.requesterName.toLowerCase().includes(search));

      const matchType = typeFilter === 'ALL' || r.requestType === typeFilter;

      let matchStatus = true;
      if (statusFilter === 'UnderReview') {
        matchStatus = r.status === 'UnderReview';
      } else if (statusFilter === 'Approved') {
        matchStatus = r.schoolBoardApprovedAt != null || r.status === 'Approved';
      } else if (statusFilter === 'Rejected') {
        matchStatus = r.status === 'Rejected';
      }

      return matchSearch && matchType && matchStatus;
    });
  }, [requests, searchTerm, typeFilter, statusFilter]);

  return (
    <div style={{ maxWidth: 1200, margin: '0 auto', padding: '16px 20px 48px' }}>
      {/* Header */}
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
                backgroundColor: '#F3E8FF',
                color: '#7E22CE',
                borderRadius: 12,
                border: '2px solid #1E293B',
                boxShadow: '2px 2px 0 #1E293B',
              }}
            >
              🏛️
            </span>
            Phê Duyệt Đơn Hành Chính (Ban Giám Hiệu)
          </h1>
          <p style={{ margin: 0, color: '#64748B', fontSize: 14 }}>
            Xem xét các đề xuất chuyển lớp và cấp văn bản xác nhận từ bộ phận Giáo vụ; theo dõi lịch sử các đơn đã được duyệt hoặc từ chối.
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
          gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
          gap: 16,
          marginBottom: 20,
        }}
      >
        <div
          onClick={() => setStatusFilter('UnderReview')}
          style={{
            backgroundColor: statusFilter === 'UnderReview' ? '#F3E8FF' : '#FFFFFF',
            border: statusFilter === 'UnderReview' ? '2px solid #7E22CE' : '2px solid #1E293B',
            borderRadius: 14,
            padding: 16,
            boxShadow: '3px 3px 0 #1E293B',
            display: 'flex',
            alignItems: 'center',
            gap: 14,
            cursor: 'pointer',
            transition: 'all 0.15s ease',
          }}
        >
          <div
            style={{
              width: 44,
              height: 44,
              borderRadius: 12,
              backgroundColor: '#F3E8FF',
              color: '#6B21A8',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              border: '2px solid #1E293B',
            }}
          >
            <Clock size={22} />
          </div>
          <div>
            <div style={{ fontSize: 12, fontWeight: 700, color: '#64748B' }}>ĐƠN CHỜ BGH DUYỆT</div>
            <div style={{ fontSize: 24, fontWeight: 900, color: '#1E293B' }}>{stats.pending}</div>
          </div>
        </div>

        <div
          onClick={() => setStatusFilter('Approved')}
          style={{
            backgroundColor: statusFilter === 'Approved' ? '#D1FAE5' : '#ECFDF5',
            border: statusFilter === 'Approved' ? '2px solid #059669' : '2px solid #1E293B',
            borderRadius: 14,
            padding: 16,
            boxShadow: '3px 3px 0 #1E293B',
            display: 'flex',
            alignItems: 'center',
            gap: 14,
            cursor: 'pointer',
            transition: 'all 0.15s ease',
          }}
        >
          <div
            style={{
              width: 44,
              height: 44,
              borderRadius: 12,
              backgroundColor: '#D1FAE5',
              color: '#065F46',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              border: '2px solid #1E293B',
            }}
          >
            <CheckCircle2 size={22} />
          </div>
          <div>
            <div style={{ fontSize: 12, fontWeight: 700, color: '#047857' }}>ĐÃ PHÊ DUYỆT</div>
            <div style={{ fontSize: 24, fontWeight: 900, color: '#065F46' }}>{stats.approved}</div>
          </div>
        </div>

        <div
          onClick={() => setStatusFilter('Rejected')}
          style={{
            backgroundColor: statusFilter === 'Rejected' ? '#FEE2E2' : '#FEF2F2',
            border: statusFilter === 'Rejected' ? '2px solid #DC2626' : '2px solid #1E293B',
            borderRadius: 14,
            padding: 16,
            boxShadow: '3px 3px 0 #1E293B',
            display: 'flex',
            alignItems: 'center',
            gap: 14,
            cursor: 'pointer',
            transition: 'all 0.15s ease',
          }}
        >
          <div
            style={{
              width: 44,
              height: 44,
              borderRadius: 12,
              backgroundColor: '#FEE2E2',
              color: '#B91C1C',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              border: '2px solid #1E293B',
            }}
          >
            <XCircle size={22} />
          </div>
          <div>
            <div style={{ fontSize: 12, fontWeight: 700, color: '#B91C1C' }}>ĐÃ TỪ CHỐI</div>
            <div style={{ fontSize: 24, fontWeight: 900, color: '#991B1B' }}>{stats.rejected}</div>
          </div>
        </div>

        <div
          onClick={() => setStatusFilter('ALL')}
          style={{
            backgroundColor: statusFilter === 'ALL' ? '#E2E8F0' : '#FFFFFF',
            border: statusFilter === 'ALL' ? '2px solid #475569' : '2px solid #1E293B',
            borderRadius: 14,
            padding: 16,
            boxShadow: '3px 3px 0 #1E293B',
            display: 'flex',
            alignItems: 'center',
            gap: 14,
            cursor: 'pointer',
            transition: 'all 0.15s ease',
          }}
        >
          <div
            style={{
              width: 44,
              height: 44,
              borderRadius: 12,
              backgroundColor: '#E2E8F0',
              color: '#334155',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              border: '2px solid #1E293B',
            }}
          >
            <ShieldCheck size={22} />
          </div>
          <div>
            <div style={{ fontSize: 12, fontWeight: 700, color: '#475569' }}>TỔNG SỐ HỒ SƠ</div>
            <div style={{ fontSize: 24, fontWeight: 900, color: '#1E293B' }}>{stats.total}</div>
          </div>
        </div>
      </div>

      {/* Filter Tabs as Playful Pills */}
      <div style={{ display: 'flex', gap: 10, marginBottom: 18, flexWrap: 'wrap' }}>
        {[
          { key: 'UnderReview', label: 'Chờ BGH duyệt', count: stats.pending },
          { key: 'Approved', label: 'Đã phê duyệt', count: stats.approved },
          { key: 'Rejected', label: 'Đã từ chối', count: stats.rejected },
          { key: 'ALL', label: 'Tất cả hồ sơ', count: stats.total },
        ].map((tab) => {
          const isActive = statusFilter === tab.key;
          return (
            <button
              key={tab.key}
              type="button"
              className={isActive ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
              onClick={() => setStatusFilter(tab.key)}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                fontWeight: isActive ? 800 : 600,
                padding: '6px 14px',
              }}
            >
              <span>{tab.label}</span>
              <span
                style={{
                  fontSize: 11,
                  padding: '2px 7px',
                  borderRadius: 10,
                  backgroundColor: isActive ? 'rgba(255,255,255,0.3)' : 'var(--color-muted, #E2E8F0)',
                  color: isActive ? '#FFFFFF' : '#1E293B',
                  fontWeight: 800,
                }}
              >
                {tab.count}
              </span>
            </button>
          );
        })}
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

        <div style={{ minWidth: 200 }}>
          <select
            className="input"
            style={{ width: '100%', borderRadius: 10, padding: '9px 12px', fontSize: 13, fontWeight: 600 }}
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
          >
            <option value="ALL">Tất cả loại đề xuất</option>
            <option value="ClassTransfer">Đề xuất Chuyển lớp</option>
            <option value="Document">Đề xuất Cấp giấy tờ</option>
          </select>
        </div>
      </div>

      {/* Requests List */}
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
          ⏳ Đang tải danh sách hồ sơ trình Ban Giám Hiệu...
        </div>
      ) : filteredRequests.length === 0 ? (
        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: 16,
            border: '2px dashed #94A3B8',
            padding: '48px 24px',
            textAlign: 'center',
          }}
        >
          <div style={{ fontSize: 44, marginBottom: 12 }}>✨</div>
          <h3 style={{ fontSize: 18, fontWeight: 800, color: '#1E293B', margin: '0 0 6px' }}>
            {statusFilter === 'Approved'
              ? 'Chưa có hồ sơ nào đã phê duyệt'
              : statusFilter === 'Rejected'
              ? 'Không có hồ sơ nào bị từ chối'
              : statusFilter === 'UnderReview'
              ? 'Không có hồ sơ nào đang chờ Ban Giám Hiệu phê duyệt'
              : 'Không tìm thấy hồ sơ nào phù hợp'}
          </h3>
          <p style={{ fontSize: 14, color: '#64748B', maxWidth: 440, margin: '0 auto' }}>
            {statusFilter === 'UnderReview'
              ? 'Tất cả các đề xuất từ bộ phận Giáo vụ đã được xem xét và ra quyết định hoàn tất.'
              : 'Hãy thay đổi bộ lọc tìm kiếm hoặc trạng thái để xem các đơn khác.'}
          </p>
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {filteredRequests.map((req) => {
            const isTransfer = req.requestType === 'ClassTransfer';
            const transfer = req.classTransfer;
            const doc = req.documentRequest;
            const isApproved = req.schoolBoardApprovedAt != null || req.status === 'Approved';
            const isRejected = req.status === 'Rejected';
            const isPending = req.status === 'UnderReview';

            return (
              <div
                key={req.id}
                style={{
                  backgroundColor: '#FFFFFF',
                  borderRadius: 16,
                  border: isApproved
                    ? '2px solid #059669'
                    : isRejected
                    ? '2px solid #DC2626'
                    : '2px solid #1E293B',
                  boxShadow: isApproved
                    ? '4px 4px 0 #D1FAE5'
                    : isRejected
                    ? '4px 4px 0 #FEE2E2'
                    : '4px 4px 0 #1E293B',
                  padding: 22,
                  display: 'flex',
                  flexDirection: 'column',
                  gap: 16,
                }}
              >
                {/* Header card */}
                <div
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'flex-start',
                    flexWrap: 'wrap',
                    gap: 12,
                    paddingBottom: 12,
                    borderBottom: '2px dashed #E2E8F0',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
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
                      {isTransfer ? 'Đề xuất Chuyển Lớp' : 'Đề xuất Cấp Giấy Tờ'}
                    </span>
                    <span style={{ fontWeight: 800, fontSize: 16, color: '#1E293B' }}>
                      {req.requestNumber}
                    </span>
                    <span style={{ fontSize: 13, color: '#64748B' }}>
                      (Ngày trình: {new Date(req.submissionDate).toLocaleDateString('vi-VN')})
                    </span>

                    {/* Status Badge */}
                    {isApproved && (
                      <span className="badge badge--mint" style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                        <CheckCircle2 size={13} /> Ban Giám Hiệu đã phê duyệt
                      </span>
                    )}
                    {isRejected && (
                      <span className="badge badge--pink" style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                        <XCircle size={13} /> Ban Giám Hiệu đã từ chối
                      </span>
                    )}
                    {isPending && (
                      <span className="badge badge--violet" style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
                        <Clock size={13} /> Chờ BGH phê duyệt
                      </span>
                    )}
                  </div>

                  <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                    {/* View details button for all requests */}
                    <button
                      type="button"
                      className="btn btn--outline"
                      onClick={() => {
                        setSelectedReq(req);
                        setDecisionModalOpen(true);
                      }}
                      style={{
                        padding: '7px 14px',
                        fontSize: 13,
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: 6,
                      }}
                      title="Xem toàn bộ hồ sơ chi tiết và ý kiến thẩm định"
                    >
                      <Eye size={15} /> Xem chi tiết
                    </button>

                    {/* Action buttons */}
                    {isPending && (
                      <button
                        type="button"
                        className="btn btn--primary"
                        onClick={() => {
                          setSelectedReq(req);
                          setDecisionModalOpen(true);
                        }}
                        style={{
                          padding: '8px 18px',
                          fontSize: 13,
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: 6,
                        }}
                      >
                        <ShieldCheck size={16} /> Phê duyệt / Từ chối
                      </button>
                    )}

                    {/* Download button for approved document request */}
                    {isApproved && !isTransfer && (
                      <button
                        type="button"
                        className="btn btn--outline"
                        onClick={() => handleDownloadDoc(req)}
                        style={{
                          padding: '7px 14px',
                          fontSize: 13,
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: 6,
                          borderColor: '#059669',
                          color: '#065F46',
                        }}
                      >
                        <Download size={14} /> Tải văn bản chính thức
                      </button>
                    )}
                  </div>
                </div>

                {/* Body info */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 18 }}>
                  <div>
                    <div style={{ fontSize: 12, fontWeight: 800, color: '#64748B' }}>HỌC SINH:</div>
                    <div style={{ fontSize: 16, fontWeight: 800, color: '#0F172A', marginTop: 2 }}>
                      {req.studentName} ({req.studentCode})
                    </div>
                    <div style={{ fontSize: 13, color: '#475569', marginTop: 4 }}>
                      Lớp hiện tại: <strong>{req.currentClassName || 'Chính khóa'}</strong> | Phụ huynh: <strong>{req.requesterName}</strong>
                    </div>
                  </div>

                  <div>
                    <div style={{ fontSize: 12, fontWeight: 800, color: '#64748B' }}>
                      {isTransfer ? 'LỘ TRÌNH CHUYỂN:' : 'VĂN BẢN ĐỀ NGHỊ:'}
                    </div>

                    {isTransfer && transfer && (
                      <div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 4 }}>
                          <span style={{ padding: '3px 8px', backgroundColor: '#F1F5F9', borderRadius: 6, fontWeight: 700, fontSize: 13 }}>
                            Lớp cũ: {transfer.fromClassName}
                          </span>
                          <ArrowRight size={16} color="#64748B" />
                          <span style={{ padding: '3px 8px', backgroundColor: '#D1FAE5', color: '#065F46', borderRadius: 6, fontWeight: 800, fontSize: 13, border: '1px solid #059669' }}>
                            Lớp mới: {transfer.toClassName}
                          </span>
                        </div>
                        <div style={{ marginTop: 6, display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 12, fontWeight: 700, color: '#15803D' }}>
                          <Users size={14} /> Sĩ số lớp đích: {transfer.targetClassCapacity ?? 0}/{transfer.targetClassMaxCapacity ?? 35} học sinh
                        </div>
                        {transfer.isExecuted && (
                          <div style={{ marginTop: 4, fontSize: 12, fontWeight: 700, color: '#059669' }}>
                            ✓ Giáo vụ đã thực thi phân lớp cho học sinh.
                          </div>
                        )}
                        {!transfer.isExecuted && isApproved && (
                          <div style={{ marginTop: 4, fontSize: 12, fontWeight: 600, color: '#D97706' }}>
                            ⏳ BGH đã duyệt. Đang chờ Giáo vụ thực thi chuyển lớp.
                          </div>
                        )}
                      </div>
                    )}

                    {!isTransfer && doc && (
                      <div>
                        <div style={{ fontSize: 14, fontWeight: 800, color: '#831843', marginTop: 4 }}>
                          {doc.documentType} ({doc.numberOfCopies} bản)
                        </div>
                        <div style={{ fontSize: 13, color: '#334155', marginTop: 2 }}>
                          Mục đích: {doc.purpose}
                        </div>
                      </div>
                    )}
                  </div>
                </div>

                {/* Ý kiến thẩm định của Giáo vụ */}
                <div
                  style={{
                    backgroundColor: '#EFF6FF',
                    border: '1.5px solid #3B82F6',
                    borderRadius: 12,
                    padding: '12px 16px',
                  }}
                >
                  <div style={{ fontSize: 12, fontWeight: 800, color: '#1D4ED8', marginBottom: 2 }}>
                    Ý KIẾN ĐỀ XUẤT CỦA BỘ PHẬN GIÁO VỤ:
                  </div>
                  <div style={{ fontSize: 13, color: '#1E40AF', fontStyle: 'italic', lineHeight: 1.5 }}>
                    "{isTransfer ? transfer?.staffRecommendation : doc?.staffEndorsement}"
                  </div>
                </div>

                {/* Quyết định / Chỉ đạo của Ban Giám Hiệu (đối với hồ sơ đã duyệt hoặc từ chối) */}
                {(isApproved || isRejected) && (
                  <div
                    style={{
                      backgroundColor: isApproved ? '#ECFDF5' : '#FEF2F2',
                      border: `2px solid ${isApproved ? '#059669' : '#DC2626'}`,
                      borderRadius: 12,
                      padding: '12px 16px',
                    }}
                  >
                    <div style={{ fontSize: 12, fontWeight: 800, color: isApproved ? '#047857' : '#B91C1C', marginBottom: 2 }}>
                      {isApproved ? 'QUYẾT ĐỊNH PHÊ DUYỆT CỦA BAN GIÁM HIỆU:' : 'LÝ DO TỪ CHỐI CỦA BAN GIÁM HIỆU:'}
                    </div>
                    <div style={{ fontSize: 13, color: isApproved ? '#065F46' : '#991B1B', fontWeight: 600, lineHeight: 1.5 }}>
                      "{req.schoolBoardNotes || req.reviewerNotes || (isApproved ? 'Đồng ý phê duyệt đề xuất.' : 'Không đủ điều kiện phê duyệt.')}"
                    </div>
                    {req.schoolBoardApprovedAt && (
                      <div style={{ fontSize: 11, color: '#64748B', marginTop: 6 }}>
                        Thời gian phê duyệt: {new Date(req.schoolBoardApprovedAt).toLocaleString('vi-VN')}
                      </div>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* Modal xét duyệt & quyết định */}
      <BoardDecisionModal
        open={decisionModalOpen}
        onClose={() => setDecisionModalOpen(false)}
        request={selectedReq}
        onSuccess={fetchRequests}
      />
    </div>
  );
}
