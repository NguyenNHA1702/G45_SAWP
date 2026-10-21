import { useState } from 'react';
import { AlertCircle, Check, UserPlus } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { studentApi } from '../../services/implementations/studentApi';
import type { CreateStudentDto, StudentProfileDto } from '../../types/student.types';
import type { ClassSummaryDto } from '../../types/class.types';

interface CreateStudentModalProps {
  open: boolean;
  onClose: () => void;
  classes: ClassSummaryDto[];
  onCreated: (student: StudentProfileDto) => void;
  onDuplicateDetected: (pending: CreateStudentDto, existing: StudentProfileDto) => void;
}

export const CreateStudentModal: React.FC<CreateStudentModalProps> = ({
  open,
  onClose,
  classes,
  onCreated,
  onDuplicateDetected,
}) => {
  const [formData, setFormData] = useState<CreateStudentDto>({
    studentCode: '',
    fullName: '',
    dateOfBirth: '2020-01-01',
    gender: 'Nam',
    address: '',
    classId: undefined,
    parentFullName: '',
    parentPhone: '',
    parentIdentityCard: '',
    parentRelationship: 'Bố',
  });

  const [loading, setLoading] = useState(false);
  const [dupChecking, setDupChecking] = useState(false);
  const [dupWarning, setDupWarning] = useState<string | null>(null);

  const checkCodeDuplicate = async (code: string) => {
    if (!code.trim()) return;
    setDupChecking(true);
    setDupWarning(null);
    try {
      const res = await studentApi.checkDuplicate(code.trim().toUpperCase());
      if (res.data?.isDuplicate) {
        setDupWarning(res.data.message || 'Mã học sinh đã tồn tại!');
      }
    } catch {
      // ignore
    } finally {
      setDupChecking(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.studentCode.trim() || !formData.fullName.trim()) {
      toast.error('Vui lòng điền đầy đủ Mã học sinh và Họ tên!');
      return;
    }

    setLoading(true);
    try {
      const res = await studentApi.createStudent({
        ...formData,
        studentCode: formData.studentCode.trim().toUpperCase(),
        fullName: formData.fullName.trim(),
      });

      if (res.success && res.data) {
        toast.success(res.message || 'Tạo hồ sơ học sinh thành công!');
        onCreated(res.data);
        onClose();
      } else {
        toast.error(res.message || 'Tạo hồ sơ thất bại.');
      }
    } catch (err: any) {
      // 409 Conflict - MSG12 duplicate detection
      if (err.response?.status === 409) {
        toast.error('Mã học sinh đã tồn tại (MSG12). Chuyển sang màn hình giải quyết trùng lặp.');
        // fetch existing info
        const dupRes = await studentApi.checkDuplicate(formData.studentCode.trim().toUpperCase());
        if (dupRes.data?.existingStudent) {
          onDuplicateDetected(formData, dupRes.data.existingStudent);
          onClose();
        }
      } else {
        toast.error(err.response?.data?.message || 'Lỗi khi tạo hồ sơ học sinh.');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Thêm hồ sơ học sinh mới"
      badgeText="Tạo mới hồ sơ"
      badgeColor="mint"
      icon={<UserPlus size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-quaternary)"
      maxWidth="lg"
    >
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Section 1: Thông tin học sinh */}
        <div>
          <h4 style={{ fontSize: 'var(--fs-sm)', textTransform: 'uppercase', color: 'var(--color-accent)', marginBottom: 12 }}>
            1. Thông tin học sinh
          </h4>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            <div className="field">
              <label className="label">Mã học sinh (*)</label>
              <input
                type="text"
                className="input"
                placeholder="VD: HS2026001"
                required
                value={formData.studentCode}
                onChange={(e) => {
                  setFormData({ ...formData, studentCode: e.target.value.toUpperCase() });
                  setDupWarning(null);
                }}
                onBlur={(e) => checkCodeDuplicate(e.target.value)}
              />
              {dupChecking && <small style={{ color: 'var(--color-muted-fg)' }}>Đang kiểm tra trùng lặp...</small>}
              {dupWarning && (
                <div style={{ color: 'var(--color-secondary)', fontSize: 'var(--fs-xs)', display: 'flex', alignItems: 'center', gap: 4, marginTop: 4 }}>
                  <AlertCircle size={14} /> {dupWarning}
                </div>
              )}
            </div>

            <div className="field">
              <label className="label">Họ và tên (*)</label>
              <input
                type="text"
                className="input"
                placeholder="Nguyễn Văn An"
                required
                value={formData.fullName}
                onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
              />
            </div>

            <div className="field">
              <label className="label">Ngày sinh (*)</label>
              <input
                type="date"
                className="input"
                required
                value={formData.dateOfBirth}
                onChange={(e) => setFormData({ ...formData, dateOfBirth: e.target.value })}
              />
            </div>

            <div className="field">
              <label className="label">Giới tính</label>
              <select
                className="select"
                value={formData.gender}
                onChange={(e) => setFormData({ ...formData, gender: e.target.value })}
              >
                <option value="Nam">Nam</option>
                <option value="Nữ">Nữ</option>
                <option value="Khác">Khác</option>
              </select>
            </div>

            <div className="field" style={{ gridColumn: 'span 2' }}>
              <label className="label">Địa chỉ thường trú</label>
              <input
                type="text"
                className="input"
                placeholder="Số nhà, đường, phường/xã, quận/huyện..."
                value={formData.address || ''}
                onChange={(e) => setFormData({ ...formData, address: e.target.value })}
              />
            </div>

            <div className="field" style={{ gridColumn: 'span 2' }}>
              <label className="label">Xếp lớp dự kiến (Tùy chọn)</label>
              <select
                className="select"
                value={formData.classId || ''}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    classId: e.target.value ? Number(e.target.value) : undefined,
                  })
                }
              >
                <option value="">-- Chưa xếp lớp (Xếp lớp sau) --</option>
                {classes.map((c) => (
                  <option key={c.id} value={c.id}>
                    Lớp {c.className} (Khối {c.grade}) - Hiện có {c.enrolledStudentsCount}/{c.capacity} học sinh
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>

        {/* Section 2: Thông tin phụ huynh */}
        <div style={{ borderTop: '2px dashed var(--color-border)', paddingTop: 14 }}>
          <h4 style={{ fontSize: 'var(--fs-sm)', textTransform: 'uppercase', color: 'var(--color-secondary)', marginBottom: 12 }}>
            2. Thông tin phụ huynh / Người giám hộ
          </h4>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            <div className="field">
              <label className="label">Họ tên phụ huynh</label>
              <input
                type="text"
                className="input"
                placeholder="Nguyễn Văn Nam"
                value={formData.parentFullName || ''}
                onChange={(e) => setFormData({ ...formData, parentFullName: e.target.value })}
              />
            </div>

            <div className="field">
              <label className="label">Số điện thoại liên hệ</label>
              <input
                type="tel"
                className="input"
                placeholder="0912345678"
                value={formData.parentPhone || ''}
                onChange={(e) => setFormData({ ...formData, parentPhone: e.target.value })}
              />
            </div>

            <div className="field">
              <label className="label">Số CCCD / CMND phụ huynh</label>
              <input
                type="text"
                className="input"
                placeholder="001200123456"
                value={formData.parentIdentityCard || ''}
                onChange={(e) => setFormData({ ...formData, parentIdentityCard: e.target.value })}
              />
            </div>

            <div className="field">
              <label className="label">Mối quan hệ</label>
              <select
                className="select"
                value={formData.parentRelationship || 'Bố'}
                onChange={(e) => setFormData({ ...formData, parentRelationship: e.target.value })}
              >
                <option value="Bố">Bố</option>
                <option value="Mẹ">Mẹ</option>
                <option value="Người giám hộ">Người giám hộ</option>
              </select>
            </div>
          </div>
        </div>

        {/* Action Buttons */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 10 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={loading}>
            Hủy
          </button>
          <button type="submit" className="btn btn--primary" disabled={loading}>
            {loading ? 'Đang tạo...' : 'Lưu hồ sơ học sinh'}
            <Check size={18} />
          </button>
        </div>
      </form>
    </GeometricModal>
  );
};
