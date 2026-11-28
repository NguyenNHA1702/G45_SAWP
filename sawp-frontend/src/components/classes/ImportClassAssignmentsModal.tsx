import React, { useEffect, useState } from 'react';
import { AlertTriangle, CheckCircle2, Download, FileSpreadsheet, Upload } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { classApi } from '../../services/implementations/classApi';
import { SYSTEM_MESSAGES, getSystemMessage } from '../../constants/messages';
import type { SchoolYearDto } from '../../types/class.types';
import type {
  ClassImportMode,
  ClassImportPreviewDto,
  ClassImportResultDto,
  ClassImportRowsDto,
} from '../../types/classImport.types';

interface ImportClassAssignmentsModalProps {
  open: boolean;
  onClose: () => void;
  schoolYear: SchoolYearDto | null;
  onImported: () => void;
}

type Step = 'select' | 'preview' | 'result';

/** SRS message of the response code (MSG41, MSG49, MSG62), then the details; the API puts the first detail in `message`. */
const errorOf = (err: any, fallback: string): string => {
  const data = err?.response?.data ?? err;
  const srs = (data?.code && getSystemMessage(data.code)?.content) || (data?.errors?.length ? fallback : data?.message) || fallback;
  return [srs, ...(data?.errors ?? [])].join(' ');
};

const cellStyle: React.CSSProperties = { padding: '6px 10px', borderBottom: '1px solid #E2E8F0', textAlign: 'left', verticalAlign: 'top' };
const headStyle: React.CSSProperties = { ...cellStyle, fontWeight: 800, background: '#F8FAFC', fontSize: '0.75rem', textTransform: 'uppercase' };

/** UC-69 Import Class Assignments: select the file, preview (A2 Replace / Merge), confirm, totals. */
export const ImportClassAssignmentsModal: React.FC<ImportClassAssignmentsModalProps> = ({
  open,
  onClose,
  schoolYear,
  onImported,
}) => {
  const [step, setStep] = useState<Step>('select');
  const [file, setFile] = useState<File | null>(null);
  const [mode, setMode] = useState<ClassImportMode>('Merge');
  const [allowAgeException, setAllowAgeException] = useState(false);
  const [preview, setPreview] = useState<ClassImportPreviewDto | null>(null);
  const [result, setResult] = useState<ClassImportResultDto | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (open) {
      setStep('select');
      setFile(null);
      setMode('Merge');
      setAllowAgeException(false);
      setPreview(null);
      setResult(null);
    }
  }, [open]);

  if (!schoolYear) return null;

  const rowsOf = (p: ClassImportPreviewDto, m: ClassImportMode): ClassImportRowsDto => ({
    mode: m,
    allowAgeException,
    classes: p.classes,
    students: p.students,
  });

  const handleDownloadTemplate = async () => {
    try {
      const blob = await classApi.downloadClassImportTemplate();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'Mau_Xep_Lop_SAWP.xlsx';
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch {
      toast.error('Lỗi khi tải tệp mẫu.');
    }
  };

  const handlePreview = async () => {
    if (!file) {
      toast.error('Vui lòng chọn tệp Excel (.xlsx).');
      return;
    }
    setBusy(true);
    try {
      const res = await classApi.previewClassImport(schoolYear.id, file, mode);
      if (res.success && res.data) {
        setPreview(res.data);
        setMode(res.data.mode);
        setStep('preview');
      } else {
        toast.error(errorOf(res, SYSTEM_MESSAGES.MSG41.content));
      }
    } catch (err: any) {
      toast.error(errorOf(err, SYSTEM_MESSAGES.MSG41.content));
    } finally {
      setBusy(false);
    }
  };

  const handleModeChange = async (next: ClassImportMode) => {
    if (!preview || next === mode) return;
    const previous = mode;
    setMode(next);
    setBusy(true);
    try {
      const res = await classApi.previewClassImportRows(schoolYear.id, rowsOf(preview, next));
      if (res.success && res.data) {
        setPreview(res.data);
      } else {
        setMode(previous);
      }
    } catch (err: any) {
      setMode(previous);
      toast.error(errorOf(err, 'Không thể kiểm tra lại dữ liệu.'));
    } finally {
      setBusy(false);
    }
  };

  const handleConfirm = async () => {
    if (!preview) return;
    setBusy(true);
    try {
      const res = await classApi.confirmClassImport(schoolYear.id, rowsOf(preview, mode));
      if (res.success && res.data) {
        setResult(res.data);
        setStep('result');
        toast.success(SYSTEM_MESSAGES.MSG48.content);
        if (res.data.skippedRows > 0) {
          toast.error(SYSTEM_MESSAGES.MSG49.content);
        }
        onImported();
      } else {
        toast.error(errorOf(res, SYSTEM_MESSAGES.MSG49.content));
      }
    } catch (err: any) {
      toast.error(errorOf(err, SYSTEM_MESSAGES.MSG49.content));
    } finally {
      setBusy(false);
    }
  };

  const invalidRows = preview
    ? [
        ...preview.classes.filter((r) => !r.isValid).map((r) => ({
          sheet: 'Classes',
          row: r.rowNumber,
          content: [r.className, r.grade, r.homeroomTeacherCode].filter(Boolean).join(' | '),
          errors: r.errors,
        })),
        ...preview.students.filter((r) => !r.isValid).map((r) => ({
          sheet: 'Students',
          row: r.rowNumber,
          content: [r.studentCode, r.fullName, r.className].filter(Boolean).join(' | '),
          errors: r.errors,
        })),
      ]
    : [];
  const validRows = preview ? preview.validClassRows + preview.validStudentRows : 0;
  const diff = preview?.diff;

  return (
    <GeometricModal
      open={open}
      onClose={() => !busy && onClose()}
      title="Nhập xếp lớp từ tệp"
      subtitle={`Năm học ${schoolYear.yearName} (Bản nháp)`}
      badgeText="UC-69"
      icon={<FileSpreadsheet size={22} />}
      maxWidth="xl"
    >
      {step === 'select' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          <p style={{ margin: 0, fontSize: 'var(--fs-sm)' }}>
            Chọn tệp Excel (.xlsx) theo mẫu gồm 2 trang tính: <strong>Classes</strong> (lớp và mã giáo viên chủ nhiệm) và{' '}
            <strong>Students</strong> (mã học sinh và lớp được xếp). Việc xếp lớp do nhà trường quyết định; SAWP chỉ ghi nhận
            kết quả vào năm học Bản nháp để rà soát từng lớp trước khi kích hoạt.
          </p>
          <div>
            <button type="button" className="btn btn--secondary btn--sm" onClick={handleDownloadTemplate}>
              <Download size={14} /> Tải tệp mẫu
            </button>
          </div>
          <label style={{ display: 'flex', flexDirection: 'column', gap: 6, fontWeight: 700, fontSize: 'var(--fs-sm)' }}>
            Tệp dữ liệu xếp lớp
            <input
              type="file"
              accept=".xlsx"
              aria-label="Tệp dữ liệu xếp lớp"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />
          </label>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
            <button type="button" className="btn btn--secondary" onClick={onClose} disabled={busy}>
              Hủy
            </button>
            <button type="button" className="btn btn--primary" onClick={handlePreview} disabled={busy || !file}>
              <Upload size={16} /> {busy ? 'Đang kiểm tra...' : 'Kiểm tra tệp'}
            </button>
          </div>
        </div>
      )}

      {step === 'preview' && preview && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }} data-testid="class-import-preview">
          {preview.isReimport && (
            <fieldset style={{ border: '2px solid #E2E8F0', borderRadius: 12, padding: '10px 14px' }}>
              <legend style={{ fontWeight: 800, fontSize: 'var(--fs-sm)' }}>Năm học đã có dữ liệu xếp lớp – cách áp dụng tệp</legend>
              <label style={{ display: 'flex', gap: 8, fontSize: 'var(--fs-sm)', marginBottom: 6 }}>
                <input type="radio" name="class-import-mode" checked={mode === 'Merge'} disabled={busy} onChange={() => handleModeChange('Merge')} />
                <span>
                  <strong>Gộp</strong> – chỉ áp dụng các khác biệt; học sinh có trong tệp được xếp theo tệp, các lớp và học sinh
                  khác giữ nguyên.
                </span>
              </label>
              <label style={{ display: 'flex', gap: 8, fontSize: 'var(--fs-sm)' }}>
                <input type="radio" name="class-import-mode" checked={mode === 'Replace'} disabled={busy} onChange={() => handleModeChange('Replace')} />
                <span>
                  <strong>Thay thế</strong> – bản nháp trở thành đúng như tệp; các thay đổi thủ công sẽ bị mất.
                </span>
              </label>
            </fieldset>
          )}

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 10, fontSize: 'var(--fs-sm)' }}>
            <div className="card" style={{ padding: 10 }}>
              Trang <strong>Classes</strong>: {preview.validClassRows} dòng hợp lệ, {preview.invalidClassRows} dòng lỗi
            </div>
            <div className="card" style={{ padding: 10 }}>
              Trang <strong>Students</strong>: {preview.validStudentRows} dòng hợp lệ, {preview.invalidStudentRows} dòng lỗi
            </div>
          </div>

          {preview.isReimport && diff && (
            <div data-testid="class-import-diff" style={{ fontSize: 'var(--fs-sm)' }}>
              <h4 style={{ margin: '0 0 6px' }}>Khác biệt so với bản nháp hiện tại</h4>
              <ul style={{ margin: 0, paddingLeft: 18 }}>
                <li>Lớp thêm mới: {diff.classesAdded.length ? diff.classesAdded.join(', ') : 'không có'}</li>
                <li>Lớp bị xóa: {diff.classesRemoved.length ? diff.classesRemoved.join(', ') : 'không có'}</li>
                <li>
                  Học sinh thêm vào lớp: {diff.studentsAdded.length}
                  {diff.studentsAdded.length > 0 && ` (${diff.studentsAdded.map((s) => `${s.studentCode} → ${s.toClass}`).join(', ')})`}
                </li>
                <li>
                  Học sinh chuyển lớp: {diff.studentsMoved.length}
                  {diff.studentsMoved.length > 0 && ` (${diff.studentsMoved.map((s) => `${s.studentCode}: ${s.fromClass} → ${s.toClass}`).join(', ')})`}
                </li>
                <li>
                  Học sinh bị gỡ khỏi lớp: {diff.studentsRemoved.length}
                  {diff.studentsRemoved.length > 0 && ` (${diff.studentsRemoved.map((s) => `${s.studentCode} (${s.fromClass})`).join(', ')})`}
                </li>
                <li>
                  Thay đổi giáo viên chủ nhiệm: {diff.teacherChanges.length}
                  {diff.teacherChanges.length > 0 &&
                    ` (${diff.teacherChanges.map((t) => `${t.className}: ${t.fromTeacher ?? 'chưa có'} → ${t.toTeacher ?? 'chưa có'}`).join(', ')})`}
                </li>
              </ul>
            </div>
          )}

          {preview.warnings.length > 0 && (
            <div data-testid="class-import-warnings" style={{ border: '2px solid #FCD34D', background: '#FFFBEB', borderRadius: 12, padding: '10px 14px', fontSize: 'var(--fs-sm)' }}>
              <div style={{ fontWeight: 800, display: 'flex', gap: 6, alignItems: 'center', marginBottom: 4 }}>
                <AlertTriangle size={16} /> Cảnh báo
              </div>
              <ul style={{ margin: 0, paddingLeft: 18 }}>
                {preview.warnings.map((w) => (
                  <li key={w}>{w}</li>
                ))}
              </ul>
            </div>
          )}

          <div>
            <h4 style={{ margin: '0 0 6px' }}>Danh sách lớp sau khi nhập</h4>
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }} data-testid="class-import-classes">
                <thead>
                  <tr>
                    <th style={headStyle}>Lớp</th>
                    <th style={headStyle}>Khối</th>
                    <th style={headStyle}>Sĩ số / Tối đa</th>
                    <th style={headStyle}>GV chủ nhiệm</th>
                    <th style={headStyle}>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {preview.classSummaries.map((c) => (
                    <tr key={c.className}>
                      <td style={cellStyle}><strong>{c.className}</strong></td>
                      <td style={cellStyle}>{c.grade}</td>
                      <td style={cellStyle}>{c.isRemoved ? '—' : `${c.studentCount} / ${c.capacity}`}</td>
                      <td style={cellStyle}>
                        {c.homeroomTeacherName ? `${c.homeroomTeacherName} (${c.homeroomTeacherCode})` : 'Chưa phân công'}
                      </td>
                      <td style={cellStyle}>
                        {c.isRemoved
                          ? 'Sẽ bị xóa'
                          : c.isNew
                          ? 'Lớp mới'
                          : c.isChanged
                          ? 'Có thay đổi – cần rà soát lại'
                          : 'Không thay đổi'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {invalidRows.length > 0 && (
            <div>
              <h4 style={{ margin: '0 0 6px' }}>Dòng lỗi (sẽ bị bỏ qua)</h4>
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }} data-testid="class-import-errors">
                  <thead>
                    <tr>
                      <th style={headStyle}>Trang</th>
                      <th style={headStyle}>Dòng</th>
                      <th style={headStyle}>Dữ liệu</th>
                      <th style={headStyle}>Lý do</th>
                    </tr>
                  </thead>
                  <tbody>
                    {invalidRows.map((r) => (
                      <tr key={`${r.sheet}-${r.row}`}>
                        <td style={cellStyle}>{r.sheet}</td>
                        <td style={cellStyle}>{r.row}</td>
                        <td style={cellStyle}>{r.content}</td>
                        <td style={{ ...cellStyle, color: '#B91C1C' }}>{r.errors.join(' ')}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {preview.students.some((s) => s.requiresAgeException) && (
            <label
              className="banner banner--warning"
              style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', fontSize: 'var(--fs-sm)' }}
            >
              <input
                type="checkbox"
                checked={allowAgeException}
                onChange={(e) => setAllowAgeException(e.target.checked)}
              />
              <span>
                Tệp có học sinh <strong>vượt quá 15 tuổi</strong>. Tôi xác nhận cho phép ngoại lệ quá tuổi để xếp lớp cho các học sinh này (và đồng ý ghi nhật ký hệ thống).
              </span>
            </label>
          )}

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, flexWrap: 'wrap' }}>
            <button type="button" className="btn btn--secondary" onClick={onClose} disabled={busy}>
              Hủy
            </button>
            <button type="button" className="btn btn--secondary" onClick={() => setStep('select')} disabled={busy}>
              Chọn tệp khác
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={handleConfirm}
              disabled={busy || validRows === 0 || (preview.students.some((s) => s.requiresAgeException) && !allowAgeException)}
            >
              <CheckCircle2 size={16} /> {busy ? 'Đang nhập...' : `Xác nhận nhập ${validRows} dòng hợp lệ`}
            </button>
          </div>
        </div>
      )}

      {step === 'result' && result && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12, fontSize: 'var(--fs-sm)' }} data-testid="class-import-result">
          <p style={{ margin: 0, fontWeight: 700 }}>{SYSTEM_MESSAGES.MSG48.content}</p>
          <ul style={{ margin: 0, paddingLeft: 18 }}>
            <li>Lớp tạo mới: {result.classesCreated}</li>
            {result.classesRemoved > 0 && <li>Lớp bị xóa: {result.classesRemoved}</li>}
            <li>Học sinh được xếp lớp: {result.studentsAdded}</li>
            <li>Học sinh chuyển lớp: {result.studentsMoved}</li>
            {result.studentsRemoved > 0 && <li>Học sinh bị gỡ khỏi lớp: {result.studentsRemoved}</li>}
            <li>Thay đổi giáo viên chủ nhiệm: {result.teacherChanges}</li>
            <li>Lớp cần rà soát: {result.classesToReview}</li>
            <li>Dòng bị bỏ qua: {result.skippedRows}</li>
          </ul>
          {result.errors.length > 0 && (
            <div style={{ border: '2px solid #FECACA', background: '#FEF2F2', borderRadius: 12, padding: '10px 14px' }}>
              <div style={{ fontWeight: 800, marginBottom: 4 }}>{SYSTEM_MESSAGES.MSG49.content}</div>
              <ul style={{ margin: 0, paddingLeft: 18 }}>
                {result.errors.map((e) => (
                  <li key={e}>{e}</li>
                ))}
              </ul>
            </div>
          )}
          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <button type="button" className="btn btn--primary" onClick={onClose}>
              Đóng
            </button>
          </div>
        </div>
      )}
    </GeometricModal>
  );
};
