import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router';
import toast from 'react-hot-toast';
import { BarChart3, CalendarCheck2, CalendarRange, CheckCheck, Clock, Eye, GraduationCap, PencilLine, RotateCcw, Save, Search, Send, Table2, TriangleAlert, Users } from 'lucide-react';
import './AttendancePage.css';
import { getAuthUser } from '../../services/apiClient';
import { attendanceApi } from '../../services/implementations/attendanceApi';
import { useAttendanceSheet } from '../../hooks/useAttendanceSheet';
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning';
import { asApiError } from '../../utils/apiError';
import { formatViDateTime, todayIso, toIsoDate } from '../../utils/date';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import type {
  AttendanceSession,
  AttendanceSheetEntry,
  AttendanceStatus,
  AttendanceSubmitResult,
  ClassAttendanceMatrix,
  ClassAttendanceSummary,
  StudentAttendanceHistory,
  TeacherClassOption,
} from '../../types/attendance.types';
import { AttendanceSummaryBar } from '../../components/attendance/AttendanceSummaryBar';
import { AttendanceRosterTable } from '../../components/attendance/AttendanceRosterTable';
import { ClassAttendanceMatrixTable } from '../../components/attendance/ClassAttendanceMatrixTable';
import { StudentAttendanceDetailView } from '../../components/attendance/StudentAttendanceDetailView';
import { LockedBanner } from '../../components/attendance/LockedBanner';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { GeometricModal } from '../../components/common/GeometricModal';

const formatDashDate = (iso: string): string => {
  const [y, m, d] = iso.slice(0, 10).split('-');
  return d && m && y ? `${d}-${m}-${y}` : iso;
};

const defaultSummaryFromIso = (): string => {
  const d = new Date();
  d.setDate(d.getDate() - 30);
  return toIsoDate(d);
};

/** Roles of the signed-in user, as stored at login (sessionStorage, or localStorage in older sessions). */
const currentRoles = (): string[] => {
  try {
    const json = getAuthUser();
    if (!json) return [];
    const parsed = JSON.parse(json);
    const raw = parsed?.roles ?? (parsed?.role ? [parsed.role] : []);
    return Array.isArray(raw) ? raw.map(String) : [String(raw)];
  } catch {
    return [];
  }
};

export default function AttendancePage() {
  const [searchParams] = useSearchParams();
  const [roles] = useState(() => currentRoles());
  const hasRole = (...targets: string[]) =>
    roles.some((r) => targets.some((t) => t.toLowerCase() === r.toLowerCase()));
  // OVR-01 (UC-23 A2): Office Staff and Teacher can send absence notices; the API checks the role too.
  const isOfficeStaff = hasRole('OfficeStaff');
  const canSendAbsenceNotice = isOfficeStaff || hasRole('Teacher');
  const isOfficeOrAdmin = isOfficeStaff || hasRole('Admin');
  const isTeacherOnly = hasRole('Teacher') && !isOfficeOrAdmin;
  const isSchoolBoardOnly = hasRole('SchoolBoard') && !hasRole('Teacher') && !isOfficeOrAdmin;
  const [activeTab, setActiveTab] = useState<'sheet' | 'summary'>(() => (isSchoolBoardOnly ? 'summary' : 'sheet'));
  const [noticeEntry, setNoticeEntry] = useState<AttendanceSheetEntry | null>(null);
  const [noticeSending, setNoticeSending] = useState(false);
  const [reattendOpen, setReattendOpen] = useState(false);
  const [reattendReason, setReattendReason] = useState('');
  const [reattendSending, setReattendSending] = useState(false);
  const [date, setDate] = useState<string>(() => searchParams.get('date') || todayIso());
  const [session, setSession] = useState<AttendanceSession>(() =>
    searchParams.get('session') === 'Afternoon' ? 'Afternoon' : 'Morning'
  );
  const [classes, setClasses] = useState<TeacherClassOption[]>([]);
  const [classId, setClassId] = useState<number | null>(() => {
    const raw = Number(searchParams.get('classId'));
    return Number.isFinite(raw) && raw > 0 ? raw : null;
  });
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [result, setResult] = useState<AttendanceSubmitResult | null>(null);
  const [correctingEntry, setCorrectingEntry] = useState<AttendanceSheetEntry | null>(null);
  const [correctionStatus, setCorrectionStatus] = useState<AttendanceStatus>('Present');
  const [correctionRemarks, setCorrectionRemarks] = useState('');
  const [correctionReason, setCorrectionReason] = useState('');
  const [submittingCorrection, setSubmittingCorrection] = useState(false);
  const [supplementReason, setSupplementReason] = useState('');
  const todayStr = todayIso();
  const isSupplementingPastDay = date < todayStr;

  // ATT-14: Tab Tổng hợp chuyên cần lớp (UC-23, UC-28, UC-50)
  const [summaryFrom, setSummaryFrom] = useState<string>(defaultSummaryFromIso);
  const [summaryTo, setSummaryTo] = useState<string>(todayIso);
  const [classSummary, setClassSummary] = useState<ClassAttendanceSummary | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [summaryViewMode, setSummaryViewMode] = useState<'table' | 'matrix'>('table');
  const [summarySearch, setSummarySearch] = useState('');
  const [classMatrix, setClassMatrix] = useState<ClassAttendanceMatrix | null>(null);
  const [matrixLoading, setMatrixLoading] = useState(false);
  const [matrixError, setMatrixError] = useState<string | null>(null);
  const [detailStudent, setDetailStudent] = useState<{ studentId: number; fullName: string; studentCode: string } | null>(null);
  const [detailHistory, setDetailHistory] = useState<StudentAttendanceHistory | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const vm = useAttendanceSheet(classId, date, session);
  useUnsavedChangesWarning(vm.isDirty);

  // Load danh sách lớp GV được điểm danh trong ngày / buổi
  useEffect(() => {
    let cancelled = false;
    attendanceApi
      .getMyClasses(date, session)
      .then((list) => {
        if (cancelled) return;
        setClasses(list);
        setClassId((prev) => {
          if (prev !== null && list.some((c) => c.classId === prev)) return prev;
          // ATT-2: Ưu tiên chọn mặc định lớp đầu tiên có học sinh để tránh mở ra lớp rỗng
          const firstNonEmpty = list.find((c) => (c.studentCount ?? 0) > 0);
          return firstNonEmpty?.classId ?? list[0]?.classId ?? null;
        });
      })
      .catch((e: unknown) => toast.error(asApiError(e).message));
    return () => {
      cancelled = true;
    };
  }, [date, session]);

  // Load báo cáo tổng hợp chuyên cần lớp khi mở tab Tổng hợp
  useEffect(() => {
    if (activeTab !== 'summary' || classId === null) return;
    if (!summaryFrom || !summaryTo) return;
    if (summaryFrom > summaryTo) {
      setClassSummary(null);
      setSummaryError('Từ ngày phải trước hoặc bằng Đến ngày.');
      return;
    }
    let cancelled = false;
    setSummaryLoading(true);
    setSummaryError(null);
    attendanceApi
      .getClassSummary(classId, summaryFrom, summaryTo)
      .then((data) => {
        if (!cancelled) setClassSummary(data);
      })
      .catch((e: unknown) => {
        if (cancelled) return;
        const err = asApiError(e);
        setClassSummary(null);
        setSummaryError(
          err.code === 'MSG43'
            ? 'Không có dữ liệu chuyên cần trong khoảng thời gian đã chọn.'
            : err.message
        );
      })
      .finally(() => {
        if (!cancelled) setSummaryLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [activeTab, classId, summaryFrom, summaryTo]);

  // Load ma trận điểm danh theo ngày của cả lớp khi ở chế độ Ma trận
  useEffect(() => {
    if (activeTab !== 'summary' || summaryViewMode !== 'matrix' || classId === null) return;
    if (!summaryFrom || !summaryTo) return;
    if (summaryFrom > summaryTo) {
      setClassMatrix(null);
      setMatrixError('Từ ngày phải trước hoặc bằng Đến ngày.');
      return;
    }
    const diffDays = Math.round((new Date(summaryTo).getTime() - new Date(summaryFrom).getTime()) / 86400000);
    if (diffDays > 31) {
      setClassMatrix(null);
      setMatrixError('Chế độ ma trận theo ngày hỗ trợ tối đa 31 ngày. Vui lòng thu hẹp khoảng thời gian.');
      return;
    }
    let cancelled = false;
    setMatrixLoading(true);
    setMatrixError(null);
    attendanceApi
      .getClassMatrix(classId, summaryFrom, summaryTo)
      .then((data) => {
        if (!cancelled) setClassMatrix(data);
      })
      .catch((e: unknown) => {
        if (cancelled) return;
        const err = asApiError(e);
        setClassMatrix(null);
        setMatrixError(
          err.code === 'MSG43'
            ? 'Không có dữ liệu chuyên cần trong khoảng thời gian đã chọn.'
            : err.message
        );
      })
      .finally(() => {
        if (!cancelled) setMatrixLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [activeTab, summaryViewMode, classId, summaryFrom, summaryTo]);

  const openStudentDetail = async (studentId: number, fullName: string, studentCode: string) => {
    setDetailStudent({ studentId, fullName, studentCode });
    setDetailHistory(null);
    setDetailLoading(true);
    try {
      const history = await attendanceApi.getStudentHistory(studentId, summaryFrom, summaryTo);
      setDetailHistory(history);
    } catch (e: unknown) {
      toast.error(asApiError(e).message);
    } finally {
      setDetailLoading(false);
    }
  };

  const guardDirty = () => !vm.isDirty || window.confirm('Bạn có thay đổi chưa lưu. Bỏ thay đổi?');

  const handleSaveClick = () => {
    if (vm.summary.notMarked > 0) {
      toast.error(`Còn ${vm.summary.notMarked} học sinh chưa điểm danh.`);
      return;
    }
    if (isSupplementingPastDay) {
      const trimmed = supplementReason.trim();
      if (trimmed.length < 5 || trimmed.length > 500) {
        toast.error('Bổ sung điểm danh ngày quá khứ bắt buộc nhập lý do từ 5 đến 500 ký tự.');
        return;
      }
    }
    setConfirmOpen(true);
  };

  const handleConfirm = async () => {
    setConfirmOpen(false);
    try {
      const res = await vm.submit(isSupplementingPastDay ? supplementReason.trim() : undefined);
      toast.success(res.message || SYSTEM_MESSAGES.MSG18.content);
      setResult(res.data);
      setSupplementReason('');
      res.data?.warnings?.forEach((w) => toast(w, { icon: '⚠️', duration: 6000 }));
    } catch (e) {
      const err = asApiError(e);
      toast.error(err.message);
      err.errors?.forEach((m) => toast.error(m));
      if (err.code === 'MSG19') vm.reload(); // vừa bị khóa -> tải lại ở chế độ chỉ xem
    }
  };

  const handleSendAbsenceNotice = async () => {
    if (!noticeEntry?.recordId) return;
    setNoticeSending(true);
    try {
      const res = await attendanceApi.sendAbsenceNotice(noticeEntry.recordId);
      toast.success(res.message || SYSTEM_MESSAGES.MSG55.content);
    } catch (e) {
      const apiErr = asApiError(e);
      toast.error(apiErr.errors?.[0] || apiErr.message);
    } finally {
      setNoticeSending(false);
      setNoticeEntry(null);
      vm.reload(); // show the sent time, or the current state after MSG09
    }
  };

  const canEdit = vm.sheet?.canEdit ?? false;
  const canCorrectPastDay =
    !canEdit &&
    (vm.sheet?.lockReason === 'PastDay' || vm.sheet?.lockReason === 'AlreadySubmitted') &&
    isOfficeOrAdmin;

  const openCorrectionModal = (entry: AttendanceSheetEntry) => {
    setCorrectingEntry(entry);
    const initialStatus = entry.status === 'ExcusedAbsence' ? 'Present' : (entry.status ?? 'Present');
    setCorrectionStatus(initialStatus);
    setCorrectionRemarks(entry.remarks ?? '');
    setCorrectionReason('');
  };

  const handleSubmitCorrection = async () => {
    if (!correctingEntry?.recordId) return;
    const trimmedReason = correctionReason.trim();
    if (trimmedReason.length < 5 || trimmedReason.length > 500) {
      toast.error('Lý do điều chỉnh bắt buộc từ 5 đến 500 ký tự.');
      return;
    }

    try {
      setSubmittingCorrection(true);
      const res = await attendanceApi.correctRecord(correctingEntry.recordId, {
        status: correctionStatus,
        remarks: correctionRemarks.trim() || null,
        correctionReason: trimmedReason,
      });
      toast.success(res.message || 'Đã điều chỉnh bản ghi điểm danh thành công.');
      setCorrectingEntry(null);
      vm.reload();
    } catch (e) {
      const err = asApiError(e);
      toast.error(err.message);
      err.errors?.forEach((m) => toast.error(m));
    } finally {
      setSubmittingCorrection(false);
    }
  };

  return (
    <main className="container attendance-page" style={{ paddingBottom: 60 }}>
      {/* Header */}
      <header className="page-header" style={{ marginBottom: 20 }}>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
          <span className="badge badge--violet" style={{ border: '2px solid var(--color-fg)', padding: '5px 14px', fontWeight: 800, borderRadius: 'var(--radius-full)' }}>
            <GraduationCap size={15} /> Quản lý Điểm danh &amp; Chuyên cần
          </span>
        </div>
        <h1 style={{ margin: '4px 0 8px' }}>
          Điểm Danh <span className="squiggle">Hằng Ngày</span>
        </h1>
        <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)', maxWidth: '68ch' }}>
          Ghi nhận chuyên cần từng buổi của học sinh. Học sinh vắng có phép trước giờ cắt cơm sẽ tự động hủy suất ăn. Giáo viên hoặc Phòng Giáo vụ có thể gửi thông báo vắng mặt không phép tới phụ huynh.
        </p>

        {/* ATT-14: Chuyển đổi giữa Điểm danh theo ngày & Tổng hợp chuyên cần lớp */}
        <div style={{ display: 'flex', gap: 10, marginTop: 16, flexWrap: 'wrap' }} role="tablist" aria-label="Chế độ xem điểm danh">
          {!isSchoolBoardOnly && (
            <button
              type="button"
              role="tab"
              aria-selected={activeTab === 'sheet'}
              className={`btn ${activeTab === 'sheet' ? 'btn--primary' : 'btn--secondary'}`}
              onClick={() => setActiveTab('sheet')}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: activeTab === 'sheet' ? '3px 3px 0 0 var(--color-fg)' : 'none',
                fontWeight: 800,
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
              }}
            >
              <CalendarCheck2 size={16} aria-hidden="true" /> Điểm danh theo ngày
            </button>
          )}
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'summary'}
            className={`btn ${activeTab === 'summary' ? 'btn--primary' : 'btn--secondary'}`}
            onClick={() => {
              if (guardDirty()) setActiveTab('summary');
            }}
            style={{
              borderRadius: 'var(--radius-full)',
              border: '2px solid var(--color-fg)',
              boxShadow: activeTab === 'summary' ? '3px 3px 0 0 var(--color-fg)' : 'none',
              fontWeight: 800,
              display: 'inline-flex',
              alignItems: 'center',
              gap: 8,
            }}
          >
            <BarChart3 size={16} aria-hidden="true" /> Tổng hợp chuyên cần
          </button>
          {isTeacherOnly && (
            <button
              id="btn-header-request-reattendance"
              type="button"
              className="btn btn--secondary"
              disabled={classId === null}
              onClick={() => {
                setReattendReason('');
                setReattendOpen(true);
              }}
              style={{
                borderRadius: 'var(--radius-full)',
                border: '2px solid var(--color-fg)',
                boxShadow: classId === null ? 'none' : '3px 3px 0 0 var(--color-fg)',
                fontWeight: 800,
                background: 'var(--color-tertiary)',
                color: 'var(--color-fg)',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                marginLeft: 'auto',
              }}
            >
              <RotateCcw size={16} aria-hidden="true" /> Yêu cầu Giáo vụ điểm danh lại
            </button>
          )}
        </div>
      </header>

      {/* Toolbar */}
      <section
        className="card card--toolbar pop-in"
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'flex-end',
          gap: 16,
          padding: '18px 24px',
          background: '#FFFDF5',
          border: '2px solid var(--color-fg)',
          boxShadow: '4px 4px 0 0 var(--color-fg)',
          borderRadius: 'var(--radius-lg)',
          marginBottom: 20,
        }}
        aria-label="Bộ lọc"
      >
        <div className="field" style={{ minWidth: 220, flex: 1 }}>
          <label className="label" htmlFor="attendance-class" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
            LỚP HỌC
          </label>
          <select
            id="attendance-class"
            className="select"
            value={classId ?? ''}
            onChange={(e) => {
              if (guardDirty()) setClassId(Number(e.target.value));
            }}
            disabled={classes.length === 0}
            style={{
              borderRadius: 'var(--radius-full)',
              border: '2px solid var(--color-fg)',
              fontWeight: 700,
              padding: '8px 16px',
              width: '100%',
            }}
          >
            {classes.length === 0 && <option value="">Không có lớp được phân công</option>}
            {classes.map((c) => {
              const yearPart = c.schoolYearName ? ` (${c.schoolYearName})` : '';
              const countPart = typeof c.studentCount === 'number' ? ` · ${c.studentCount} HS` : '';
              const rolePart = c.accessReason === 'Homeroom' ? ' · Chủ nhiệm' : '';
              return (
                <option key={c.classId} value={c.classId}>
                  Lớp {c.className}{yearPart}{rolePart}{countPart}
                  {activeTab === 'sheet' && c.isSubmitted ? ' ✓ (Đã lưu)' : ''}
                </option>
              );
            })}
          </select>
        </div>

        {activeTab === 'sheet' ? (
          <>
            <div className="field" style={{ minWidth: 160 }}>
              <label className="label" htmlFor="attendance-date" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
                NGÀY ĐIỂM DANH
              </label>
              <input
                id="attendance-date"
                type="date"
                className="input"
                value={date}
                max={todayIso()}
                onChange={(e) => {
                  if (e.target.value && guardDirty()) setDate(e.target.value);
                }}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  fontWeight: 700,
                  padding: '8px 16px',
                }}
              />
            </div>

            <div className="field" style={{ minWidth: 150 }}>
              <label className="label" htmlFor="attendance-session" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
                BUỔI HỌC
              </label>
              <select
                id="attendance-session"
                className="select"
                value={session}
                onChange={(e) => {
                  if (guardDirty()) setSession(e.target.value as AttendanceSession);
                }}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  fontWeight: 700,
                  padding: '8px 16px',
                }}
              >
                <option value="Morning">Buổi sáng</option>
                <option value="Afternoon">Buổi chiều</option>
              </select>
            </div>

            <div style={{ marginLeft: 'auto' }}>
              <button
                id="btn-fill-suggested"
                type="button"
                className="btn btn--secondary"
                onClick={vm.fillSuggested}
                disabled={!canEdit || vm.summary.notMarked === 0}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: (!canEdit || vm.summary.notMarked === 0) ? 'none' : '3px 3px 0 0 var(--color-fg)',
                  fontWeight: 800,
                  padding: '10px 20px',
                  background: (!canEdit || vm.summary.notMarked === 0) ? 'var(--color-muted)' : 'var(--color-tertiary)',
                  color: 'var(--color-fg)',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: 8,
                  cursor: (!canEdit || vm.summary.notMarked === 0) ? 'not-allowed' : 'pointer',
                }}
              >
                <CheckCheck size={18} aria-hidden="true" />
                Điền &quot;Có mặt&quot; cho HS còn lại
              </button>
            </div>
          </>
        ) : (
          <>
            <div className="field" style={{ minWidth: 160 }}>
              <label className="label" htmlFor="summary-from" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
                TỪ NGÀY
              </label>
              <input
                id="summary-from"
                type="date"
                className="input"
                value={summaryFrom}
                max={summaryTo || todayIso()}
                onChange={(e) => e.target.value && setSummaryFrom(e.target.value)}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  fontWeight: 700,
                  padding: '8px 16px',
                }}
              />
            </div>

            <div className="field" style={{ minWidth: 160 }}>
              <label className="label" htmlFor="summary-to" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
                ĐẾN NGÀY
              </label>
              <input
                id="summary-to"
                type="date"
                className="input"
                value={summaryTo}
                min={summaryFrom}
                max={todayIso()}
                onChange={(e) => e.target.value && setSummaryTo(e.target.value)}
                style={{
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  fontWeight: 700,
                  padding: '8px 16px',
                }}
              />
            </div>
          </>
        )}
      </section>

      {activeTab === 'summary' ? (
        <section aria-label="Báo cáo tổng hợp chuyên cần lớp">
          {/* Thanh chuyển chế độ xem: Bảng thống kê vs Ma trận theo ngày */}
          <div
            style={{
              display: 'flex',
              flexWrap: 'wrap',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 12,
              marginBottom: 16,
            }}
          >
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
              <button
                type="button"
                className={`btn btn--sm ${summaryViewMode === 'table' ? 'btn--primary' : 'btn--secondary'}`}
                onClick={() => setSummaryViewMode('table')}
                style={{ borderRadius: 'var(--radius-full)', border: '2px solid var(--color-fg)', fontWeight: 700 }}
              >
                <Table2 size={14} /> Bảng thống kê
              </button>
              <button
                type="button"
                className={`btn btn--sm ${summaryViewMode === 'matrix' ? 'btn--primary' : 'btn--secondary'}`}
                onClick={() => setSummaryViewMode('matrix')}
                style={{ borderRadius: 'var(--radius-full)', border: '2px solid var(--color-fg)', fontWeight: 700 }}
              >
                <CalendarRange size={14} /> Ma trận theo ngày
              </button>
            </div>

            {summaryViewMode === 'table' && classSummary && (
              <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
                <div style={{ position: 'relative' }}>
                  <Search
                    size={14}
                    style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)', color: 'var(--color-muted-fg)' }}
                  />
                  <input
                    type="text"
                    className="input"
                    value={summarySearch}
                    onChange={(e) => setSummarySearch(e.target.value)}
                    placeholder="Tìm tên hoặc mã HS..."
                    aria-label="Tìm học sinh trong báo cáo"
                    style={{
                      paddingLeft: 30,
                      paddingRight: 12,
                      paddingTop: 6,
                      paddingBottom: 6,
                      borderRadius: 'var(--radius-full)',
                      border: '2px solid var(--color-fg)',
                      fontSize: 'var(--fs-xs)',
                      width: 210,
                    }}
                  />
                </div>
              </div>
            )}
          </div>

          {summaryViewMode === 'matrix' ? (
            <>
              {matrixLoading && (
                <div aria-busy="true" style={{ display: 'grid', gap: 8 }}>
                  {Array.from({ length: 5 }).map((_, i) => (
                    <div key={i} className="skeleton" />
                  ))}
                </div>
              )}
              {!matrixLoading && matrixError && (
                <div className="card empty-state" style={{ border: '2px solid var(--color-fg)', boxShadow: '4px 4px 0 0 var(--color-fg)', borderRadius: 'var(--radius-lg)' }}>
                  <CalendarRange size={36} aria-hidden="true" />
                  <p style={{ margin: 0, fontWeight: 600 }}>{matrixError}</p>
                </div>
              )}
              {!matrixLoading && !matrixError && classMatrix && (
                <ClassAttendanceMatrixTable
                  matrix={classMatrix}
                  onSelectStudent={openStudentDetail}
                />
              )}
            </>
          ) : (
            <>
              {summaryLoading && (
                <div aria-busy="true" style={{ display: 'grid', gap: 8 }}>
                  {Array.from({ length: 5 }).map((_, i) => (
                    <div key={i} className="skeleton" />
                  ))}
                </div>
              )}

              {!summaryLoading && summaryError && (
                <div className="card empty-state" style={{ border: '2px solid var(--color-fg)', boxShadow: '4px 4px 0 0 var(--color-fg)', borderRadius: 'var(--radius-lg)' }}>
                  <BarChart3 size={36} aria-hidden="true" />
                  <p style={{ margin: 0, fontWeight: 600 }}>{summaryError}</p>
                </div>
              )}

              {!summaryLoading && !summaryError && classSummary && (
                <>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: 12, marginBottom: 16 }}>
                    <div className="card" style={{ padding: '14px 18px', border: '2px solid var(--color-fg)', boxShadow: '3px 3px 0 0 var(--color-fg)', borderRadius: 'var(--radius-md)' }}>
                      <div style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, color: 'var(--color-muted-fg)' }}>SỐ NGÀY HỌC</div>
                      <div style={{ fontSize: '1.5rem', fontWeight: 800 }}>{classSummary.schoolDays}</div>
                    </div>
                    <div className="card" style={{ padding: '14px 18px', border: '2px solid var(--color-fg)', boxShadow: '3px 3px 0 0 var(--color-fg)', borderRadius: 'var(--radius-md)', background: '#ECFDF5' }}>
                      <div style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, color: '#065F46' }}>TỈ LỆ CHUYÊN CẦN</div>
                      <div style={{ fontSize: '1.5rem', fontWeight: 800, color: '#059669' }}>{classSummary.attendanceRate}%</div>
                    </div>
                    <div className="card" style={{ padding: '14px 18px', border: '2px solid var(--color-fg)', boxShadow: '3px 3px 0 0 var(--color-fg)', borderRadius: 'var(--radius-md)' }}>
                      <div style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, color: 'var(--color-muted-fg)' }}>KHOẢNG THỜI GIAN</div>
                      <div style={{ fontSize: 'var(--fs-sm)', fontWeight: 800, marginTop: 6 }}>
                        {formatDashDate(classSummary.from)} – {formatDashDate(classSummary.to)}
                      </div>
                    </div>
                  </div>

                  <AttendanceSummaryBar summary={classSummary.totals} totalLabel="Tổng lượt điểm danh" />

                  <div
                    className="card pop-in"
                    style={{
                      border: '2px solid var(--color-fg)',
                      boxShadow: '4px 4px 0 0 var(--color-fg)',
                      borderRadius: 'var(--radius-lg)',
                      background: 'var(--color-card)',
                      padding: 0,
                      overflow: 'hidden',
                    }}
                  >
                    <div style={{ overflowX: 'auto' }}>
                      <table className="table" style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                        <thead>
                          <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                            <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', width: 48, fontWeight: 800 }}>#</th>
                            <th scope="col" style={{ padding: '12px 16px', textAlign: 'left', fontWeight: 800 }}>Học sinh</th>
                            <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Có mặt</th>
                            <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Đi muộn</th>
                            <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Vắng có phép</th>
                            <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Vắng không phép</th>
                            <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Chưa điểm danh</th>
                            <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Tỉ lệ</th>
                            <th scope="col" style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 800 }}>Chi tiết</th>
                          </tr>
                        </thead>
                        <tbody>
                          {classSummary.students
                            .filter((st) => {
                              const q = summarySearch.trim().toLowerCase();
                              if (!q) return true;
                              return st.fullName.toLowerCase().includes(q) || st.studentCode.toLowerCase().includes(q);
                            })
                            .map((st, idx) => {
                              const total = st.present + st.late + st.excusedAbsence + st.unexcusedAbsence;
                              const rateDisplay =
                                total > 0 ? `${Math.round(((st.present + st.late) * 10000) / total) / 100}%` : '—';
                              return (
                                <tr key={st.studentId} style={{ borderBottom: '1px solid var(--color-border)' }}>
                                  <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 600 }}>{idx + 1}</td>
                                  <td style={{ padding: '10px 16px' }}>
                                    <strong style={{ display: 'block' }}>{st.fullName}</strong>
                                    <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 600 }}>{st.studentCode}</span>
                                  </td>
                                  <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 700, color: '#059669' }}>{st.present}</td>
                                  <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 700, color: '#D97706' }}>{st.late}</td>
                                  <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 700, color: '#2563EB' }}>{st.excusedAbsence}</td>
                                  <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 700, color: '#DC2626' }}>{st.unexcusedAbsence}</td>
                                  <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 700, color: '#64748B' }}>{st.notMarked ?? 0}</td>
                                  <td style={{ padding: '10px 14px', textAlign: 'center', fontWeight: 800 }}>{rateDisplay}</td>
                                  <td style={{ padding: '10px 14px', textAlign: 'center' }}>
                                    <button
                                      type="button"
                                      className="btn btn--secondary btn--sm"
                                      onClick={() => openStudentDetail(st.studentId, st.fullName, st.studentCode)}
                                      style={{ borderRadius: 'var(--radius-full)', border: '1.5px solid var(--color-fg)', fontWeight: 700 }}
                                    >
                                      <Eye size={13} /> Xem
                                    </button>
                                  </td>
                                </tr>
                              );
                            })}
                        </tbody>
                      </table>
                    </div>
                  </div>
                </>
              )}
            </>
          )}
        </section>
      ) : (
        <>
          <LockedBanner reason={vm.sheet?.lockReason ?? null} />

          {vm.sheet?.submittedAt && (
            <div
              className="banner pop-in"
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 12,
                padding: '14px 20px',
                background: vm.summary.notMarked > 0 ? '#FEF3C7' : '#EDE9FE',
                border: '2px solid var(--color-fg)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-md)',
                marginBottom: 20,
                fontSize: 'var(--fs-sm)',
                fontWeight: 600,
              }}
              role="status"
            >
              <Clock size={18} color={vm.summary.notMarked > 0 ? '#B45309' : 'var(--color-accent)'} />
              <div>
                {vm.summary.notMarked > 0 ? (
                  <>
                    Chưa hoàn tất điểm danh (đã ghi nhận một phần lúc <strong>{formatViDateTime(vm.sheet.submittedAt)}</strong>
                    {vm.sheet.submittedByName ? ` bởi ${vm.sheet.submittedByName}` : ''}) ·{' '}
                    <span style={{ color: '#B45309', fontWeight: 700 }}>
                      Còn {vm.summary.notMarked} học sinh chưa điểm danh.
                    </span>
                  </>
                ) : (
                  <>
                    Đã điểm danh lúc <strong>{formatViDateTime(vm.sheet.submittedAt)}</strong>
                    {vm.sheet.submittedByName ? ` bởi ${vm.sheet.submittedByName}` : ''}.
                    {canEdit && (
                      <span style={{ color: '#059669', marginLeft: 8, fontWeight: 700 }}>
                        ✓ Bạn vẫn có thể chỉnh sửa đến hết ngày hôm nay.
                      </span>
                    )}
                  </>
                )}
              </div>
            </div>
          )}

          <AttendanceSummaryBar summary={vm.summary} />

          {vm.loading && (
            <div aria-busy="true" style={{ display: 'grid', gap: 8 }}>
              {Array.from({ length: 6 }).map((_, i) => (
                <div key={i} className="skeleton" />
              ))}
            </div>
          )}

          {!vm.loading && vm.error && (
            <div className="card empty-state" style={{ border: '2px solid var(--color-fg)', boxShadow: '4px 4px 0 0 var(--color-fg)', borderRadius: 'var(--radius-lg)' }}>
              <TriangleAlert size={40} aria-hidden="true" />
              <p>{vm.error.code === 'MSG43' ? SYSTEM_MESSAGES.MSG43.content : vm.error.message}</p>
              <button type="button" className="btn btn--secondary" onClick={vm.reload} style={{ borderRadius: 'var(--radius-full)' }}>
                Thử lại
              </button>
            </div>
          )}

          {!vm.loading && !vm.error && vm.sheet && vm.sheet.entries.length === 0 && (
            <div className="card empty-state" style={{ border: '2px solid var(--color-fg)', boxShadow: '4px 4px 0 0 var(--color-fg)', borderRadius: 'var(--radius-lg)' }}>
              <Users size={38} aria-hidden="true" />
              <p style={{ margin: 0, fontWeight: 700 }}>
                Lớp {vm.sheet.className} hiện chưa có học sinh nào được xếp vào ngày {formatDashDate(date)}.
              </p>
              <p style={{ margin: 0, fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
                Hãy chọn lớp khác hoặc kiểm tra lại danh sách xếp lớp của Phòng Giáo vụ.
              </p>
            </div>
          )}

          {!vm.loading && !vm.error && vm.sheet && vm.sheet.entries.length > 0 && (
            <AttendanceRosterTable
              entries={vm.sheet.entries}
              draft={vm.draft}
              disabled={!canEdit || vm.saving}
              onStatusChange={vm.setStatus}
              onRemarksChange={vm.setRemarks}
              onCorrect={canCorrectPastDay ? openCorrectionModal : undefined}
              onSendAbsenceNotice={canSendAbsenceNotice ? setNoticeEntry : undefined}
              absenceNoticeDisabled={noticeSending || vm.saving || vm.isDirty}
              canSendNoticeForDate={date === todayStr}
            />
          )}

          {canEdit && isSupplementingPastDay && vm.sheet && vm.sheet.entries.length > 0 && (
            <div
              className="card pop-in"
              style={{
                padding: '14px 18px',
                border: '2px solid var(--color-fg)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-md)',
                background: '#FFFBEB',
                marginTop: 16,
              }}
            >
              <label htmlFor="supplement-reason" className="label" style={{ display: 'block', marginBottom: 6, fontWeight: 700 }}>
                Lý do bổ sung điểm danh ngày quá khứ * (5–500 ký tự)
              </label>
              <input
                id="supplement-reason"
                className="input"
                type="text"
                maxLength={500}
                value={supplementReason}
                onChange={(e) => setSupplementReason(e.target.value)}
                placeholder="Ví dụ: Giáo viên nghỉ ốm đột xuất, Phòng Giáo vụ bổ sung điểm danh ngày..."
                style={{ width: '100%' }}
              />
            </div>
          )}

          {canEdit && vm.sheet && vm.sheet.entries.length > 0 && (
            <footer
              className="sticky-footer pop-in"
              style={{
                position: 'sticky',
                bottom: 20,
                zIndex: 10,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: 16,
                padding: '14px 24px',
                background: '#FFFFFF',
                border: '2px solid var(--color-fg)',
                boxShadow: '4px 4px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-full)',
                marginTop: 24,
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                {vm.isDirty ? (
                  <span className="badge badge--pink" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                    ● Có thay đổi chưa lưu
                  </span>
                ) : (
                  <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                    ✓ Dữ liệu khớp sổ
                  </span>
                )}
                <span style={{ fontWeight: 700, fontSize: 'var(--fs-sm)', color: 'var(--color-fg)' }}>
                  {session === 'Morning' ? 'Buổi sáng' : 'Buổi chiều'} · {formatDashDate(date)}
                </span>
              </div>

              <div style={{ display: 'flex', gap: 12 }}>
                <button
                  id="btn-reset"
                  type="button"
                  className="btn btn--secondary"
                  onClick={vm.reset}
                  disabled={!vm.isDirty || vm.saving}
                  style={{
                    borderRadius: 'var(--radius-full)',
                    border: '2px solid var(--color-fg)',
                    boxShadow: vm.isDirty ? '2px 2px 0 0 var(--color-fg)' : 'none',
                    fontWeight: 700,
                    padding: '8px 18px',
                  }}
                >
                  <RotateCcw size={16} aria-hidden="true" /> Hoàn tác
                </button>
                <button
                  id="btn-save-attendance"
                  type="button"
                  className="btn btn--primary"
                  onClick={handleSaveClick}
                  disabled={vm.saving || (!vm.isDirty && !!vm.sheet.submittedAt)}
                  style={{
                    borderRadius: 'var(--radius-full)',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '4px 4px 0 0 var(--color-fg)',
                    fontWeight: 800,
                    padding: '8px 24px',
                    background: 'var(--color-accent)',
                    color: '#FFFFFF',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 8,
                  }}
                >
                  <Save size={16} aria-hidden="true" />
                  {vm.saving ? 'Đang lưu…' : 'Lưu điểm danh'}
                </button>
              </div>
            </footer>
          )}

          {result && (
            <section
              className="card pop-in"
              style={{
                marginTop: 24,
                padding: '20px 24px',
                background: '#ECFDF5',
                border: '2px solid var(--color-fg)',
                boxShadow: '4px 4px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-lg)',
              }}
              aria-label="Kết quả lưu"
            >
              <h3 style={{ margin: '0 0 12px', fontFamily: 'var(--font-heading)', display: 'flex', alignItems: 'center', gap: 8 }}>
                <CheckCheck size={20} color="#059669" />
                Kết quả lưu điểm danh gần nhất
              </h3>
              <ul style={{ margin: 0, paddingLeft: 20, display: 'flex', flexDirection: 'column', gap: 6, fontSize: 'var(--fs-sm)' }}>
                <li>
                  Đã trừ <strong>{result.lunchDeductedCount}</strong> suất ăn do vắng có phép
                </li>
                <li>
                  Khôi phục <strong>{result.lunchRestoredCount}</strong> suất ăn
                </li>
                {result.lunchRestoreSkippedCount > 0 && (
                  <li style={{ color: '#D97706' }}>
                    Không khôi phục được <strong>{result.lunchRestoreSkippedCount}</strong> suất (nhà bếp đã chốt)
                  </li>
                )}
              </ul>
              {result.warnings && result.warnings.length > 0 && (
                <div
                  style={{
                    marginTop: 12,
                    padding: '10px 14px',
                    borderRadius: 'var(--radius-md)',
                    background: '#FEF3C7',
                    border: '1.5px solid #D97706',
                    color: '#92400E',
                    fontSize: 'var(--fs-sm)',
                    fontWeight: 600,
                  }}
                  role="alert"
                >
                  <div style={{ fontWeight: 800, marginBottom: 4 }}>⚠️ Cảnh báo đồng bộ suất ăn:</div>
                  <ul style={{ margin: 0, paddingLeft: 18 }}>
                    {result.warnings.map((w, idx) => (
                      <li key={idx}>{w}</li>
                    ))}
                  </ul>
                </div>
              )}
            </section>
          )}
        </>
      )}

      <ConfirmDialog
        open={confirmOpen}
        title="Xác nhận lưu điểm danh"
        confirmText="Lưu"
        busy={vm.saving}
        onConfirm={handleConfirm}
        onCancel={() => setConfirmOpen(false)}
      >
        <p>{SYSTEM_MESSAGES.MSG45.content}</p>
        {vm.impact.newlyExcused > 0 ? (
          <p>
            <strong>{vm.impact.newlyExcused}</strong> học sinh vắng có phép mới
            {vm.impact.lunchToDeduct > 0 ? (
              <> → trừ <strong>{vm.impact.lunchToDeduct}</strong> suất ăn.</>
            ) : (
              '.'
            )}
          </p>
        ) : (
          <p>Không có học sinh vắng có phép mới cần trừ suất ăn.</p>
        )}
      </ConfirmDialog>

      <GeometricModal
        open={Boolean(correctingEntry)}
        onClose={() => setCorrectingEntry(null)}
        title={date < todayStr ? 'Điều Chỉnh Điểm Danh Ngày Quá Khứ' : 'Điều Chỉnh Điểm Danh'}
        subtitle={correctingEntry ? `${correctingEntry.fullName} (${correctingEntry.studentCode}) • Ngày ${formatDashDate(date)}` : ''}
        badgeText="Ghi nhận Audit Log"
        badgeColor="amber"
        icon={<PencilLine size={20} color="var(--color-fg)" />}
        iconBg="var(--color-tertiary)"
        maxWidth="sm"
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div style={{ padding: '10px 12px', borderRadius: 'var(--radius-md)', background: '#FEF3C7', border: '1.5px solid var(--color-fg)', fontSize: 'var(--fs-xs)', fontWeight: 700, color: '#92400E' }}>
            {date < todayStr
              ? 'Chỉ dùng để sửa lỗi nhập liệu ngày quá khứ (không chuyển sang Vắng có phép). Mọi thao tác đều lưu lý do và lịch sử kiểm toán.'
              : 'Điều chỉnh từng bản ghi điểm danh hôm nay. Mọi thao tác đều lưu lý do và lịch sử kiểm toán.'}
          </div>

          <div>
            <label htmlFor="correction-status" className="label" style={{ display: 'block', marginBottom: 6 }}>
              Trạng thái mới
            </label>
            <select
              id="correction-status"
              className="select"
              value={correctionStatus}
              onChange={(e) => setCorrectionStatus(e.target.value as AttendanceStatus)}
              style={{ width: '100%' }}
            >
              <option value="Present">Có mặt</option>
              <option value="Late">Đi muộn</option>
              <option
                value="UnexcusedAbsence"
                disabled={correctingEntry?.leave?.status === 'Approved'}
              >
                Vắng không phép{correctingEntry?.leave?.status === 'Approved' ? ' (Đã có đơn nghỉ duyệt)' : ''}
              </option>
            </select>
          </div>

          <div>
            <label htmlFor="correction-remarks" className="label" style={{ display: 'block', marginBottom: 6 }}>
              Ghi chú điểm danh (tùy chọn)
            </label>
            <input
              id="correction-remarks"
              className="input"
              maxLength={500}
              value={correctionRemarks}
              onChange={(e) => setCorrectionRemarks(e.target.value)}
              placeholder="Ghi chú kèm theo bản ghi..."
            />
          </div>

          <div>
            <label htmlFor="correction-reason" className="label" style={{ display: 'block', marginBottom: 6 }}>
              Lý do điều chỉnh (bắt buộc, 5–500 ký tự) *
            </label>
            <textarea
              id="correction-reason"
              className="input"
              rows={3}
              maxLength={500}
              value={correctionReason}
              onChange={(e) => setCorrectionReason(e.target.value)}
              placeholder="Ví dụ: Giáo viên chấm nhầm dòng học sinh, đã xác nhận lại sổ điểm danh giấy..."
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 4 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setCorrectingEntry(null)}
              disabled={submittingCorrection}
            >
              Hủy
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleSubmitCorrection}
              disabled={submittingCorrection}
            >
              {submittingCorrection ? 'Đang lưu...' : 'Xác nhận điều chỉnh'}
            </button>
          </div>
        </div>
      </GeometricModal>

      <ConfirmDialog
        open={noticeEntry !== null}
        title="Gửi thông báo vắng mặt"
        confirmText="Gửi"
        busy={noticeSending}
        onConfirm={handleSendAbsenceNotice}
        onCancel={() => setNoticeEntry(null)}
      >
        <p>
          Gửi thông báo vắng mặt không phép của <strong>{noticeEntry?.fullName}</strong> ({session === 'Morning' ? 'buổi sáng' : 'buổi chiều'}{' '}
          {formatDashDate(date)}) tới phụ huynh? Mỗi buổi vắng chỉ gửi được một lần.
        </p>
      </ConfirmDialog>

      <GeometricModal
        open={detailStudent !== null}
        onClose={() => {
          setDetailStudent(null);
          setDetailHistory(null);
        }}
        title="Chi Tiết Chuyên Cần Học Sinh"
        subtitle={
          detailStudent
            ? `${detailStudent.fullName} (${detailStudent.studentCode}) • ${formatDashDate(summaryFrom)} – ${formatDashDate(summaryTo)}`
            : ''
        }
        badgeText="Cá nhân"
        badgeColor="mint"
        icon={<CalendarCheck2 size={20} color="var(--color-fg)" />}
        iconBg="var(--status-present)"
        maxWidth="lg"
      >
        {detailLoading ? (
          <div aria-busy="true" style={{ display: 'grid', gap: 8, padding: '16px 0' }}>
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="skeleton" />
            ))}
          </div>
        ) : detailHistory ? (
          <StudentAttendanceDetailView data={detailHistory} />
        ) : (
          <div className="empty-state" style={{ padding: 24 }}>
            {SYSTEM_MESSAGES.MSG43.content}
          </div>
        )}
      </GeometricModal>

      <GeometricModal
        open={reattendOpen}
        onClose={() => !reattendSending && setReattendOpen(false)}
        title="Yêu cầu Giáo vụ điểm danh lại"
        subtitle={`Gửi yêu cầu tới Phòng Giáo vụ để hỗ trợ điểm danh / điều chỉnh lại (${session === 'Morning' ? 'Buổi sáng' : 'Buổi chiều'} · ${formatDashDate(date)})`}
        badgeText="Gửi Phòng Giáo vụ"
        badgeColor="violet"
        icon={<RotateCcw size={20} color="var(--color-fg)" />}
        iconBg="var(--color-tertiary)"
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div className="field">
            <label className="label" htmlFor="reattend-reason">
              Lý do yêu cầu điểm danh lại *
            </label>
            <textarea
              id="reattend-reason"
              className="input"
              rows={3}
              maxLength={300}
              placeholder="VD: Đã quá giờ khóa sổ hoặc cần điều chỉnh lại trạng thái vắng của học sinh..."
              value={reattendReason}
              onChange={(e) => setReattendReason(e.target.value)}
            />
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button
              type="button"
              className="btn btn--secondary"
              onClick={() => setReattendOpen(false)}
              disabled={reattendSending}
            >
              Hủy
            </button>
            <button
              type="button"
              className="btn btn--primary"
              disabled={reattendSending || classId === null}
              onClick={async () => {
                if (classId === null) return;
                if (reattendReason.trim().length < 5) {
                  toast.error('Vui lòng nhập lý do yêu cầu điểm danh lại (tối thiểu 5 ký tự).');
                  return;
                }
                setReattendSending(true);
                try {
                  const res = await attendanceApi.requestReattendance(classId, {
                    date,
                    session,
                    reason: reattendReason.trim(),
                  });
                  toast.success(res.message || 'Đã gửi yêu cầu điểm danh lại tới Phòng Giáo vụ.');
                  setReattendOpen(false);
                } catch (err) {
                  toast.error(asApiError(err).message || 'Không thể gửi yêu cầu điểm danh lại.');
                } finally {
                  setReattendSending(false);
                }
              }}
            >
              <Send size={15} /> {reattendSending ? 'Đang gửi...' : 'Gửi yêu cầu'}
            </button>
          </div>
        </div>
      </GeometricModal>
    </main>
  );
}
