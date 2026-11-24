import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { BarChart3, CheckCheck, Clock, GraduationCap, PencilLine, RotateCcw, Save, TriangleAlert } from 'lucide-react';
import './AttendancePage.css';
import { getAuthUser } from '../../services/apiClient';
import { attendanceApi } from '../../services/implementations/attendanceApi';
import { useAttendanceSheet } from '../../hooks/useAttendanceSheet';
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning';
import { asApiError } from '../../utils/apiError';
import { formatViDate, formatViDateTime, monthRange, todayIso } from '../../utils/date';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import type {
  AttendanceSession,
  AttendanceSheetEntry,
  AttendanceStatus,
  AttendanceSubmitResult,
  ClassAttendanceSummary,
  TeacherClassOption,
} from '../../types/attendance.types';
import { AttendanceSummaryBar } from '../../components/attendance/AttendanceSummaryBar';
import { AttendanceRosterTable } from '../../components/attendance/AttendanceRosterTable';
import { LockedBanner } from '../../components/attendance/LockedBanner';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';
import { GeometricModal } from '../../components/common/GeometricModal';

/** Roles of the signed-in user, as stored at login (sessionStorage, or localStorage in older sessions). */
const currentRoles = (): string[] => {
  try {
    const json = getAuthUser();
    return json ? ((JSON.parse(json)?.roles as string[] | undefined) ?? []) : [];
  } catch {
    return [];
  }
};

export default function AttendancePage() {
  const [roles] = useState(() => currentRoles());
  // OVR-01 (UC-23 A2): only Office Staff send absence notices; the API checks the role too.
  const isOfficeStaff = roles.includes('OfficeStaff');
  const isOfficeOrAdmin = isOfficeStaff || roles.includes('Admin');
  const [noticeEntry, setNoticeEntry] = useState<AttendanceSheetEntry | null>(null);
  const [noticeSending, setNoticeSending] = useState(false);
  const [date, setDate] = useState<string>(todayIso());
  const [session, setSession] = useState<AttendanceSession>('Morning');
  const [classes, setClasses] = useState<TeacherClassOption[]>([]);
  const [classId, setClassId] = useState<number | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [pastDayReason, setPastDayReason] = useState('');
  const [result, setResult] = useState<AttendanceSubmitResult | null>(null);
  const [activeTab, setActiveTab] = useState<'sheet' | 'summary'>('sheet');
  const currentMonth = (() => {
    const now = new Date();
    return monthRange(now.getFullYear(), now.getMonth() + 1);
  })();
  const [summaryFrom, setSummaryFrom] = useState<string>(currentMonth.from);
  const [summaryTo, setSummaryTo] = useState<string>(currentMonth.to);
  const [classSummary, setClassSummary] = useState<ClassAttendanceSummary | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [correctingEntry, setCorrectingEntry] = useState<AttendanceSheetEntry | null>(null);
  const [correctionStatus, setCorrectionStatus] = useState<AttendanceStatus>('Present');
  const [correctionRemarks, setCorrectionRemarks] = useState('');
  const [correctionReason, setCorrectionReason] = useState('');
  const [submittingCorrection, setSubmittingCorrection] = useState(false);

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
        setClassId((prev) => (prev !== null && list.some((c) => c.classId === prev) ? prev : list[0]?.classId ?? null));
      })
      .catch((e: unknown) => toast.error(asApiError(e).message));
    return () => {
      cancelled = true;
    };
  }, [date, session]);

  const guardDirty = () => !vm.isDirty || window.confirm('Bạn có thay đổi chưa lưu. Bỏ thay đổi?');

  const handleSaveClick = () => {
    if (vm.summary.notMarked > 0) {
      toast.error(`Còn ${vm.summary.notMarked} học sinh chưa điểm danh.`);
      return;
    }
    setPastDayReason('');
    setConfirmOpen(true);
  };

  const isPastDayEdit = date < todayIso();

  const handleConfirm = async () => {
    if (isPastDayEdit) {
      const trimmed = pastDayReason.trim();
      if (trimmed.length < 5 || trimmed.length > 500) {
        toast.error('Bổ sung điểm danh ngày quá khứ bắt buộc nhập lý do từ 5 đến 500 ký tự.');
        return;
      }
    }
    setConfirmOpen(false);
    try {
      const res = await vm.submit(isPastDayEdit ? pastDayReason : undefined);
      toast.success(res.message || SYSTEM_MESSAGES.MSG18.content);
      setResult(res.data);
      res.data?.warnings?.forEach((w) => toast(w, { icon: '⚠️', duration: 6000 }));
    } catch (e) {
      const err = asApiError(e);
      toast.error(err.message);
      err.errors?.forEach((m) => toast.error(m));
      if (err.code === 'MSG19') vm.reload(); // vừa bị khóa -> tải lại ở chế độ chỉ xem
    }
  };

  useEffect(() => {
    if (activeTab !== 'summary' || classId === null) return;
    let cancelled = false;
    setSummaryLoading(true);
    setSummaryError(null);
    attendanceApi
      .getClassSummary(classId, summaryFrom, summaryTo)
      .then((data) => {
        if (!cancelled) setClassSummary(data);
      })
      .catch((e: unknown) => {
        if (!cancelled) {
          setClassSummary(null);
          setSummaryError(asApiError(e).message);
        }
      })
      .finally(() => {
        if (!cancelled) setSummaryLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [activeTab, classId, summaryFrom, summaryTo]);

  const handleSendAbsenceNotice = async () => {
    if (!noticeEntry?.recordId) return;
    setNoticeSending(true);
    try {
      const res = await attendanceApi.sendAbsenceNotice(noticeEntry.recordId);
      toast.success(res.message || SYSTEM_MESSAGES.MSG55.content);
    } catch (e) {
      toast.error(asApiError(e).message);
    } finally {
      setNoticeSending(false);
      setNoticeEntry(null);
      vm.reload(); // show the sent time, or the current state after MSG09
    }
  };

  const canEdit = vm.sheet?.canEdit ?? false;
  const canCorrectPastDay = !canEdit && vm.sheet?.lockReason === 'PastDay' && isOfficeOrAdmin;

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
            <GraduationCap size={15} /> Cổng Giáo Viên Chủ Nhiệm
          </span>
        </div>
        <h1 style={{ margin: '4px 0 8px' }}>
          Điểm Danh <span className="squiggle">Hằng Ngày</span>
        </h1>
        <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)', maxWidth: '65ch' }}>
          Ghi nhận chuyên cần từng buổi của học sinh. Học sinh vắng có phép trước giờ cắt cơm sẽ tự động hủy suất ăn. Giáo viên lưu điểm danh; Phòng Giáo vụ gửi thông báo vắng mặt cho phụ huynh.
        </p>
        <div style={{ display: 'flex', gap: 10, marginTop: 14, flexWrap: 'wrap' }}>
          <button
            type="button"
            className={`btn ${activeTab === 'sheet' ? 'btn--primary' : 'btn--secondary'}`}
            onClick={() => setActiveTab('sheet')}
            style={{ borderRadius: 'var(--radius-full)', border: '2px solid var(--color-fg)', fontWeight: 800, padding: '8px 18px' }}
          >
            Điểm danh hằng ngày
          </button>
          <button
            type="button"
            className={`btn ${activeTab === 'summary' ? 'btn--primary' : 'btn--secondary'}`}
            onClick={() => setActiveTab('summary')}
            style={{ borderRadius: 'var(--radius-full)', border: '2px solid var(--color-fg)', fontWeight: 800, padding: '8px 18px', display: 'inline-flex', alignItems: 'center', gap: 6 }}
          >
            <BarChart3 size={16} aria-hidden="true" />
            Tổng hợp chuyên cần lớp
          </button>
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
        <div className="field" style={{ minWidth: 200, flex: 1 }}>
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
            {classes.map((c) => (
              <option key={c.classId} value={c.classId}>
                Lớp {c.className}{c.schoolYearName ? ` (${c.schoolYearName})` : ''} · {c.accessReason === 'Homeroom' ? 'Chủ nhiệm' : c.accessReason === 'SchoolAdmin' ? 'Toàn trường' : 'Tiết đầu'}
                {c.isSubmitted ? ' ✓ (Đã lưu)' : ''}
              </option>
            ))}
          </select>
        </div>

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
      </section>

      {activeTab === 'sheet' && (
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
                background: '#EDE9FE',
                border: '2px solid var(--color-fg)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-md)',
                marginBottom: 20,
                fontSize: 'var(--fs-sm)',
                fontWeight: 600,
              }}
              role="status"
            >
              <Clock size={18} color="var(--color-accent)" />
              <div>
                Đã điểm danh lúc <strong>{formatViDateTime(vm.sheet.submittedAt)}</strong>
                {vm.sheet.submittedByName ? ` bởi ${vm.sheet.submittedByName}` : ''}.
                {canEdit && (
                  <span style={{ color: '#059669', marginLeft: 8, fontWeight: 700 }}>
                    ✓ Bạn vẫn có thể chỉnh sửa đến hết ngày hôm nay.
                  </span>
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

          {!vm.loading && vm.error && vm.error.code === 'MSG43' && (
            <div className="card empty-state" style={{ border: '2px solid var(--color-fg)', boxShadow: '4px 4px 0 0 var(--color-fg)', borderRadius: 'var(--radius-lg)', padding: 32, textAlign: 'center' }}>
              <GraduationCap size={40} aria-hidden="true" style={{ margin: '0 auto 10px', color: 'var(--color-muted-fg)' }} />
              <p style={{ fontWeight: 700, margin: 0 }}>Lớp chưa có học sinh trong danh sách điểm danh ngày {formatViDate(date)}.</p>
            </div>
          )}

          {!vm.loading && vm.error && vm.error.code !== 'MSG43' && (
            <div className="card empty-state" style={{ border: '2px solid var(--color-fg)', boxShadow: '4px 4px 0 0 var(--color-fg)', borderRadius: 'var(--radius-lg)' }}>
              <TriangleAlert size={40} aria-hidden="true" />
              <p>{vm.error.message}</p>
              <button type="button" className="btn btn--secondary" onClick={vm.reload} style={{ borderRadius: 'var(--radius-full)' }}>
                Thử lại
              </button>
            </div>
          )}

          {!vm.loading && !vm.error && vm.sheet && (
            <AttendanceRosterTable
              entries={vm.sheet.entries}
              draft={vm.draft}
              disabled={!canEdit || vm.saving}
              onStatusChange={vm.setStatus}
              onRemarksChange={vm.setRemarks}
              onCorrect={canCorrectPastDay ? openCorrectionModal : undefined}
              onSendAbsenceNotice={isOfficeStaff ? setNoticeEntry : undefined}
              absenceNoticeDisabled={noticeSending || vm.saving || vm.isDirty}
            />
          )}
        </>
      )}

      {activeTab === 'summary' && (
        <section className="card pop-in" style={{ border: '2px solid var(--color-fg)', boxShadow: '4px 4px 0 0 var(--color-fg)', borderRadius: 'var(--radius-lg)', padding: 24 }}>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 16, alignItems: 'flex-end', marginBottom: 20 }}>
            <div className="field">
              <label className="label" htmlFor="summary-from">TỪ NGÀY</label>
              <input id="summary-from" type="date" className="input" value={summaryFrom} onChange={(e) => e.target.value && setSummaryFrom(e.target.value)} />
            </div>
            <div className="field">
              <label className="label" htmlFor="summary-to">ĐẾN NGÀY</label>
              <input id="summary-to" type="date" className="input" value={summaryTo} onChange={(e) => e.target.value && setSummaryTo(e.target.value)} />
            </div>
          </div>

          {summaryLoading && <div className="skeleton" style={{ height: 120 }} />}
          {!summaryLoading && summaryError && (
            <div className="empty-state">
              <TriangleAlert size={32} aria-hidden="true" />
              <p>{summaryError}</p>
            </div>
          )}
          {!summaryLoading && !summaryError && classSummary && (
            <>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, marginBottom: 18 }}>
                <span className="badge badge--violet" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                  Số ngày học: {classSummary.schoolDays} ngày
                </span>
                <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                  Tỉ lệ chuyên cần: {classSummary.attendanceRate}%
                </span>
              </div>
              <AttendanceSummaryBar summary={classSummary.totals} />
              {classSummary.students.length === 0 ? (
                <p style={{ color: 'var(--color-muted-fg)', fontWeight: 600 }}>Chưa có dữ liệu điểm danh trong khoảng thời gian đã chọn.</p>
              ) : (
                <div style={{ overflowX: 'auto', marginTop: 16 }}>
                  <table className="table" style={{ width: '100%', borderCollapse: 'collapse' }}>
                    <thead>
                      <tr>
                        <th style={{ textAlign: 'left', padding: '10px 12px' }}>Mã HS</th>
                        <th style={{ textAlign: 'left', padding: '10px 12px' }}>Họ và tên</th>
                        <th style={{ textAlign: 'right', padding: '10px 12px' }}>Có mặt</th>
                        <th style={{ textAlign: 'right', padding: '10px 12px' }}>Đi muộn</th>
                        <th style={{ textAlign: 'right', padding: '10px 12px' }}>Vắng có phép</th>
                        <th style={{ textAlign: 'right', padding: '10px 12px' }}>Vắng không phép</th>
                      </tr>
                    </thead>
                    <tbody>
                      {classSummary.students.map((st) => (
                        <tr key={st.studentId} style={{ borderTop: '1px solid var(--color-border)' }}>
                          <td style={{ padding: '10px 12px', fontWeight: 700 }}>{st.studentCode}</td>
                          <td style={{ padding: '10px 12px', fontWeight: 600 }}>{st.fullName}</td>
                          <td style={{ padding: '10px 12px', textAlign: 'right', color: '#059669', fontWeight: 700 }}>{st.present}</td>
                          <td style={{ padding: '10px 12px', textAlign: 'right', color: '#D97706', fontWeight: 700 }}>{st.late}</td>
                          <td style={{ padding: '10px 12px', textAlign: 'right', color: '#2563EB', fontWeight: 700 }}>{st.excusedAbsence}</td>
                          <td style={{ padding: '10px 12px', textAlign: 'right', color: '#DC2626', fontWeight: 700 }}>{st.unexcusedAbsence}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </>
          )}
        </section>
      )}

      {activeTab === 'sheet' && canEdit && vm.sheet && (
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
              {session === 'Morning' ? 'Buổi sáng' : 'Buổi chiều'} · {formatViDate(date)}
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
              Đã trừ <strong>{result.lunchDeductedCount}</strong> suất ăn do vắng
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
            <strong>{vm.impact.newlyExcused}</strong> học sinh vắng có phép mới → trừ{' '}
            <strong>{vm.impact.lunchToDeduct}</strong> suất ăn.
          </p>
        ) : (
          <p>Không có học sinh vắng có phép mới.</p>
        )}
        {isPastDayEdit && (
          <div style={{ marginTop: 12 }}>
            <label htmlFor="past-day-reason" className="label" style={{ display: 'block', marginBottom: 6 }}>
              Lý do bổ sung điểm danh ngày quá khứ (bắt buộc, 5–500 ký tự) *
            </label>
            <textarea
              id="past-day-reason"
              className="input"
              rows={3}
              maxLength={500}
              value={pastDayReason}
              onChange={(e) => setPastDayReason(e.target.value)}
              placeholder="Ví dụ: Giáo viên nghỉ ốm chưa kịp điểm danh, Giáo vụ bổ sung theo sổ giấy..."
            />
          </div>
        )}
      </ConfirmDialog>

      <GeometricModal
        open={Boolean(correctingEntry)}
        onClose={() => setCorrectingEntry(null)}
        title="Điều Chỉnh Điểm Danh Ngày Quá Khứ"
        subtitle={correctingEntry ? `${correctingEntry.fullName} (${correctingEntry.studentCode}) • Ngày ${formatViDate(date)}` : ''}
        badgeText="Ghi nhận Audit Log"
        badgeColor="amber"
        icon={<PencilLine size={20} color="var(--color-fg)" />}
        iconBg="var(--color-tertiary)"
        maxWidth="sm"
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <div style={{ padding: '10px 12px', borderRadius: 'var(--radius-md)', background: '#FEF3C7', border: '1.5px solid var(--color-fg)', fontSize: 'var(--fs-xs)', fontWeight: 700, color: '#92400E' }}>
            Chỉ dùng để sửa lỗi nhập liệu ngày quá khứ (không chuyển sang Vắng có phép). Mọi thao tác đều lưu lý do và lịch sử kiểm toán.
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
              <option value="UnexcusedAbsence">Vắng không phép</option>
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
          {formatViDate(date)}) tới phụ huynh? Mỗi buổi vắng chỉ gửi được một lần.
        </p>
      </ConfirmDialog>
    </main>
  );
}
