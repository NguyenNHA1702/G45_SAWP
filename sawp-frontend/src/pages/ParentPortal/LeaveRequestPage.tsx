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
  Pencil,
  RotateCcw,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { leaveRequestApi, type LeaveRequestItem } from '../../services/implementations/leaveRequestApi';
import { apiClient } from '../../services/apiClient';
import { LUNCH_CUTOFF_LABEL, formatDateDDMMYYYY } from '../../constants/lunch';
import { formatViDateTime } from '../../utils/date';
import { asApiError } from '../../utils/apiError';
import { GeometricModal } from '../../components/common/GeometricModal';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { PaginationBar } from '../../components/common/PaginationBar';
import { useParentChild } from '../../contexts/ParentChildContext';

interface AttachedFileItem {
  id: string;
  file: File;
  previewUrl: string | null;
  uploadedUrl: string;
  uploading: boolean;
}

export default function LeaveRequestPage() {
  const { children: contextChildren, activeChild } = useParentChild();
  const [requests, setRequests] = useState<LeaveRequestItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(15);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingRequestId, setEditingRequestId] = useState<number | null>(null);
  const [editingItem, setEditingItem] = useState<LeaveRequestItem | null>(null);

  const ATTACHMENT_KEYWORDS = [
    'giấy khám', 'giay kham', 'đính kèm', 'dinh kem', 'minh chứng', 'minh chung',
    'đơn thuốc', 'don thuoc', 'xác nhận', 'xac nhan', 'hồ sơ', 'ho so',
    'tệp', 'file', 'hình ảnh', 'hinh anh', 'chụp ảnh', 'chup anh', 'gửi ảnh', 'gui anh',
    'bác sĩ', 'bac si', 'bệnh viện', 'benh vien',
  ];
  const requiresAttachmentForAmendment =
    editingItem?.status === 'NeedMoreInfo' &&
    Boolean(editingItem.reviewerNotes) &&
    ATTACHMENT_KEYWORDS.some((k) => editingItem.reviewerNotes!.toLowerCase().includes(k));

  // Modal thu hồi đơn đã duyệt (A8)
  const [withdrawingItem, setWithdrawingItem] = useState<LeaveRequestItem | null>(null);
  const [withdrawReason, setWithdrawReason] = useState('');
  const [withdrawing, setWithdrawing] = useState(false);

  // Modal xác nhận hủy đơn (LV-05)
  const [cancellingItem, setCancellingItem] = useState<LeaveRequestItem | null>(null);
  const [cancelling, setCancelling] = useState(false);

  // LV-01: Ngày kết thúc năm học đang hoạt động để giới hạn chọn ngày trên UI khớp Backend
  const [activeSchoolYearEndDate, setActiveSchoolYearEndDate] = useState<string | null>(null);

  // Form states
  const children = contextChildren;
  const [selectedStudentId, setSelectedStudentId] = useState<number>(0);
  const nowLocal = new Date();
  const todayStr = `${nowLocal.getFullYear()}-${String(nowLocal.getMonth() + 1).padStart(2, '0')}-${String(nowLocal.getDate()).padStart(2, '0')}`;
  const [startDate, setStartDate] = useState(todayStr);
  const [endDate, setEndDate] = useState(todayStr);
  const [session, setSession] = useState<number>(3); // 1: Sáng, 2: Chiều, 3: Cả ngày
  const [keepLunch, setKeepLunch] = useState<boolean>(false);
  const [reason, setReason] = useState('');
  const [attachments, setAttachments] = useState<AttachedFileItem[]>([]);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [submitting, setSubmitting] = useState(false);
  const uploadingFile = attachments.some((a) => a.uploading);

  const isFixedHoliday = (d: Date) => {
    const m = d.getMonth() + 1;
    const day = d.getDate();
    return (m === 1 && day === 1) || (m === 4 && day === 30) || (m === 5 && day === 1) || (m === 9 && day === 2);
  };

  const calculateSchoolDays = (start: string, end: string) => {
    if (!start || !end || end < start) return 0;
    const [sy, sm, sd] = start.split('-').map(Number);
    const [ey, em, ed] = end.split('-').map(Number);
    const cur = new Date(sy, sm - 1, sd);
    const endObj = new Date(ey, em - 1, ed);
    let count = 0;
    while (cur <= endObj) {
      const dow = cur.getDay();
      if (dow !== 0 && dow !== 6 && !isFixedHoliday(cur)) {
        count++;
      }
      cur.setDate(cur.getDate() + 1);
    }
    return count;
  };
  const durationDays = calculateSchoolDays(startDate, endDate);

  const fetchRequests = async () => {
    try {
      setLoading(true);
      const res = await leaveRequestApi.getMyRequestsPaged({
        pageIndex,
        pageSize,
      });
      if (res.success && res.data) {
        setRequests(res.data.items);
        setTotalCount(res.data.totalCount);
      }
    } catch (err: unknown) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Không thể tải danh sách đơn xin nghỉ.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (selectedStudentId === 0) {
      const preferred = activeChild?.studentId ?? contextChildren[0]?.studentId;
      if (preferred) setSelectedStudentId(preferred);
    }
  }, [activeChild?.studentId, contextChildren, selectedStudentId]);

  useEffect(() => {
    fetchRequests();
  }, [pageIndex, pageSize]);

  useEffect(() => {
    let cancelled = false;
    apiClient
      .get<{ success: boolean; data?: { endDate?: string } }>('/classes/school-years/active')
      .then((res) => {
        const rawEnd = res.data?.data?.endDate;
        if (!cancelled && res.data?.success && typeof rawEnd === 'string' && rawEnd.length >= 10) {
          setActiveSchoolYearEndDate(rawEnd.slice(0, 10));
        }
      })
      .catch(() => {
        // Fallback sang giới hạn 180 ngày nếu không tải được năm học active
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const fileList = e.target.files ? Array.from(e.target.files) : [];
    if (fileInputRef.current) fileInputRef.current.value = '';
    if (fileList.length === 0) return;

    // Validate file extensions theo BR-50: chỉ chấp nhận PDF, JPG, PNG
    const allowedExts = ['.jpg', '.jpeg', '.png', '.pdf'];
    let currentCount = attachments.length;

    for (const file of fileList) {
      if (currentCount >= 5) {
        toast.error('Chỉ được đính kèm tối đa 5 tệp / ảnh minh chứng.');
        break;
      }

      const ext = file.name.slice(Math.max(0, file.name.lastIndexOf('.'))).toLowerCase();
      if (!allowedExts.includes(ext)) {
        toast.error(`Tệp "${file.name}" không đúng định dạng! Theo quy định BR-50 chỉ chấp nhận tệp PDF, JPG hoặc PNG.`);
        continue;
      }

      if (file.size > 10 * 1024 * 1024) {
        toast.error(`Tệp "${file.name}" vượt quá giới hạn 10MB.`);
        continue;
      }

      currentCount++;
      const tempId = `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
      const previewUrl = file.type.startsWith('image/') ? URL.createObjectURL(file) : null;

      setAttachments((prev) => [
        ...prev,
        { id: tempId, file, previewUrl, uploadedUrl: '', uploading: true },
      ]);

      try {
        const res = await leaveRequestApi.uploadAttachment(file);
        if (res.success && res.data) {
          setAttachments((prev) =>
            prev.map((item) =>
              item.id === tempId ? { ...item, uploadedUrl: res.data!.url, uploading: false } : item
            )
          );
          toast.success(`Đã tải lên: ${file.name}`);
        } else {
          toast.error(res.message || `Tải tệp "${file.name}" thất bại.`);
          currentCount--;
          setAttachments((prev) => prev.filter((item) => item.id !== tempId));
          if (previewUrl) URL.revokeObjectURL(previewUrl);
        }
      } catch (err: unknown) {
        const apiErr = asApiError(err);
        toast.error(apiErr.errors?.[0] || apiErr.message || `Tải tệp "${file.name}" thất bại.`);
        currentCount--;
        setAttachments((prev) => prev.filter((item) => item.id !== tempId));
        if (previewUrl) URL.revokeObjectURL(previewUrl);
      }
    }
  };

  const handleRemoveOneFile = (id: string) => {
    setAttachments((prev) => {
      const target = prev.find((x) => x.id === id);
      if (target?.previewUrl) URL.revokeObjectURL(target.previewUrl);
      return prev.filter((x) => x.id !== id);
    });
  };

  const handleRemoveFile = () => {
    setAttachments((prev) => {
      prev.forEach((x) => {
        if (x.previewUrl) URL.revokeObjectURL(x.previewUrl);
      });
      return [];
    });
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const resetForm = () => {
    setEditingRequestId(null);
    setEditingItem(null);
    setStartDate(todayStr);
    setEndDate(todayStr);
    setSession(3);
    setKeepLunch(false);
    setReason('');
    handleRemoveFile();
  };

  const openCreateModal = () => {
    resetForm();
    setShowModal(true);
  };

  const openEditModal = (item: LeaveRequestItem) => {
    setEditingRequestId(item.id);
    setEditingItem(item);
    setSelectedStudentId(item.studentId);
    setStartDate(item.startDate.slice(0, 10));
    setEndDate(item.endDate.slice(0, 10));
    const mappedSession = item.session === 'Morning' ? 1 : item.session === 'Afternoon' ? 2 : 3;
    setSession(mappedSession);
    setKeepLunch(mappedSession === 1 ? item.keepLunchService : false);
    setReason(item.reason);
    const existingUrls = (item.doctorNoteAttachmentUrl || '')
      .split('|')
      .map((u) => u.trim())
      .filter(Boolean);
    setAttachments(
      existingUrls.map((url, idx) => ({
        id: `existing-${item.id}-${idx}`,
        file: new File([], `Tệp đính kèm ${idx + 1}`),
        previewUrl: null,
        uploadedUrl: url,
        uploading: false,
      }))
    );
    setShowModal(true);
  };

  const addDaysIso = (iso: string, days: number): string => {
    if (!iso) return '';
    const [y, m, d] = iso.split('-').map(Number);
    const dt = new Date(y, m - 1, d);
    dt.setDate(dt.getDate() + days);
    return `${dt.getFullYear()}-${String(dt.getMonth() + 1).padStart(2, '0')}-${String(dt.getDate()).padStart(2, '0')}`;
  };
  const hasActiveYearEnd = Boolean(activeSchoolYearEndDate && activeSchoolYearEndDate >= todayStr);
  const maxStartDateStr = hasActiveYearEnd ? activeSchoolYearEndDate! : addDaysIso(todayStr, 180);
  const rawMaxEndDateStr = startDate ? addDaysIso(startDate, 29) : maxStartDateStr;
  const maxEndDateStr =
    hasActiveYearEnd && rawMaxEndDateStr > maxStartDateStr ? maxStartDateStr : rawMaxEndDateStr;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedStudentId) {
      toast.error('Vui lòng chọn học sinh.');
      return;
    }
    if (!startDate || !endDate) {
      toast.error('Vui lòng chọn ngày bắt đầu và ngày kết thúc nghỉ.');
      return;
    }
    if (endDate < startDate) {
      toast.error('Ngày bắt đầu nghỉ không được lớn hơn ngày kết thúc.');
      return;
    }
    const trimmedReason = reason.trim();
    if (trimmedReason.length < 5 || trimmedReason.length > 500) {
      toast.error('Lý do xin nghỉ học phải từ 5 đến 500 ký tự.');
      return;
    }
    if (requiresAttachmentForAmendment && attachments.filter((a) => a.uploadedUrl.trim()).length === 0) {
      toast.error('Đơn được yêu cầu bổ sung giấy khám / tệp minh chứng. Vui lòng đính kèm tệp trước khi gửi lại.');
      return;
    }
    if (startDate < todayStr) {
      toast.error('Không thể xin nghỉ học cho ngày trong quá khứ.');
      return;
    }
    if (hasActiveYearEnd) {
      if (startDate > maxStartDateStr || endDate > maxStartDateStr) {
        toast.error(
          `Thời gian xin nghỉ không được vượt quá ngày kết thúc năm học hiện tại (${formatDateDDMMYYYY(activeSchoolYearEndDate!)}).`
        );
        return;
      }
    } else if (startDate > maxStartDateStr) {
      toast.error('Không thể đặt ngày bắt đầu xin nghỉ quá 180 ngày kể từ hôm nay.');
      return;
    }
    // Kiểm tra theo lịch trường (cho phép ngày học bù T7/CN, chặn ngày nghỉ lễ giữa tuần)
    const monthsToCheck = Array.from(
      new Set([startDate.slice(0, 7), endDate.slice(0, 7)]),
    );
    const calendarMap = new Map<string, boolean>();
    try {
      const responses = await Promise.all(
        monthsToCheck.map((m) =>
          apiClient.get<{ success: boolean; data?: Array<{ date: string; isSchoolDay: boolean }> }>(
            `/admin/calendar?month=${encodeURIComponent(m)}`,
          ),
        ),
      );
      for (const r of responses) {
        if (r.data?.success && Array.isArray(r.data.data)) {
          for (const item of r.data.data) {
            calendarMap.set(item.date.slice(0, 10), item.isSchoolDay);
          }
        }
      }
    } catch {
      // Nếu không tải được lịch, backend sẽ kiểm tra lại khi submit
    }

    let hasSchoolDay = false;
    for (let d = new Date(startDate); d <= new Date(endDate); d.setDate(d.getDate() + 1)) {
      const iso = d.toISOString().slice(0, 10);
      if (calendarMap.has(iso)) {
        if (calendarMap.get(iso)) {
          hasSchoolDay = true;
          break;
        }
      } else {
        const day = d.getDay();
        if (day !== 0 && day !== 6) {
          hasSchoolDay = true;
          break;
        }
      }
    }
    if (!hasSchoolDay) {
      toast.error('Khoảng ngày chọn không có ngày học nào theo lịch nhà trường. Vui lòng chọn ngày học hợp lệ.');
      return;
    }
    const sessionOverlaps = (existingSession: string | number, newSession: number) => {
      const normalized =
        existingSession === 'Morning' || existingSession === 1
          ? 1
          : existingSession === 'Afternoon' || existingSession === 2
            ? 2
            : 3;
      return normalized === 3 || newSession === 3 || normalized === newSession;
    };
    const overlapping = requests.some(
      (r) =>
        r.id !== editingRequestId &&
        r.studentId === selectedStudentId &&
        (r.status === 'Submitted' || r.status === 'UnderReview' || r.status === 'Approved' || r.status === 'NeedMoreInfo') &&
        r.startDate.slice(0, 10) <= endDate &&
        r.endDate.slice(0, 10) >= startDate &&
        sessionOverlaps(r.session, session)
    );
    if (overlapping) {
      toast.error('Học sinh đã có đơn xin nghỉ trùng khoảng thời gian này đang chờ duyệt hoặc đã duyệt.');
      return;
    }

    try {
      setSubmitting(true);
      const combinedUrls = attachments
        .map((a) => a.uploadedUrl.trim())
        .filter(Boolean)
        .join(' | ');

      const effectiveKeepLunch = session === 1 ? keepLunch : false;

      if (editingRequestId) {
        const res = await leaveRequestApi.update(editingRequestId, {
          startDate,
          endDate,
          session,
          keepLunchService: effectiveKeepLunch,
          reason,
          doctorNoteAttachmentUrl: combinedUrls || undefined,
        });

        if (res.success) {
          toast.success(res.message || 'Đã cập nhật và gửi lại đơn xin nghỉ thành công!');
          setShowModal(false);
          resetForm();
          fetchRequests();
        } else {
          toast.error(res.message || 'Cập nhật đơn thất bại.');
        }
      } else {
        const res = await leaveRequestApi.submit({
          studentId: selectedStudentId,
          startDate,
          endDate,
          session,
          keepLunchService: effectiveKeepLunch,
          reason,
          doctorNoteAttachmentUrl: combinedUrls || undefined,
        });

        if (res.success) {
          const actualDays = res.data?.totalDays ?? durationDays;
          const noHomeroom = res.data?.hasHomeroomTeacher === false;
          toast.success(
            noHomeroom
              ? 'Đã gửi đơn xin nghỉ thành công! Lớp hiện chưa có GVCN nên đơn đã được chuyển thẳng tới Ban Giám Hiệu xem xét.'
              : actualDays > 3
                ? 'Đã gửi đơn nghỉ (> 3 ngày học)! GVCN sẽ xác nhận và chuyển Ban Giám Hiệu phê duyệt.'
                : res.message || 'Đã gửi đơn xin nghỉ thành công! GVCN sẽ sớm xem xét.',
            { duration: 5000 }
          );
          setShowModal(false);
          resetForm();
          fetchRequests();
        } else {
          toast.error(res.message || 'Gửi đơn thất bại.');
        }
      }
    } catch (err: unknown) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Không thể gửi đơn xin nghỉ.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleConfirmCancel = async () => {
    if (!cancellingItem) return;
    try {
      setCancelling(true);
      const res = await leaveRequestApi.cancel(cancellingItem.id);
      if (res.success) {
        toast.success('Đã hủy đơn thành công.');
        fetchRequests();
      } else {
        toast.error(res.message || 'Không thể hủy đơn.');
      }
    } catch (err: unknown) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Không thể hủy đơn.');
    } finally {
      setCancelling(false);
      setCancellingItem(null);
    }
  };

  const handleConfirmWithdraw = async () => {
    if (!withdrawingItem) return;
    try {
      setWithdrawing(true);
      const res = await leaveRequestApi.withdraw(withdrawingItem.id, withdrawReason.trim() || undefined);
      if (res.success) {
        toast.success(res.message || 'Đã thu hồi đơn và khôi phục suất ăn (nếu có) thành công!');
        setWithdrawingItem(null);
        setWithdrawReason('');
        fetchRequests();
      } else {
        toast.error(res.message || 'Không thể thu hồi đơn.');
      }
    } catch (err: unknown) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Không thể thu hồi đơn.');
    } finally {
      setWithdrawing(false);
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
            onClick={openCreateModal}
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
              Nhà bếp thực hiện chốt thực phẩm theo <strong>mốc giờ chốt từng ngày phục vụ</strong> (mặc định <strong>{LUNCH_CUTOFF_LABEL}</strong>, có thể thay đổi theo cấu hình nhà trường). 
              Các đơn nghỉ được phê duyệt trước giờ chốt của ngày áp dụng sẽ được hệ thống <strong>tự động hủy suất ăn</strong> để không tính phí bán trú. 
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
          <button type="button" className="btn btn--secondary" onClick={openCreateModal}>
            <Plus size={16} /> Tạo đơn ngay
          </button>
        </div>
      ) : (
        <div style={{ display: 'grid', gap: 18 }}>
          {requests.map((r) => {
            const isLongLeave = Boolean(r.isEscalated);
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
                      <span className={r.hasHomeroomTeacher === false ? 'badge badge--violet' : 'badge badge--amber'}>
                        {r.hasHomeroomTeacher === false
                          ? 'Chờ BGH duyệt (lớp chưa có GVCN)'
                          : isLongLeave
                            ? 'Chờ GVCN chuyển BGH'
                            : 'Chờ GVCN duyệt'}
                      </span>
                    )}
                    {r.status === 'UnderReview' && (
                      <span className="badge badge--violet">
                        Chờ BGH phê duyệt
                      </span>
                    )}
                    {r.status === 'NeedMoreInfo' && (
                      <span className="badge badge--pink">
                        <ShieldAlert size={14} /> Yêu cầu bổ sung thông tin
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
                      r.isWithdrawn ? (
                        <span className="badge badge--amber">
                          <RotateCcw size={13} /> Đã thu hồi
                        </span>
                      ) : (
                        <span className="badge badge--muted">
                          Đã hủy
                        </span>
                      )
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
                      {formatDateDDMMYYYY(r.startDate)}
                      {r.startDate !== r.endDate && ` → ${formatDateDDMMYYYY(r.endDate)}`}
                    </strong>{' '}
                    <span className={`badge ${isLongLeave ? 'badge--violet' : 'badge--mint'}`} style={{ fontSize: 'var(--fs-xs)', marginLeft: 4 }}>
                      {r.totalDays} ngày học {isLongLeave ? '(Chuyển BGH)' : '(Thông thường)'}
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
                    ) : r.session === 'Afternoon' ? (
                      <span style={{ color: 'var(--color-muted-fg)' }}>
                        Nghỉ buổi chiều (không ảnh hưởng bữa trưa)
                      </span>
                    ) : r.status === 'Submitted' || r.status === 'UnderReview' || r.status === 'NeedMoreInfo' ? (
                      <span style={{ color: 'var(--color-muted-fg)' }}>
                        Chờ duyệt (tự động hủy nếu duyệt trước {LUNCH_CUTOFF_LABEL})
                      </span>
                    ) : r.status === 'Rejected' || r.status === 'Cancelled' ? (
                      <span style={{ color: 'var(--color-muted-fg)' }}>
                        Không áp dụng
                      </span>
                    ) : r.isLunchCancelled ? (
                      <span style={{ color: '#16a34a', fontWeight: 700 }}>
                        ✓ Đã tự động hủy trước {LUNCH_CUTOFF_LABEL}
                      </span>
                    ) : r.startDate.slice(0, 10) > todayStr ? (
                      <span style={{ color: 'var(--color-muted-fg)' }}>
                        Không đăng ký bán trú ngày này
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
                    <div style={{ marginTop: 8, display: 'flex', flexWrap: 'wrap', gap: 8 }}>
                      {r.doctorNoteAttachmentUrl
                        .split('|')
                        .map((u) => u.trim())
                        .filter(Boolean)
                        .map((urlItem, idx) =>
                          urlItem.startsWith('/') || urlItem.startsWith('http') ? (
                            <button
                              key={idx}
                              type="button"
                              onClick={async () => {
                                try {
                                  await leaveRequestApi.openAttachment(urlItem);
                                } catch {
                                  toast.error('Không thể mở tệp minh chứng. Vui lòng thử lại.');
                                }
                              }}
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
                              <span>Xem minh chứng {idx + 1}</span>
                              <ExternalLink size={12} />
                            </button>
                          ) : (
                            <div key={idx} style={{ display: 'inline-flex', alignItems: 'center', gap: 6, color: 'var(--color-accent)' }}>
                              <Paperclip size={14} />
                              <strong>Minh chứng:</strong> {urlItem}
                            </div>
                          )
                        )}
                    </div>
                  )}
                  {r.reviewerNotes && (
                    <div
                      style={{
                        marginTop: 8,
                        paddingTop: 8,
                        borderTop: '1px dashed var(--color-border)',
                        color: r.status === 'Rejected' || r.status === 'NeedMoreInfo' ? 'var(--color-secondary)' : 'var(--color-accent)',
                      }}
                    >
                      <strong>Ý kiến phản hồi:</strong> {r.reviewerNotes}
                    </div>
                  )}

                  {r.statusHistory && r.statusHistory.length > 0 && (
                    <div style={{ marginTop: 10, paddingTop: 8, borderTop: '1px dashed var(--color-border)' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontWeight: 800, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginBottom: 4 }}>
                        <Clock size={12} /> Lịch sử xử lý đơn:
                      </div>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                        {r.statusHistory.map((h, idx) => (
                          <div key={idx} style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-fg)' }}>
                            <strong>{h.actionLabel}</strong>
                            {h.actorName ? ` (${h.actorName})` : ''}
                            {' — '}
                            <span style={{ color: 'var(--color-muted-fg)' }}>{formatViDateTime(h.timestamp)}</span>
                            {h.notes ? `: ${h.notes}` : ''}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>

                {/* Actions Footer */}
                {(r.status === 'Submitted' || r.status === 'NeedMoreInfo' || (r.status === 'Approved' && r.canWithdraw)) && (
                  <div style={{ display: 'flex', justifyContent: 'flex-end', flexWrap: 'wrap', gap: 8, paddingTop: 8, borderTop: '1px solid var(--color-border)', marginTop: 8 }}>
                    {(r.status === 'Submitted' || r.status === 'NeedMoreInfo') && (
                      <>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          onClick={() => openEditModal(r)}
                        >
                          <Pencil size={14} /> {r.status === 'NeedMoreInfo' ? 'Bổ sung & Gửi lại' : 'Chỉnh sửa đơn'}
                        </button>
                        <button
                          type="button"
                          className="btn btn--secondary btn--sm"
                          style={{ color: '#e11d48', borderColor: '#fda4af' }}
                          onClick={() => setCancellingItem(r)}
                        >
                          <X size={14} /> Hủy đơn này
                        </button>
                      </>
                    )}
                    {r.status === 'Approved' && r.canWithdraw && (
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        style={{ color: '#B45309', borderColor: '#FCD34D', background: '#FFFBEB' }}
                        onClick={() => {
                          setWithdrawingItem(r);
                          setWithdrawReason('');
                        }}
                      >
                        <RotateCcw size={14} /> Thu hồi đơn đã duyệt
                      </button>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      <PaginationBar
        pageIndex={pageIndex}
        pageSize={pageSize}
        totalCount={totalCount}
        onPageChange={setPageIndex}
        onPageSizeChange={(s) => {
          setPageSize(s);
          setPageIndex(1);
        }}
        itemLabel="đơn xin nghỉ"
      />

      {/* CREATE / EDIT MODAL (GeometricModal) */}
      <GeometricModal
        open={showModal}
        onClose={() => {
          setShowModal(false);
          resetForm();
        }}
        title={editingRequestId ? 'Cập nhật & Gửi lại đơn xin nghỉ' : 'Gửi đơn xin nghỉ học'}
        badgeText={editingRequestId ? 'Chỉnh sửa đơn' : 'Đơn xin nghỉ trực tuyến'}
        badgeColor="violet"
        icon={<Calendar size={24} color="var(--color-fg)" />}
        maxWidth="lg"
      >
        <form onSubmit={handleSubmit} noValidate>
          {editingItem?.status === 'NeedMoreInfo' && editingItem.reviewerNotes && (
            <div
              className="card"
              style={{
                marginBottom: 14,
                padding: '10px 14px',
                background: '#FFF1F2',
                borderColor: 'var(--color-secondary)',
                fontSize: 'var(--fs-xs)',
                color: '#9F1239',
                fontWeight: 600,
              }}
            >
              <strong>Yêu cầu bổ sung từ nhà trường:</strong> {editingItem.reviewerNotes}
            </div>
          )}
          <div className="modal-grid-2col">
            {/* CỘT TRÁI: THÔNG TIN NGHỈ HỌC */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12, minWidth: 0 }}>
              {/* Chọn học sinh */}
              <div className="field">
                <label className="label" htmlFor="leave-student">Học sinh *</label>
                {children.length > 0 ? (
                  <select
                    id="leave-student"
                    className="select"
                    value={selectedStudentId}
                    disabled={!!editingRequestId}
                    onChange={(e) => setSelectedStudentId(Number(e.target.value))}
                  >
                    <option value={0}>-- Chọn học sinh --</option>
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
                  />
                )}
              </div>

              {/* Ngày nghỉ (L-F: min/max trên input date) */}
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 10 }}>
                <div className="field">
                  <label className="label" htmlFor="leave-start-date">Từ ngày *</label>
                  <input
                    id="leave-start-date"
                    type="date"
                    className="input"
                    min={todayStr}
                    max={maxStartDateStr}
                    value={startDate}
                    onChange={(e) => {
                      const nextStart = e.target.value;
                      setStartDate(nextStart);
                      if (nextStart && (!endDate || endDate < nextStart)) {
                        setEndDate(nextStart);
                      }
                    }}
                  />
                </div>
                <div className="field">
                  <label className="label" htmlFor="leave-end-date">Đến ngày *</label>
                  <input
                    id="leave-end-date"
                    type="date"
                    className="input"
                    min={startDate || todayStr}
                    max={maxEndDateStr}
                    value={endDate}
                    onChange={(e) => setEndDate(e.target.value)}
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
                      onClick={() => {
                        setSession(s.val);
                        if (s.val !== 1) setKeepLunch(false);
                      }}
                      style={{ width: '100%', padding: '6px 4px' }}
                    >
                      {s.label}
                    </button>
                  ))}
                </div>
              </div>

              {/* Quy tắc định tuyến đơn (A2: > 3 ngày học) */}
              {durationDays > 3 ? (
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
                    <strong style={{ color: 'var(--color-accent)' }}>Nghỉ {durationDays} ngày học (&gt; 3 ngày):</strong> GVCN xác nhận &amp; chuyển Ban Giám Hiệu duyệt.
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
                    <strong style={{ color: '#059669' }}>
                      {durationDays > 0 ? `Nghỉ ${durationDays} ngày học:` : 'Ngày cuối tuần / nghỉ lễ:'}
                    </strong>{' '}
                    {durationDays > 0
                      ? 'GVCN trực tiếp xem xét và phê duyệt (≤ 3 ngày học).'
                      : 'Chỉ được chấp thuận nếu là ngày học bù theo lịch nhà trường.'}
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
                  <p style={{ margin: '4px 0 0 24px', fontSize: '11px', color: 'var(--color-muted-fg)' }}>
                    Chỉ áp dụng khi học sinh đã đăng ký suất ăn bán trú trong các ngày xin nghỉ.
                  </p>
                </div>
              )}
            </div>

            {/* CỘT PHẢI: LÝ DO & MINH CHỨNG */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12, minWidth: 0 }}>
              {/* Lý do (L-F: minLength / maxLength + đếm ký tự) */}
              <div className="field">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <label className="label" htmlFor="leave-reason">Lý do xin nghỉ học (5–500 ký tự) *</label>
                  <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>{reason.trim().length}/500</span>
                </div>
                <textarea
                  id="leave-reason"
                  className="input"
                  rows={3}
                  minLength={5}
                  maxLength={500}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder="Ví dụ: Cháu bị sốt nhẹ, gia đình xin phép cô cho cháu nghỉ một buổi để đi khám..."
                  style={{ minHeight: 75, resize: 'vertical' }}
                  required
                />
              </div>

              {/* Tài liệu đính kèm (Nhiều ảnh / tệp) */}
              <div className="field" style={{ minWidth: 0 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                  <label className="label" style={{ marginBottom: 0 }}>
                    Tài liệu đính kèm {requiresAttachmentForAmendment ? '(bắt buộc theo yêu cầu bổ sung) *' : '(giấy khám, đơn thuốc nếu có)'}
                  </label>
                  {attachments.length > 0 && (
                    <span className="badge badge--violet" style={{ fontSize: '10px', padding: '2px 8px' }}>
                      Đã chọn {attachments.length}/5 tệp
                    </span>
                  )}
                </div>

                <input
                  ref={fileInputRef}
                  id="leave-file-input"
                  type="file"
                  multiple
                  accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
                  style={{ display: 'none' }}
                  onChange={handleFileSelect}
                />

                {/* Danh sách các tệp / ảnh đã chọn với nút X xóa rõ ràng */}
                {attachments.length > 0 && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 10 }}>
                    {attachments.map((item) => (
                      <div
                        key={item.id}
                        style={{
                          border: '2px solid var(--color-fg)',
                          borderRadius: 'var(--radius-md)',
                          padding: '8px 10px',
                          background: '#FFFFFF',
                          boxShadow: '2px 2px 0 0 var(--color-fg)',
                          display: 'grid',
                          gridTemplateColumns: '42px minmax(0, 1fr) auto',
                          alignItems: 'center',
                          gap: 10,
                          width: '100%',
                          boxSizing: 'border-box',
                        }}
                      >
                        {item.previewUrl ? (
                          <img
                            src={item.previewUrl}
                            alt="Xem trước"
                            style={{
                              width: 42,
                              height: 42,
                              objectFit: 'cover',
                              borderRadius: 'var(--radius-sm)',
                              border: '1.5px solid var(--color-fg)',
                            }}
                          />
                        ) : (
                          <div
                            style={{
                              width: 42,
                              height: 42,
                              borderRadius: 'var(--radius-sm)',
                              background: 'var(--color-tertiary)',
                              border: '1.5px solid var(--color-fg)',
                              display: 'grid',
                              placeItems: 'center',
                            }}
                          >
                            <FileText size={18} color="var(--color-fg)" />
                          </div>
                        )}

                        <div style={{ minWidth: 0, overflow: 'hidden' }}>
                          <div
                            title={item.file.name}
                            style={{
                              fontWeight: 700,
                              fontSize: 'var(--fs-xs)',
                              color: 'var(--color-fg)',
                              whiteSpace: 'nowrap',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                            }}
                          >
                            {item.file.name}
                          </div>
                          <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginTop: 2 }}>
                            <span style={{ fontSize: '11px', color: 'var(--color-muted-fg)' }}>
                              {(item.file.size / 1024).toFixed(1)} KB
                            </span>
                            {item.uploading ? (
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

                        <button
                          type="button"
                          onClick={() => handleRemoveOneFile(item.id)}
                          title="Xóa ảnh / tệp này"
                          style={{
                            width: 30,
                            height: 30,
                            borderRadius: '50%',
                            border: '2px solid var(--color-fg)',
                            background: '#FFE4E6',
                            color: '#E11D48',
                            boxShadow: '1.5px 1.5px 0 0 var(--color-fg)',
                            display: 'grid',
                            placeItems: 'center',
                            cursor: 'pointer',
                            flexShrink: 0,
                            padding: 0,
                          }}
                        >
                          <X size={15} strokeWidth={2.5} />
                        </button>
                      </div>
                    ))}
                  </div>
                )}

                {attachments.length < 5 && (
                  <div
                    className="card"
                    onClick={() => fileInputRef.current?.click()}
                    style={{
                      border: '2px dashed var(--color-fg)',
                      borderRadius: 'var(--radius-md)',
                      padding: attachments.length > 0 ? '10px 12px' : '16px 14px',
                      textAlign: 'center',
                      cursor: 'pointer',
                      background: '#FFFDF5',
                      display: 'flex',
                      flexDirection: attachments.length > 0 ? 'row' : 'column',
                      justifyContent: 'center',
                      alignItems: 'center',
                      gap: 8,
                    }}
                  >
                    <div
                      style={{
                        width: attachments.length > 0 ? 28 : 38,
                        height: attachments.length > 0 ? 28 : 38,
                        borderRadius: '50%',
                        background: 'var(--color-primary)',
                        color: '#FFFFFF',
                        display: 'grid',
                        placeItems: 'center',
                        boxShadow: 'var(--shadow-pop-active)',
                        flexShrink: 0,
                      }}
                    >
                      {attachments.length > 0 ? <Plus size={15} /> : <UploadCloud size={18} />}
                    </div>
                    <div>
                      <span style={{ fontWeight: 800, fontSize: 'var(--fs-xs)', color: 'var(--color-fg)' }}>
                        {attachments.length > 0
                          ? 'Bấm để thêm ảnh / tài liệu khác (Tối đa 5 tệp)'
                          : 'Bấm vào đây để chọn 1 hoặc nhiều ảnh / tệp từ máy tính'}
                      </span>
                      {attachments.length === 0 && (
                        <p style={{ margin: '2px 0 0', fontSize: '11px', color: 'var(--color-muted-fg)' }}>
                          Hỗ trợ chọn nhiều tệp PDF hoặc ảnh JPG, PNG (theo BR-50) — Tối đa 10MB/tệp
                        </p>
                      )}
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
                resetForm();
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
              {submitting ? 'Đang xử lý…' : editingRequestId ? 'Cập nhật & Gửi lại' : 'Gửi đơn xin nghỉ'}
            </button>
          </div>
        </form>
      </GeometricModal>

      {/* WITHDRAW APPROVED LEAVE MODAL (A8) */}
      <GeometricModal
        open={!!withdrawingItem}
        onClose={() => setWithdrawingItem(null)}
        title="Thu hồi đơn xin nghỉ đã duyệt"
        badgeText="Thu hồi trước giờ chốt"
        badgeColor="amber"
        icon={<RotateCcw size={24} color="#1E293B" />}
        iconBg="#FEF3C7"
        maxWidth="md"
      >
        <div>
          <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-fg)', marginBottom: 10 }}>
            Đơn: <strong>{withdrawingItem?.requestNumber}</strong> — Học sinh: <strong>{withdrawingItem?.studentName}</strong>.
          </p>
          <p style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginBottom: 14 }}>
            Hệ thống sẽ gỡ trạng thái Vắng có phép tự động sinh từ đơn và khôi phục suất ăn bán trú (nếu đã hủy theo đơn) cho các ngày chưa qua giờ chốt, đồng thời thông báo lại cho GVCN.
          </p>
          <div className="field" style={{ marginBottom: 16 }}>
            <label className="label" htmlFor="withdraw-reason">Lý do thu hồi (tuỳ chọn)</label>
            <textarea
              id="withdraw-reason"
              className="input"
              rows={2}
              maxLength={500}
              value={withdrawReason}
              onChange={(e) => setWithdrawReason(e.target.value)}
              placeholder="Ví dụ: Cháu đã hết sốt và gia đình cho đi học trở lại..."
              style={{ minHeight: 65, resize: 'vertical' }}
            />
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setWithdrawingItem(null)}
            >
              Đóng
            </button>
            <button
              type="button"
              disabled={withdrawing}
              className="btn btn--primary"
              onClick={handleConfirmWithdraw}
            >
              <RotateCcw size={15} /> {withdrawing ? 'Đang thu hồi...' : 'Xác nhận thu hồi đơn'}
            </button>
          </div>
        </div>
      </GeometricModal>

      {/* CANCEL LEAVE REQUEST CONFIRM DIALOG (LV-05) */}
      <ConfirmDialog
        open={!!cancellingItem}
        title="Xác nhận hủy đơn xin nghỉ học"
        confirmText={cancelling ? 'Đang hủy...' : 'Xác nhận hủy đơn'}
        cancelText="Giữ lại đơn"
        busy={cancelling}
        onConfirm={handleConfirmCancel}
        onCancel={() => {
          if (!cancelling) setCancellingItem(null);
        }}
      >
        {cancellingItem && (
          <p style={{ margin: 0, lineHeight: 1.6 }}>
            Bạn có chắc chắn muốn hủy đơn <strong>{cancellingItem.requestNumber}</strong> của học sinh{' '}
            <strong>{cancellingItem.studentName}</strong> (ngày{' '}
            <strong>
              {formatDateDDMMYYYY(cancellingItem.startDate)}
              {cancellingItem.startDate.slice(0, 10) !== cancellingItem.endDate.slice(0, 10)
                ? ` → ${formatDateDDMMYYYY(cancellingItem.endDate)}`
                : ''}
            </strong>
            )? Thao tác này không thể hoàn tác.
          </p>
        )}
      </ConfirmDialog>
    </div>
  );
}
