import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { CheckCheck, Clock, GraduationCap, RotateCcw, Save, TriangleAlert } from 'lucide-react';
import './AttendancePage.css';
import { attendanceApi } from '../../services/implementations/attendanceApi';
import { useAttendanceSheet } from '../../hooks/useAttendanceSheet';
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning';
import { asApiError } from '../../utils/apiError';
import { formatViDate, formatViDateTime, todayIso } from '../../utils/date';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import type { AttendanceSession, AttendanceSubmitResult, TeacherClassOption } from '../../types/attendance.types';
import { AttendanceSummaryBar } from '../../components/attendance/AttendanceSummaryBar';
import { AttendanceRosterTable } from '../../components/attendance/AttendanceRosterTable';
import { LockedBanner } from '../../components/attendance/LockedBanner';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';

export default function AttendancePage() {
  const [date, setDate] = useState<string>(todayIso());
  const [session, setSession] = useState<AttendanceSession>('Morning');
  const [classes, setClasses] = useState<TeacherClassOption[]>([]);
  const [classId, setClassId] = useState<number | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [result, setResult] = useState<AttendanceSubmitResult | null>(null);

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
    setConfirmOpen(true);
  };

  const handleConfirm = async () => {
    setConfirmOpen(false);
    try {
      const res = await vm.submit();
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

  const canEdit = vm.sheet?.canEdit ?? false;

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
          Ghi nhận chuyên cần từng buổi của học sinh. Học sinh vắng không phép trước giờ cắt cơm sẽ tự động hủy suất ăn và thông báo tới phụ huynh.
        </p>
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
                Lớp {c.className} · {c.accessReason === 'Homeroom' ? 'Chủ nhiệm' : c.accessReason === 'SchoolAdmin' ? 'Toàn trường' : 'Tiết đầu'}
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

      {!vm.loading && vm.error && (
        <div className="card empty-state" style={{ border: '2px solid var(--color-fg)', boxShadow: '4px 4px 0 0 var(--color-fg)', borderRadius: 'var(--radius-lg)' }}>
          <TriangleAlert size={40} aria-hidden="true" />
          <p>{vm.error.code === 'MSG43' ? SYSTEM_MESSAGES.MSG43.content : vm.error.message}</p>
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
        />
      )}

      {canEdit && vm.sheet && (
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
            <li>
              Đã gửi thông báo tới <strong>{result.parentsNotifiedCount}</strong> phụ huynh
            </li>
          </ul>
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
        {vm.impact.newlyUnexcused > 0 ? (
          <p>
            <strong>{vm.impact.newlyUnexcused}</strong> học sinh vắng không phép mới → trừ{' '}
            <strong>{vm.impact.lunchToDeduct}</strong> suất ăn và gửi thông báo cho phụ huynh.
          </p>
        ) : (
          <p>Không có học sinh vắng không phép mới.</p>
        )}
      </ConfirmDialog>
    </main>
  );
}
