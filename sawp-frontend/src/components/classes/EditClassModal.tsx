import React, { useEffect, useState } from 'react';
import { Check, Pencil } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { classApi } from '../../services/implementations/classApi';
import type { ClassSummaryDto } from '../../types/class.types';

interface EditClassModalProps {
  open: boolean;
  onClose: () => void;
  targetClass: ClassSummaryDto | null;
  onUpdated: (updated: ClassSummaryDto) => void;
}

export const EditClassModal: React.FC<EditClassModalProps> = ({
  open,
  onClose,
  targetClass,
  onUpdated,
}) => {
  const [grade, setGrade] = useState<number>(1);
  const [className, setClassName] = useState<string>('');
  const [roomNumber, setRoomNumber] = useState<string>('');
  const [capacity, setCapacity] = useState<number>(35);
  const [status, setStatus] = useState<string>('Active');
  const [loading, setLoading] = useState<boolean>(false);

  useEffect(() => {
    if (open && targetClass) {
      setGrade(targetClass.grade || 1);
      setClassName(targetClass.className || '');
      setRoomNumber(targetClass.roomNumber || '');
      setCapacity(targetClass.capacity || 35);
      setStatus(targetClass.status || 'Active');
    }
  }, [open, targetClass]);

  if (!targetClass) return null;

  const enrolled = targetClass.enrolledCount ?? targetClass.enrolledStudentsCount ?? 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmedName = className.trim().toUpperCase();
    if (!trimmedName) {
      toast.error('Vui lòng nhập tên lớp học!');
      return;
    }
    if (trimmedName.includes('<') || trimmedName.includes('>')) {
      toast.error('Tên lớp học không được chứa thẻ HTML hoặc ký tự đặc biệt!');
      return;
    }
    if (!new RegExp(`^${grade}[A-Z]$`).test(trimmedName)) {
      toast.error(`Tên lớp '${trimmedName}' không khớp với Khối ${grade}. Quy tắc: Khối (${grade}) + 1 chữ cái (VD: ${grade}A).`);
      return;
    }
    if (roomNumber.includes('<') || roomNumber.includes('>')) {
      toast.error('Số phòng học không được chứa thẻ HTML hoặc ký tự "<", ">"!');
      return;
    }
    if (roomNumber.trim().length > 50) {
      toast.error('Số phòng học không được vượt quá 50 ký tự!');
      return;
    }
    if (Number.isNaN(capacity) || capacity < 25 || capacity > 40) {
      toast.error('Sĩ số tối đa mỗi lớp phải từ 25 đến 40 học sinh (BR-15).');
      return;
    }
    if (capacity < enrolled) {
      toast.error(`Không thể giảm sĩ số tối đa (${capacity}) nhỏ hơn số học sinh hiện tại (${enrolled}).`);
      return;
    }

    setLoading(true);
    try {
      const res = await classApi.updateClass(targetClass.id, {
        className: trimmedName,
        grade,
        roomNumber: roomNumber.trim() || undefined,
        capacity,
        status,
      });
      if (res.success && res.data) {
        toast.success(`Đã cập nhật Lớp ${res.data.className} thành công!`);
        onUpdated(res.data);
        onClose();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể cập nhật lớp học.');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.errors?.[0] || err?.response?.data?.message || 'Lỗi khi cập nhật lớp học.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title={`Chỉnh sửa Lớp ${targetClass.className}`}
      badgeText={`Năm học ${targetClass.schoolYearName}`}
      badgeColor="amber"
      icon={<Pencil size={22} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-tertiary-soft)"
      maxWidth="md"
    >
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
          <div className="field">
            <label className="label">Khối học (*)</label>
            <select
              className="select"
              value={grade}
              onChange={(e) => setGrade(Number(e.target.value))}
            >
              {[1, 2, 3, 4, 5].map((g) => (
                <option key={g} value={g}>
                  Khối {g}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label className="label">Tên lớp (*)</label>
            <input
              type="text"
              className="input"
              required
              placeholder={`VD: ${grade}A`}
              value={className}
              onChange={(e) => setClassName(e.target.value.toUpperCase())}
            />
          </div>

          <div className="field">
            <label className="label">Phòng học</label>
            <input
              type="text"
              className="input"
              placeholder="VD: Phòng 101"
              maxLength={50}
              value={roomNumber}
              onChange={(e) => setRoomNumber(e.target.value)}
            />
          </div>

          <div className="field">
            <label className="label">Sĩ số tối đa (25–40)</label>
            <input
              type="number"
              className="input"
              min={25}
              max={40}
              required
              value={capacity}
              onChange={(e) => setCapacity(Number(e.target.value))}
            />
            {enrolled > 0 && (
              <small style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>
                Hiện có {enrolled} học sinh.
              </small>
            )}
          </div>

          <div className="field" style={{ gridColumn: 'span 2' }}>
            <label className="label">Trạng thái lớp học</label>
            <select
              className="select"
              value={status}
              onChange={(e) => setStatus(e.target.value)}
            >
              <option value="Active">Hoạt động (Active)</option>
              <option value="Inactive">Ngừng hoạt động (Inactive)</option>
            </select>
          </div>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={loading}>
            Hủy
          </button>
          <button type="submit" className="btn btn--primary" disabled={loading}>
            <Check size={16} /> {loading ? 'Đang lưu...' : 'Lưu thay đổi'}
          </button>
        </div>
      </form>
    </GeometricModal>
  );
};
