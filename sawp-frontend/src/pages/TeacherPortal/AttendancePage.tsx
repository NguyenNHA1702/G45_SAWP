import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { CheckCheck, RotateCcw, Save, TriangleAlert } from 'lucide-react';
import './AttendancePage.css';
import { attendanceApi } from '../../services/implementations/attendanceApi';
import { useAttendanceSheet } from '../../hooks/useAttendanceSheet';
import { useUnsavedChangesWarning } from '../../hooks/useUnsavedChangesWarning';
import { asApiError } from '../../utils/apiError';
import { formatViDate, formatViDateTime, todayIso } from '../../utils/date';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import type { AttendanceSubmitResult, TeacherClassOption } from '../../types/attendance.types';
import { AttendanceSummaryBar } from '../../components/attendance/AttendanceSummaryBar';
import { AttendanceRosterTable } from '../../components/attendance/AttendanceRosterTable';
import { LockedBanner } from '../../components/attendance/LockedBanner';
import { ConfirmDialog } from '../../components/common/ConfirmDialog';

export default function AttendancePage() {
  const [date, setDate] = useState<string>(todayIso());
  const [classes, setClasses] = useState<TeacherClassOption[]>([]);
  const [classId, setClassId] = useState<number | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [result, setResult] = useState<AttendanceSubmitResult | null>(null);

  const vm = useAttendanceSheet(classId, date);
  useUnsavedChangesWarning(vm.isDirty);

  // Load danh sách lớp GV được điểm danh trong ngày
  useEffect(() => {
    let cancelled = false;
    attendanceApi
      .getMyClasses(date)
      .then((list) => {
        if (cancelled) return;
        setClasses(list);
        setClassId((prev) => (prev !== null && list.some((c) => c.classId === prev) ? prev : list[0]?.classId ?? null));
      })
      .catch((e: unknown) => toast.error(asApiError(e).message));
    return () => {
      cancelled = true;
    };
  }, [date]);

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
    <main className="container attendance-page">
      {/* Decor kiểu Memphis */}
      <span className="decor decor--circle" aria-hidden="true" />
      <span className="decor decor--triangle" aria-hidden="true" />
      <span className="decor decor--square" aria-hidden="true" />

      <header className="page-header">
        <span className="page-header__eyebrow">Cổng giáo viên</span>
        <h1>
          Điểm danh <span className="squiggle">hằng ngày</span>
        </h1>
        <p>Chọn lớp và trạng thái cho từng học sinh. Vắng không phép sẽ tự trừ suất ăn và gửi thông báo cho phụ huynh.</p>
      </header>

      <section className="toolbar" aria-label="Bộ lọc">
        <div className="field">
          <label className="label" htmlFor="attendance-class">
            Lớp
          </label>
          <select
            id="attendance-class"
            className="select"
            value={classId ?? ''}
            onChange={(e) => {
              if (guardDirty()) setClassId(Number(e.target.value));
            }}
            disabled={classes.length === 0}
          >
            {classes.length === 0 && <option value="">Không có lớp được phân công</option>}
            {classes.map((c) => (
              <option key={c.classId} value={c.classId}>
                {c.className} · {c.accessReason === 'Homeroom' ? 'Chủ nhiệm' : c.accessReason === 'SchoolAdmin' ? 'Toàn trường' : 'Tiết đầu'}
                {c.isSubmitted ? ' ✓' : ''}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label className="label" htmlFor="attendance-date">
            Ngày
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
          />
        </div>
        <div className="toolbar__spacer" />
        <button
          id="btn-fill-suggested"
          type="button"
          className="btn btn--secondary"
          onClick={vm.fillSuggested}
          disabled={!canEdit || vm.summary.notMarked === 0}
        >
          <CheckCheck size={18} aria-hidden="true" /> Điền "Có mặt" cho HS còn lại
        </button>
      </section>

      <LockedBanner reason={vm.sheet?.lockReason ?? null} />

      {vm.sheet?.submittedAt && (
        <div className="banner banner--info" role="status">
          Đã điểm danh lúc {formatViDateTime(vm.sheet.submittedAt)}
          {vm.sheet.submittedByName ? ` bởi ${vm.sheet.submittedByName}` : ''}.
          {canEdit && ' Bạn vẫn có thể chỉnh sửa đến hết ngày hôm nay.'}
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
        <div className="card empty-state">
          <TriangleAlert size={40} aria-hidden="true" />
          <p>{vm.error.code === 'MSG43' ? SYSTEM_MESSAGES.MSG43.content : vm.error.message}</p>
          <button type="button" className="btn btn--secondary" onClick={vm.reload}>
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
        <footer className="sticky-footer">
          <span className="sticky-footer__info">
            {vm.isDirty && <span className="dirty-dot" aria-hidden="true" />}
            {vm.isDirty ? 'Có thay đổi chưa lưu' : 'Không có thay đổi'} · {formatViDate(date)}
          </span>
          <div style={{ display: 'flex', gap: 12 }}>
            <button
              id="btn-reset"
              type="button"
              className="btn btn--secondary"
              onClick={vm.reset}
              disabled={!vm.isDirty || vm.saving}
            >
              <RotateCcw size={18} aria-hidden="true" /> Hoàn tác
            </button>
            <button
              id="btn-save-attendance"
              type="button"
              className="btn btn--primary"
              onClick={handleSaveClick}
              disabled={vm.saving || (!vm.isDirty && !!vm.sheet.submittedAt)}
            >
              {vm.saving ? 'Đang lưu…' : 'Lưu điểm danh'}
              <span className="btn__icon">
                <Save size={14} aria-hidden="true" />
              </span>
            </button>
          </div>
        </footer>
      )}

      {result && (
        <section className="card card--featured result-card pop-in" aria-label="Kết quả lưu">
          <h3>Kết quả lần lưu gần nhất</h3>
          <ul>
            <li>
              Đã trừ <strong>{result.lunchDeductedCount}</strong> suất ăn do vắng
            </li>
            <li>
              Khôi phục <strong>{result.lunchRestoredCount}</strong> suất ăn
            </li>
            {result.lunchRestoreSkippedCount > 0 && (
              <li>
                Không khôi phục được <strong>{result.lunchRestoreSkippedCount}</strong> suất (bếp đã chốt)
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
