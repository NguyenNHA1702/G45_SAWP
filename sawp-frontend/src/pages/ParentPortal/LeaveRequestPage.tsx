import React, { useEffect, useState, useRef } from 'react';
import {
  Calendar,
  Plus,
  Send,
  X,
  ShieldAlert,
  CheckCircle2,
  Sparkles,
  UtensilsCrossed,
  Clock,
  UploadCloud,
  FileText,
  Paperclip,
  ExternalLink,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { leaveRequestApi, type LeaveRequestItem } from '../../services/implementations/leaveRequestApi';
import { apiClient } from '../../services/apiClient';
import { LUNCH_CUTOFF_LABEL } from '../../constants/lunch';
import { GeometricModal } from '../../components/common/GeometricModal';

interface LinkedChild {
  studentId: number;
  fullName: string;
  studentCode: string;
  currentClassName?: string | null;
}

export default function LeaveRequestPage() {
  const [requests, setRequests] = useState<LeaveRequestItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);

  // Form states
  const [children, setChildren] = useState<LinkedChild[]>([]);
  const [selectedStudentId, setSelectedStudentId] = useState<number>(0);
  const [startDate, setStartDate] = useState(new Date().toISOString().split('T')[0]);
  const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);
  const [session, setSession] = useState<number>(3); // 1: Sáng, 2: Chiều, 3: Cả ngày
  const [keepLunch, setKeepLunch] = useState<boolean>(false);
  const [reason, setReason] = useState('');
  const [attachmentUrl, setAttachmentUrl] = useState('');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadingFile, setUploadingFile] = useState<boolean>(false);
  const [filePreviewUrl, setFilePreviewUrl] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [submitting, setSubmitting] = useState(false);

  const calculateDays = (start: string, end: string) => {
    if (!start || !end) return 1;
    const s = new Date(start);
    const e = new Date(end);
    const diffDays = Math.round((e.getTime() - s.getTime()) / (1000 * 3600 * 24)) + 1;
    return Math.max(1, diffDays);
  };
  const durationDays = calculateDays(startDate, endDate);

  const fetchRequests = async () => {
    try {
      setLoading(true);
      const res = await leaveRequestApi.getMyRequests();
      if (res.success) {
        setRequests(res.data);
      }
    } catch {
      toast.error('Không thể tải danh sách đơn xin nghỉ.');
    } finally {
      setLoading(false);
    }
  };

  const fetchChildren = async () => {
    try {
      const res = await apiClient.get('/guardians/my-children').catch(() => null);
      if (res?.data?.data && Array.isArray(res.data.data)) {
        setChildren(res.data.data);
        if (res.data.data.length > 0) {
          setSelectedStudentId(res.data.data[0].studentId);
        }
      } else {
        const fallback = await apiClient.get('/students?pageSize=5').catch(() => null);
        if (fallback?.data?.data?.items) {
          const mapped = fallback.data.data.items.map((s: { id: number; fullName: string; studentCode: string }) => ({
            studentId: s.id,
            fullName: s.fullName,
            studentCode: s.studentCode,
          }));
          setChildren(mapped);
          if (mapped.length > 0) setSelectedStudentId(mapped[0].studentId);
        }
      }
    } catch {
      // silent
    }
  };

  useEffect(() => {
    fetchRequests();
    fetchChildren();
  }, []);

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 10 * 1024 * 1024) {
      toast.error('Kích thước tệp vượt quá 10MB.');
      return;
    }

    setSelectedFile(file);
    if (file.type.startsWith('image/')) {
      setFilePreviewUrl(URL.createObjectURL(file));
    } else {
      setFilePreviewUrl(null);
    }

    try {
      setUploadingFile(true);
      const res = await leaveRequestApi.uploadAttachment(file);
      if (res.success && res.data) {
        setAttachmentUrl(res.data.url);
        toast.success(`Đã tải lên tệp: ${file.name}`);
      } else {
        setAttachmentUrl(file.name);
      }
    } catch {
      // Fallback to filename so submission still succeeds
      setAttachmentUrl(file.name);
    } finally {
      setUploadingFile(false);
    }
  };

  const handleRemoveFile = () => {
    setSelectedFile(null);
    setAttachmentUrl('');
    if (filePreviewUrl) {
      URL.revokeObjectURL(filePreviewUrl);
      setFilePreviewUrl(null);
    }
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedStudentId) {
      toast.error('Vui lòng chọn học sinh.');
      return;
    }
    if (!reason.trim()) {
      toast.error('Vui lòng nhập lý do xin nghỉ.');
      return;
    }

    try {
      setSubmitting(true);
      const res = await leaveRequestApi.submit({
        studentId: selectedStudentId,
        startDate,
        endDate,
        session,
        keepLunchService: keepLunch,
        reason,
        doctorNoteAttachmentUrl: attachmentUrl.trim() || undefined,
      });

      if (res.success) {
        toast.success(
          durationDays >= 3
            ? 'Đã gửi đơn nghỉ (≥ 3 ngày)! GVCN sẽ xác nhận và chuyển Ban Giám Hiệu phê duyệt.'
            : 'Đã gửi đơn xin nghỉ thành công! GVCN sẽ sớm xem xét.',
          { duration: 5000 }
        );
        setShowModal(false);
        setReason('');
        handleRemoveFile();
        fetchRequests();
      } else {
        toast.error(res.message || 'Gửi đơn thất bại.');
      }
    } catch (err: unknown) {
      const errorMsg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message || 'Có lỗi xảy ra khi gửi đơn.';
      toast.error(errorMsg);
    } finally {
      setSubmitting(false);
    }
  };

  const handleCancel = async (id: number) => {
    if (!window.confirm('Bạn có chắc chắn muốn hủy đơn xin nghỉ này?')) return;
    try {
      const res = await leaveRequestApi.cancel(id);
      if (res.success) {
        toast.success('Đã hủy đơn thành công.');
        fetchRequests();
      } else {
        toast.error(res.message);
      }
    } catch {
      toast.error('Không thể hủy đơn.');
    }
  };

  return (
    <div className="container pop-in">
      {/* Decorative Memphis elements */}
      <span className="decor decor--circle" style={{ top: -20, right: 40 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 30, left: 10 }} aria-hidden="true" />
      <span className="decor decor--square" style={{ top: 180, right: -15 }} aria-hidden="true" />

      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <span className="badge badge--pink" style={{ marginBottom: 8 }}>
              <Sparkles size={14} /> Cổng Phụ Huynh
            </span>
            <h1 style={{ margin: '4px 0 8px' }}>
              Đơn xin nghỉ học <span className="squiggle">cho con</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)' }}>
              Quản lý gửi đơn xin nghỉ trực tuyến, tự động đồng bộ điểm danh chuyên cần và suất ăn bán trú.
            </p>
          </div>
          <button
            type="button"
            className="btn btn--primary"
            onClick={() => setShowModal(true)}
          >
            <span className="btn__icon">
              <Plus size={16} />
            </span>
            Gửi đơn xin nghỉ mới
          </button>
        </div>
      </header>

      {/* Rules Banner (Memphis Sticker Card) */}
      <div
        className="card card--featured"
        style={{
          marginBottom: 24,
          background: 'var(--color-tertiary-soft)',
          borderColor: 'var(--color-fg)',
        }}
      >
        <div style={{ display: 'flex', gap: 14, alignItems: 'flex-start' }}>
          <div
            style={{
              width: 40,
              height: 40,
              borderRadius: '50%',
              background: 'var(--color-tertiary)',
              border: 'var(--border)',
              boxShadow: 'var(--shadow-pop-active)',
              display: 'grid',
              placeItems: 'center',
              flexShrink: 0,
            }}
          >
            <Clock size={20} color="var(--color-fg)" />
          </div>
          <div style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)', lineHeight: 1.6 }}>
            <strong style={{ fontSize: 'var(--fs-base)' }}>Quy tắc đồng bộ bán trú & điểm danh:</strong>
            <p style={{ margin: '4px 0 0' }}>
              Nhà bếp thực hiện chốt thực phẩm lúc <strong>{LUNCH_CUTOFF_LABEL}</strong> mỗi ngày. 
              Các đơn nghỉ được phê duyệt trước {LUNCH_CUTOFF_LABEL} của ngày áp dụng sẽ được hệ thống <strong>tự động hủy suất ăn</strong> để không tính phí bán trú. 
              Trường hợp học sinh chỉ nghỉ học buổi sáng nhưng trưa vẫn vào trường ăn bán trú, phụ huynh vui lòng tích chọn ô <em>"Vẫn giữ suất ăn bán trú"</em>.
            </p>
          </div>
        </div>
      </div>

      {/* List of Requests */}
      {loading ? (
        <div className="card" style={{ textAlign: 'center', padding: '48px 24px' }}>
          <p style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-base)', margin: 0 }}>Đang tải danh sách đơn xin nghỉ...</p>
        </div>
      ) : requests.length === 0 ? (
        <div className="card empty-state">
          <Calendar size={48} color="var(--color-muted-fg)" style={{ marginBottom: 12 }} />
          <h3 style={{ margin: 0, fontFamily: 'var(--font-heading)' }}>Chưa có đơn xin nghỉ nào</h3>
          <p style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)', marginTop: 6, marginBottom: 16 }}>
            Bấm vào nút "Gửi đơn xin nghỉ mới" ở trên để tạo đơn đầu tiên cho con.
          </p>
          <button type="button" className="btn btn--secondary" onClick={() => setShowModal(true)}>
            <Plus size={16} /> Tạo đơn ngay
          </button>
        </div>
      ) : (
        <div style={{ display: 'grid', gap: 18 }}>
          {requests.map((r) => {
            const isLongLeave = (r.totalDays || 1) >= 3;
            return (
              <div
                key={r.id}
                className="card card--hover"
                style={{
                  border: isLongLeave ? '2px solid var(--color-accent)' : 'var(--border)',
                  boxShadow: isLongLeave ? '8px 8px 0 0 var(--color-accent-soft)' : 'var(--shadow-card)',
                }}
              >
                {/* Header card */}
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 10, marginBottom: 12 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <span className="badge badge--muted" style={{ fontFamily: 'monospace', fontWeight: 700 }}>
                      {r.requestNumber}
                    </span>
                    <strong style={{ fontSize: 'var(--fs-md)', color: 'var(--color-fg)', fontFamily: 'var(--font-heading)' }}>
                      {r.studentName}
                    </strong>
                    {r.className && (
                      <span className="badge badge--mint">
                        {r.className}
                      </span>
                    )}
                  </div>

                  {/* Status Badge */}
                  <div>
                    {r.status === 'Submitted' && (
                      <span className="badge badge--amber">
                        {isLongLeave ? 'Chờ GVCN chuyển BGH' : 'Chờ GVCN duyệt'}
                      </span>
                    )}
                    {r.status === 'UnderReview' && (
                      <span className="badge badge--violet">
                        Chờ BGH phê duyệt
                      </span>
                    )}
                    {r.status === 'Approved' && (
                      <span className="badge badge--mint">
                        <CheckCircle2 size={14} /> Đã phê duyệt
                      </span>
                    )}
                    {r.status === 'Rejected' && (
                      <span className="badge badge--pink">
                        <ShieldAlert size={14} /> Đã từ chối
                      </span>
                    )}
                    {r.status === 'Cancelled' && (
                      <span className="badge badge--muted">
                        Đã hủy
                      </span>
                    )}
                  </div>
                </div>

                {/* Details Grid */}
                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
                    gap: 12,
                    fontSize: 'var(--fs-sm)',
                    padding: '12px 16px',
                    background: 'var(--color-muted)',
                    borderRadius: 'var(--radius-sm)',
                    border: '1px solid var(--color-border)',
                  }}
                >
                  <div>
                    <span style={{ color: 'var(--color-muted-fg)' }}>Thời gian nghỉ:</span>{' '}
                    <strong>
                      {new Date(r.startDate).toLocaleDateString('vi-VN')}
                      {r.startDate !== r.endDate && ` → ${new Date(r.endDate).toLocaleDateString('vi-VN')}`}
                    </strong>{' '}
                    <span className={`badge ${isLongLeave ? 'badge--violet' : 'badge--mint'}`} style={{ fontSize: 'var(--fs-xs)', marginLeft: 4 }}>
                      {r.totalDays || 1} ngày {isLongLeave ? '(Chuyển BGH)' : '(Thông thường)'}
                    </span>
                  </div>

                  <div>
                    <span style={{ color: 'var(--color-muted-fg)' }}>Buổi nghỉ:</span>{' '}
                    <strong>{r.sessionName}</strong>
                  </div>

                  <div>
                    <span style={{ color: 'var(--color-muted-fg)' }}>Bán trú:</span>{' '}
                    {r.keepLunchService ? (
                      <span style={{ color: 'var(--color-accent)', fontWeight: 700 }}>
                        <UtensilsCrossed size={14} style={{ display: 'inline', verticalAlign: 'middle', marginRight: 4 }} />
                        Giữ suất ăn theo yêu cầu
                      </span>
                    ) : r.isLunchCancelled ? (
                      <span style={{ color: '#16a34a', fontWeight: 700 }}>
                        ✓ Tự động hủy trước {LUNCH_CUTOFF_LABEL}
                      </span>
                    ) : (
                      <span style={{ color: 'var(--color-muted-fg)' }}>
                        Không hủy suất (sau {LUNCH_CUTOFF_LABEL} hoặc không đăng ký)
                      </span>
                    )}
                  </div>
                </div>

                {/* Reason & Notes */}
                <div style={{ marginTop: 12, fontSize: 'var(--fs-sm)', lineHeight: 1.5 }}>
                  <p style={{ margin: 0 }}>
                    <strong style={{ color: 'var(--color-muted-fg)' }}>Lý do xin nghỉ:</strong>{' '}
                    <span style={{ color: 'var(--color-fg)' }}>{r.reason}</span>
                  </p>
                  {r.doctorNoteAttachmentUrl && (
                    <div style={{ marginTop: 8 }}>
                      {r.doctorNoteAttachmentUrl.startsWith('/') || r.doctorNoteAttachmentUrl.startsWith('http') ? (
                        <a
                          href={r.doctorNoteAttachmentUrl}
                          target="_blank"
                          rel="noreferrer"
                          className="btn btn--secondary btn--sm"
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 6,
                            textDecoration: 'none',
                            color: 'var(--color-accent)',
                            borderColor: 'var(--color-accent)',
                          }}
                        >
                          <Paperclip size={14} />
                          <span>Xem tài liệu đính kèm</span>
                          <ExternalLink size={12} />
                        </a>
                      ) : (
                        <div style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color: 'var(--color-accent)' }}>
                          <Paperclip size={14} />
                          <strong>Minh chứng đính kèm:</strong> {r.doctorNoteAttachmentUrl}
                        </div>
                      )}
                    </div>
                  )}
                  {r.reviewerNotes && (
                    <div
                      style={{
                        marginTop: 8,
                        paddingTop: 8,
                        borderTop: '1px dashed var(--color-border)',
                        color: r.status === 'Rejected' ? 'var(--color-secondary)' : 'var(--color-accent)',
                      }}
                    >
                      <strong>Ý kiến phê duyệt:</strong> {r.reviewerNotes}
                    </div>
                  )}
                </div>

                {/* Actions Footer */}
                {r.status === 'Submitted' && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end', paddingTop: 8, borderTop: '1px solid var(--color-border)', marginTop: 8 }}>
                    <button
                      type="button"
                      className="btn btn--secondary btn--sm"
                      style={{ color: '#e11d48', borderColor: '#fda4af' }}
                      onClick={() => handleCancel(r.id)}
                    >
                      <X size={14} /> Hủy đơn này
                    </button>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* CREATE MODAL (GeometricModal) */}
      {/* CREATE MODAL (GeometricModal) */}
      <GeometricModal
        open={showModal}
        onClose={() => setShowModal(false)}
        title="Gửi đơn xin nghỉ học"
        badgeText="Đơn xin nghỉ trực tuyến"
        badgeColor="violet"
        icon={<Calendar size={24} color="var(--color-fg)" />}
        maxWidth="lg"
      >
        <form onSubmit={handleSubmit}>
          <div className="modal-grid-2col">
            {/* CỘT TRÁI: THÔNG TIN NGHỈ HỌC */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              {/* Chọn học sinh */}
              <div className="field">
                <label className="label" htmlFor="leave-student">Học sinh *</label>
                {children.length > 0 ? (
                  <select
                    id="leave-student"
                    className="select"
                    value={selectedStudentId}
                    onChange={(e) => setSelectedStudentId(Number(e.target.value))}
                  >
                    {children.map((c) => (
                      <option key={c.studentId} value={c.studentId}>
                        {c.fullName} ({c.studentCode}) {c.currentClassName ? `— Lớp ${c.currentClassName}` : ''}
                      </option>
                    ))}
                  </select>
                ) : (
                  <input
                    id="leave-student"
                    type="number"
                    className="input"
                    placeholder="Nhập mã ID học sinh (VD: 1)"
                    value={selectedStudentId || ''}
                    onChange={(e) => setSelectedStudentId(Number(e.target.value))}
                    required
                  />
                )}
              </div>

              {/* Ngày nghỉ */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                <div className="field">
                  <label className="label" htmlFor="leave-start-date">Từ ngày *</label>
                  <input
                    id="leave-start-date"
                    type="date"
                    className="input"
                    value={startDate}
                    onChange={(e) => {
                      setStartDate(e.target.value);
                      if (e.target.value > endDate) setEndDate(e.target.value);
                    }}
                    required
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="leave-end-date">Đến ngày *</label>
                  <input
                    id="leave-end-date"
                    type="date"
                    className="input"
                    value={endDate}
                    min={startDate}
                    onChange={(e) => setEndDate(e.target.value)}
                    required
                  />
                </div>
              </div>

              {/* Buổi nghỉ */}
              <div className="field">
                <label className="label">Buổi nghỉ *</label>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 8 }}>
                  {[
                    { val: 3, label: 'Cả ngày' },
                    { val: 1, label: 'Buổi sáng' },
                    { val: 2, label: 'Buổi chiều' },
                  ].map((s) => (
                    <button
                      key={s.val}
                      type="button"
                      className={session === s.val ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
                      onClick={() => setSession(s.val)}
                      style={{ width: '100%', padding: '6px 4px' }}
                    >
                      {s.label}
                    </button>
                  ))}
                </div>
              </div>

              {/* Quy tắc định tuyến đơn */}
              {durationDays >= 3 ? (
                <div
                  className="card"
                  style={{
                    background: 'var(--color-accent-soft)',
                    borderColor: 'var(--color-accent)',
                    padding: '10px 14px',
                    display: 'flex',
                    gap: 8,
                    alignItems: 'flex-start',
                  }}
                >
                  <ShieldAlert size={18} color="var(--color-accent)" style={{ marginTop: 2, flexShrink: 0 }} />
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-fg)', lineHeight: 1.4 }}>
                    <strong style={{ color: 'var(--color-accent)' }}>Nghỉ {durationDays} ngày (≥ 3 ngày):</strong> GVCN xác nhận & chuyển Ban Giám Hiệu duyệt.
                  </div>
                </div>
              ) : (
                <div
                  className="card"
                  style={{
                    background: 'var(--color-quaternary-soft)',
                    borderColor: 'var(--color-quaternary)',
                    padding: '10px 14px',
                    display: 'flex',
                    gap: 8,
                    alignItems: 'flex-start',
                  }}
                >
                  <CheckCircle2 size={18} color="#059669" style={{ marginTop: 2, flexShrink: 0 }} />
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-fg)', lineHeight: 1.4 }}>
                    <strong style={{ color: '#059669' }}>Nghỉ {durationDays} ngày:</strong> GVCN trực tiếp xem xét và phê duyệt.
                  </div>
                </div>
              )}

              {/* Ngoại lệ giữ suất ăn bán trú */}
              {session === 1 && (
                <div
                  className="card"
                  style={{
                    background: 'var(--color-tertiary-soft)',
                    borderColor: 'var(--color-fg)',
                    padding: '10px 12px',
                  }}
                >
                  <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontSize: 'var(--fs-xs)', fontWeight: 600 }}>
                    <input
                      type="checkbox"
                      checked={keepLunch}
                      onChange={(e) => setKeepLunch(e.target.checked)}
                      style={{ width: 16, height: 16, accentColor: 'var(--color-accent)' }}
                    />
                    <span>[Ngoại lệ] Nghỉ sáng, trưa vào ăn bán trú để học chiều</span>
                  </label>
                </div>
              )}
            </div>

            {/* CỘT PHẢI: LÝ DO & MINH CHỨNG */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              {/* Lý do */}
              <div className="field">
                <label className="label" htmlFor="leave-reason">Lý do xin nghỉ học *</label>
                <textarea
                  id="leave-reason"
                  className="input"
                  rows={3}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder="Ví dụ: Cháu bị sốt nhẹ, gia đình xin phép cô cho cháu nghỉ một buổi để đi khám..."
                  style={{ minHeight: 75, resize: 'vertical' }}
                  required
                />
              </div>

              {/* Tài liệu đính kèm */}
              <div className="field">
                <label className="label">
                  Tài liệu đính kèm (giấy khám, đơn thuốc nếu có)
                </label>

                <input
                  ref={fileInputRef}
                  id="leave-file-input"
                  type="file"
                  accept="image/png,image/jpeg,image/webp,application/pdf,.doc,.docx"
                  style={{ display: 'none' }}
                  onChange={handleFileSelect}
                />

                {!selectedFile ? (
                  <div
                    className="card"
                    onClick={() => fileInputRef.current?.click()}
                    style={{
                      border: '2px dashed var(--color-border)',
                      borderRadius: 'var(--radius-md)',
                      padding: '16px 14px',
                      textAlign: 'center',
                      cursor: 'pointer',
                      background: 'var(--color-muted)',
                      display: 'flex',
                      flexDirection: 'column',
                      alignItems: 'center',
                      gap: 6,
                    }}
                  >
                    <div
                      style={{
                        width: 38,
                        height: 38,
                        borderRadius: '50%',
                        background: 'var(--color-primary)',
                        color: '#FFFFFF',
                        display: 'grid',
                        placeItems: 'center',
                        boxShadow: 'var(--shadow-pop-active)',
                      }}
                    >
                      <UploadCloud size={18} />
                    </div>
                    <div>
                      <span style={{ fontWeight: 700, fontSize: 'var(--fs-xs)', color: 'var(--color-fg)' }}>
                        Bấm vào đây để chọn tệp từ máy tính
                      </span>
                      <p style={{ margin: '2px 0 0', fontSize: '11px', color: 'var(--color-muted-fg)' }}>
                        Ảnh (JPG, PNG), Tài liệu (PDF, DOCX) — Tối đa 10MB
                      </p>
                    </div>
                  </div>
                ) : (
                  <div
                    className="card"
                    style={{
                      border: '2px solid var(--color-primary)',
                      borderRadius: 'var(--radius-md)',
                      padding: '10px 12px',
                      background: 'var(--color-card)',
                      boxShadow: 'var(--shadow-pop-active)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      gap: 10,
                    }}
                  >
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
                      {filePreviewUrl ? (
                        <img
                          src={filePreviewUrl}
                          alt="Xem trước ảnh"
                          style={{
                            width: 42,
                            height: 42,
                            objectFit: 'cover',
                            borderRadius: 'var(--radius-sm)',
                            border: 'var(--border)',
                            flexShrink: 0,
                          }}
                        />
                      ) : (
                        <div
                          style={{
                            width: 40,
                            height: 40,
                            borderRadius: 'var(--radius-sm)',
                            background: 'var(--color-tertiary)',
                            border: 'var(--border)',
                            display: 'grid',
                            placeItems: 'center',
                            flexShrink: 0,
                          }}
                        >
                          <FileText size={20} color="var(--color-fg)" />
                        </div>
                      )}

                      <div style={{ minWidth: 0 }}>
                        <div
                          style={{
                            fontWeight: 700,
                            fontSize: 'var(--fs-xs)',
                            color: 'var(--color-fg)',
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {selectedFile.name}
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 2 }}>
                          <span style={{ fontSize: '11px', color: 'var(--color-muted-fg)' }}>
                            {(selectedFile.size / 1024).toFixed(1)} KB
                          </span>
                          {uploadingFile ? (
                            <span className="badge badge--amber" style={{ fontSize: '10px', padding: '1px 6px' }}>
                              Đang tải lên…
                            </span>
                          ) : (
                            <span className="badge badge--mint" style={{ fontSize: '10px', padding: '1px 6px' }}>
                              <CheckCircle2 size={11} /> Sẵn sàng
                            </span>
                          )}
                        </div>
                      </div>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexShrink: 0 }}>
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        onClick={() => fileInputRef.current?.click()}
                        disabled={uploadingFile}
                        style={{ padding: '4px 8px', fontSize: '11px' }}
                      >
                        Đổi
                      </button>
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        onClick={handleRemoveFile}
                        disabled={uploadingFile}
                        style={{ color: '#e11d48', borderColor: '#fda4af', padding: '4px 6px' }}
                        title="Xóa tệp đính kèm này"
                      >
                        <X size={14} />
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* Nút gửi */}
          <div
            style={{
              display: 'flex',
              justifyContent: 'flex-end',
              gap: 12,
              paddingTop: 14,
              marginTop: 14,
              borderTop: '1px solid var(--color-border)',
            }}
          >
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => {
                setShowModal(false);
                handleRemoveFile();
              }}
            >
              Đóng
            </button>
            <button
              type="submit"
              className="btn btn--primary"
              disabled={submitting || uploadingFile}
            >
              <Send size={16} />
              {submitting ? 'Đang gửi…' : 'Gửi đơn xin nghỉ'}
            </button>
          </div>
        </form>
      </GeometricModal>
    </div>
  );
}
