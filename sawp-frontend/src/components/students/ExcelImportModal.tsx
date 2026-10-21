import React, { useRef, useState } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Download,
  FileSpreadsheet,
  UploadCloud,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { studentApi } from '../../services/implementations/studentApi';
import type { ImportStudentRowDto, ImportStudentsResultDto } from '../../types/student.types';

interface ExcelImportModalProps {
  open: boolean;
  onClose: () => void;
  onImportCompleted: (result: ImportStudentsResultDto) => void;
}

export const ExcelImportModal: React.FC<ExcelImportModalProps> = ({
  open,
  onClose,
  onImportCompleted,
}) => {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [parsedRows, setParsedRows] = useState<ImportStudentRowDto[]>([]);
  const [parsing, setParsing] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [downloading, setDownloading] = useState(false);

  const handleDownloadTemplate = async () => {
    setDownloading(true);
    try {
      const blob = await studentApi.downloadTemplate();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'Mau_Danh_Sach_Hoc_Sinh_SAWP.xlsx';
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
      toast.success('Đã tải tệp mẫu Excel thành công!');
    } catch {
      toast.error('Lỗi khi tải tệp mẫu Excel.');
    } finally {
      setDownloading(false);
    }
  };

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
      toast.error(err.response?.data?.message || 'Không thể xử lý tệp Excel này.');
    } finally {
      setParsing(false);
    }
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
        toast.success(
          `Nhập thành công ${res.data.successCount} học sinh mới vào hệ thống!`
        );
        onImportCompleted(res.data);
        onClose();
      } else {
        toast.error(res.message || 'Lỗi khi nhập dữ liệu.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi xác nhận nhập dữ liệu.');
    } finally {
      setConfirming(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Nhập danh sách học sinh từ Excel"
      badgeText="Nhập liệu hàng loạt"
      badgeColor="amber"
      icon={<FileSpreadsheet size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-tertiary)"
      maxWidth="xl"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Step 1: Download template & upload file */}
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            background: 'var(--color-muted)',
            padding: '14px 18px',
            borderRadius: 'var(--radius-md)',
            border: 'var(--border)',
            flexWrap: 'wrap',
            gap: 12,
          }}
        >
          <div>
            <div style={{ fontWeight: 700, fontSize: 'var(--fs-sm)' }}>
              1. Tải tệp mẫu & Chuẩn bị dữ liệu
            </div>
            <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
              Điền danh sách học sinh theo đúng cấu trúc cột để hệ thống tự động nhận diện.
            </div>
          </div>
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={handleDownloadTemplate}
            disabled={downloading}
          >
            <Download size={16} />
            {downloading ? 'Đang tải...' : 'Tải tệp mẫu (.xlsx)'}
          </button>
        </div>

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
            Hệ thống sẽ tự động quét trùng mã định danh (MSG12) và kiểm tra tính hợp lệ của từng dòng.
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
              <span className="badge badge--pink">
                <AlertCircle size={14} /> Trùng lặp (MSG12): <strong>{duplicateRows.length}</strong>
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
                    <td>{r.dateOfBirth}</td>
                    <td>{r.gender}</td>
                    <td>{r.className || '-'}</td>
                    <td>
                      {r.guardianName ? `${r.guardianName} (${r.guardianPhone || ''})` : '-'}
                    </td>
                    <td>
                      {r.isDuplicate ? (
                        <span className="badge badge--pink" title={r.errorMessage}>
                          Trùng mã (MSG12)
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

        {/* Action Buttons */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={confirming}>
            Đóng
          </button>
          <button
            type="button"
            className="btn btn--primary"
            onClick={handleConfirmImport}
            disabled={validRows.length === 0 || confirming || parsing}
          >
            {confirming ? 'Đang nhập...' : `Xác nhận nhập ${validRows.length} học sinh hợp lệ`}
          </button>
        </div>
      </div>
    </GeometricModal>
  );
};
