import React, { useState } from 'react';
import { AlertTriangle, ArrowRight, CheckCircle2, CopyCheck, RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { studentApi } from '../../services/implementations/studentApi';
import type { CreateStudentDto, StudentProfileDto } from '../../types/student.types';

interface DuplicateResolveModalProps {
  open: boolean;
  onClose: () => void;
  pendingStudent: CreateStudentDto | null;
  existingStudent: StudentProfileDto | null;
  onResolved: (student: StudentProfileDto) => void;
}

export const DuplicateResolveModal: React.FC<DuplicateResolveModalProps> = ({
  open,
  onClose,
  pendingStudent,
  existingStudent,
  onResolved,
}) => {
  const [action, setAction] = useState<'Merge' | 'Overwrite' | 'Rename'>('Merge');
  const [newCode, setNewCode] = useState('');
  const [loading, setLoading] = useState(false);

  if (!pendingStudent || !existingStudent) return null;

  const handleResolve = async () => {
    if (action === 'Rename' && !newCode.trim()) {
      toast.error('Vui lòng nhập mã học sinh mới!');
      return;
    }

    setLoading(true);
    try {
      const res = await studentApi.resolveDuplicate({
        resolutionAction: action,
        existingStudentId: existingStudent.id,
        newStudentCode: action === 'Rename' ? newCode.trim().toUpperCase() : undefined,
        newStudentData: pendingStudent,
      });

      if (res.success && res.data) {
        toast.success(res.message || 'Xử lý trùng lặp hồ sơ thành công!');
        onResolved(res.data);
        onClose();
      } else {
        toast.error(res.message || 'Không thể xử lý trùng lặp.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi xử lý trùng lặp.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Phát hiện trùng mã định danh (MSG12)"
      badgeText="Xung đột dữ liệu"
      badgeColor="pink"
      icon={<AlertTriangle size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-secondary)"
      maxWidth="md"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div className="banner banner--danger" style={{ fontSize: 'var(--fs-sm)' }}>
          <div>
            Mã học sinh <strong>{pendingStudent.studentCode}</strong> đã tồn tại trong hệ thống với tên{' '}
            <strong>{existingStudent.fullName}</strong> (Trạng thái: {existingStudent.status}).
            Vui lòng chọn 1 phương án giải quyết:
          </div>
        </div>

        {/* Options */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {/* Option 1: Merge */}
          <label
            className="card card--hover"
            style={{
              display: 'flex',
              alignItems: 'flex-start',
              gap: 12,
              padding: 14,
              cursor: 'pointer',
              borderColor: action === 'Merge' ? 'var(--color-accent)' : undefined,
              boxShadow: action === 'Merge' ? '4px 4px 0 0 var(--color-accent)' : undefined,
            }}
          >
            <input
              type="radio"
              name="resolveAction"
              checked={action === 'Merge'}
              onChange={() => setAction('Merge')}
              style={{ marginTop: 4 }}
            />
            <div>
              <div style={{ fontWeight: 700, display: 'flex', alignItems: 'center', gap: 6 }}>
                <CopyCheck size={16} color="var(--color-accent)" /> Hợp nhất hồ sơ (Merge)
                <span className="badge badge--violet badge--sm">Khuyên dùng</span>
              </div>
              <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 4 }}>
                Giữ nguyên hồ sơ gốc, bổ sung thông tin phụ huynh và cập nhật phân lớp mới nếu chưa có.
              </div>
            </div>
          </label>

          {/* Option 2: Overwrite */}
          <label
            className="card card--hover"
            style={{
              display: 'flex',
              alignItems: 'flex-start',
              gap: 12,
              padding: 14,
              cursor: 'pointer',
              borderColor: action === 'Overwrite' ? 'var(--color-secondary)' : undefined,
              boxShadow: action === 'Overwrite' ? '4px 4px 0 0 var(--color-secondary)' : undefined,
            }}
          >
            <input
              type="radio"
              name="resolveAction"
              checked={action === 'Overwrite'}
              onChange={() => setAction('Overwrite')}
              style={{ marginTop: 4 }}
            />
            <div>
              <div style={{ fontWeight: 700, display: 'flex', alignItems: 'center', gap: 6 }}>
                <RefreshCw size={16} color="var(--color-secondary)" /> Ghi đè thông tin (Overwrite)
              </div>
              <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 4 }}>
                Thay thế toàn bộ họ tên, ngày sinh, giới tính của học sinh cũ bằng thông tin mới nhập.
              </div>
            </div>
          </label>

          {/* Option 3: Rename */}
          <label
            className="card card--hover"
            style={{
              display: 'flex',
              alignItems: 'flex-start',
              gap: 12,
              padding: 14,
              cursor: 'pointer',
              borderColor: action === 'Rename' ? 'var(--color-tertiary)' : undefined,
              boxShadow: action === 'Rename' ? '4px 4px 0 0 var(--color-tertiary)' : undefined,
            }}
          >
            <input
              type="radio"
              name="resolveAction"
              checked={action === 'Rename'}
              onChange={() => setAction('Rename')}
              style={{ marginTop: 4 }}
            />
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 700, display: 'flex', alignItems: 'center', gap: 6 }}>
                <CheckCircle2 size={16} color="var(--color-tertiary)" /> Đổi mã học sinh mới (Rename)
              </div>
              <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 4 }}>
                Tạo một hồ sơ độc lập hoàn toàn với mã học sinh khác.
              </div>
              {action === 'Rename' && (
                <div style={{ marginTop: 10 }}>
                  <input
                    type="text"
                    className="input input--sm"
                    placeholder="Nhập mã học sinh mới (VD: HS2026999)"
                    value={newCode}
                    onChange={(e) => setNewCode(e.target.value.toUpperCase())}
                    autoFocus
                  />
                </div>
              )}
            </div>
          </label>
        </div>

        {/* Action Buttons */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 12 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={loading}>
            Hủy bỏ
          </button>
          <button
            type="button"
            className="btn btn--primary"
            onClick={handleResolve}
            disabled={loading}
          >
            {loading ? 'Đang xử lý...' : 'Xác nhận xử lý'}
            <ArrowRight size={16} />
          </button>
        </div>
      </div>
    </GeometricModal>
  );
};
