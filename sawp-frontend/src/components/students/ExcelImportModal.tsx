import React, { useRef, useState } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  FileSpreadsheet,
  UploadCloud,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { useNavigate } from 'react-router';
import { GeometricModal } from '../common/GeometricModal';
import { studentApi } from '../../services/implementations/studentApi';
import { formatDDMMYYYY } from '../../utils/date';
import type { ImportStudentRowDto, ImportStudentsResultDto } from '../../types/student.types';

interface ExcelImportModalProps {
  open: boolean;
  onClose: () => void;
  onImportCompleted: (result: ImportStudentsResultDto) => void;
}

/** UC-09 step 3: whether a new guardian record is created or an existing one reused, and account eligibility (A4). */
const GuardianMatchBadge: React.FC<{ row: ImportStudentRowDto }> = ({ row }) => {
  if (!row.guardianName || !row.guardianMatch || row.guardianMatch === 'None') return <>-</>;
  if (row.guardianMatch === 'Conflict') {
    return (
      <span className="badge badge--pink" title={row.guardianMatchNote}>
        Xung đột hồ sơ
      </span>
    );
  }
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4, alignItems: 'flex-start' }}>
      <span className="badge badge--violet" title={row.guardianMatchNote}>
        {row.guardianMatch === 'Reused' ? 'Dùng hồ sơ có sẵn' : 'Tạo hồ sơ mới'}
      </span>
      {!row.guardianEligibleForAccount && (
        <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
          {row.guardianEmail ? 'Đã có tài khoản' : 'Chưa có email'}
        </span>
      )}
    </div>
  );
};

export const ExcelImportModal: React.FC<ExcelImportModalProps> = ({
  open,
  onClose,
  onImportCompleted,
}) => {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [parsedRows, setParsedRows] = useState<ImportStudentRowDto[]>([]);
  const [parsing, setParsing] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [importResult, setImportResult] = useState<ImportStudentsResultDto | null>(null);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.name.endsWith('.xlsx')) {
      toast.error('Vui lòng chọn tệp Excel định dạng .xlsx!');
      return;
    }

    setSelectedFile(file);
    setParsing(true);
    try {
      const res = await studentApi.parseExcel(file);
      if (res.success && res.data) {
        setParsedRows(res.data);
        toast.success(`Đã phân tích ${res.data.length} dòng dữ liệu từ Excel!`);
      } else {
        toast.error(res.message || 'Lỗi khi đọc file Excel.');
      }
    } catch (err: any) {
      const msg =
        err.response?.data?.errors?.[0] ||
        err.response?.data?.message ||
        'Không thể xử lý tệp Excel này.';
      toast.error(msg);
    } finally {
      setParsing(false);
    }
  };

  // Start clean next time the modal opens
  const handleClose = () => {
    setSelectedFile(null);
    setParsedRows([]);
    setImportResult(null);
    if (fileInputRef.current) fileInputRef.current.value = '';
    onClose();
  };

  const validRows = parsedRows.filter((r) => r.isValid && !r.isDuplicate);
  const duplicateRows = parsedRows.filter((r) => r.isDuplicate);
  const invalidRows = parsedRows.filter((r) => !r.isValid);

  const handleConfirmImport = async () => {
    if (validRows.length === 0) {
      toast.error('Không có bản ghi hợp lệ nào để nhập!');
      return;
    }

    setConfirming(true);
    try {
      const res = await studentApi.confirmImport({ validRows });
      if (res.success && res.data) {
        toast.success(res.message || `Nhập thành công ${res.data.successCount} học sinh mới vào hệ thống!`);
        // UC-09 step 6: show the totals and the error list before closing
        setImportResult(res.data);
        onImportCompleted(res.data);
      } else {
        toast.error(res.message || 'Lỗi khi nhập dữ liệu.');
      }
    } catch (err: any) {
      const msg =
        err.response?.data?.errors?.[0] ||
        err.response?.data?.message ||
        'Lỗi khi xác nhận nhập dữ liệu.';
      toast.error(msg);
    } finally {
      setConfirming(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={handleClose}
      title="Nhập danh sách học sinh từ Excel"
      badgeText="Nhập liệu hàng loạt"
      badgeColor="amber"
      icon={<FileSpreadsheet size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-tertiary)"
      maxWidth="xl"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Upload Drop Zone */}
        <div
          className="card"
          style={{
            borderStyle: 'dashed',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            padding: '24px 16px',
            cursor: 'pointer',
            backgroundColor: selectedFile ? '#fffdf5' : 'var(--color-card)',
          }}
          onClick={() => fileInputRef.current?.click()}
        >
          <input
            type="file"
            ref={fileInputRef}
            style={{ display: 'none' }}
            accept=".xlsx"
            onChange={handleFileChange}
          />
          <UploadCloud size={40} color="var(--color-accent)" style={{ marginBottom: 8 }} />
          <div style={{ fontWeight: 700, fontSize: 'var(--fs-base)' }}>
            {selectedFile ? selectedFile.name : 'Bấm vào đây để chọn tệp Excel (.xlsx)'}
          </div>
          <p style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', margin: '4px 0 0' }}>
            Hệ thống sẽ tự động quét trùng mã định danh học sinh và kiểm tra tính hợp lệ của từng dòng.
          </p>
        </div>

        {/* Summary stats if parsed */}
        {parsedRows.length > 0 && (
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <span className="badge badge--violet">
              Tổng số dòng: <strong>{parsedRows.length}</strong>
            </span>
            <span className="badge badge--mint">
              <CheckCircle2 size={14} /> Hợp lệ sẵn sàng nhập: <strong>{validRows.length}</strong>
            </span>
            {duplicateRows.length > 0 && (
              <span className="badge badge--pink" title="Mã học sinh đã tồn tại trong cơ sở dữ liệu hệ thống">
                <AlertCircle size={14} /> Đã có trong hệ thống: <strong>{duplicateRows.length}</strong>
              </span>
            )}
            {invalidRows.length > 0 && (
              <span className="badge badge--amber">
                <AlertCircle size={14} /> Thiếu dữ liệu: <strong>{invalidRows.length}</strong>
              </span>
            )}
          </div>
        )}

        {/* Parsed Preview Table */}
        {parsedRows.length > 0 && (
          <div className="data-table-wrapper" style={{ maxHeight: 320 }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Dòng</th>
                  <th>Mã HS</th>
                  <th>Họ và tên</th>
                  <th>Ngày sinh</th>
                  <th>Giới tính</th>
                  <th>Lớp dự kiến</th>
                  <th>Phụ huynh</th>
                  <th>Hồ sơ phụ huynh</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {parsedRows.map((r) => (
                  <tr
                    key={r.rowNumber}
                    style={{
                      backgroundColor: r.isDuplicate
                        ? 'var(--color-secondary-soft)'
                        : !r.isValid
                        ? 'var(--color-tertiary-soft)'
                        : undefined,
                    }}
                  >
                    <td>{r.rowNumber}</td>
                    <td style={{ fontWeight: 700 }}>{r.studentCode}</td>
                    <td>{r.fullName}</td>
                    <td>{formatDDMMYYYY(r.dateOfBirth)}</td>
                    <td>{r.gender}</td>
                    <td>{r.className || '-'}</td>
                    <td>
                      {r.guardianName ? (
                        <>
                          {r.guardianName}
                          {r.guardianPhone ? ` (${r.guardianPhone})` : ''}
                          {r.guardianEmail && (
                            <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>{r.guardianEmail}</div>
                          )}
                        </>
                      ) : (
                        '-'
                      )}
                    </td>
                    <td>
                      <GuardianMatchBadge row={r} />
                    </td>
                    <td>
                      {r.isDuplicate ? (
                        <span className="badge badge--pink" title={r.errorMessage || 'Mã học sinh đã tồn tại trong cơ sở dữ liệu hệ thống'}>
                          {r.errorMessage?.includes('tệp Excel') ? 'Trùng trong file' : 'Đã có trong hệ thống'}
                        </span>
                      ) : !r.isValid ? (
                        <span className="badge badge--amber" title={r.errorMessage}>
                          Không hợp lệ
                        </span>
                      ) : (
                        <span className="badge badge--mint">Hợp lệ</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {importResult && (
          <div className="card" role="status" style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <strong>Kết quả nhập dữ liệu</strong>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <span className="badge badge--mint">Học sinh đã nhập: {importResult.successCount}</span>
              {importResult.skippedCount > 0 && <span className="badge badge--amber">Bỏ qua: {importResult.skippedCount}</span>}
              <span className="badge badge--violet">Hồ sơ phụ huynh mới: {importResult.guardiansCreated}</span>
              <span className="badge badge--violet">Dùng hồ sơ có sẵn: {importResult.guardiansReused}</span>
              {importResult.guardiansWithoutEmail > 0 && (
                <span className="badge badge--pink">Chưa có email (chưa đủ điều kiện cấp tài khoản): {importResult.guardiansWithoutEmail}</span>
              )}
            </div>
            <p style={{ margin: 0, fontSize: 'var(--fs-sm)' }}>
              Không tạo tài khoản người dùng nào. {importResult.guardiansEligibleForAccountIds.length} phụ huynh có email và chưa có
              tài khoản, có thể được yêu cầu cấp tài khoản.
            </p>
            {importResult.guardiansEligibleForAccountIds.length > 0 && (
              <div>
                {/* UC-09 A5: open an account request pre-filled with these guardians, linked to this import */}
                <button
                  id="btn-request-accounts"
                  type="button"
                  className="btn btn--primary btn--sm"
                  onClick={() => {
                    const ids = importResult.guardiansEligibleForAccountIds.join(',');
                    const ref = encodeURIComponent(selectedFile?.name ?? 'import');
                    handleClose();
                    navigate(`/office/account-requests?guardianIds=${ids}&source=Import&ref=${ref}`);
                  }}
                >
                  Yêu cầu cấp tài khoản
                </button>
              </div>
            )}
            {importResult.errors && importResult.errors.length > 0 && (
              <ul style={{ margin: 0, paddingLeft: 18, fontSize: 'var(--fs-sm)', color: '#B45309' }}>
                {importResult.errors.map((e, i) => (
                  <li key={i}>{e}</li>
                ))}
              </ul>
            )}
          </div>
        )}

        {/* Action Buttons */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
          <button type="button" className="btn btn--secondary" onClick={handleClose} disabled={confirming}>
            Đóng
          </button>
          <button
            type="button"
            className="btn btn--primary"
            onClick={handleConfirmImport}
            disabled={validRows.length === 0 || confirming || parsing || importResult !== null}
          >
            {confirming ? 'Đang nhập...' : `Xác nhận nhập ${validRows.length} học sinh hợp lệ`}
          </button>
        </div>
      </div>
    </GeometricModal>
  );
};
