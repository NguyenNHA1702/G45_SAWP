import React, { useState } from 'react';
import { Check, PlusCircle, Sparkles } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { classApi } from '../../services/implementations/classApi';
import type { ClassSummaryDto, SchoolYearDto } from '../../types/class.types';

interface CreateClassModalProps {
  open: boolean;
  onClose: () => void;
  activeSchoolYear: SchoolYearDto | null;
  initialGrade?: number;
  onClassCreated: (newClass: ClassSummaryDto, grade: number) => void;
}

export const CreateClassModal: React.FC<CreateClassModalProps> = ({
  open,
  onClose,
  activeSchoolYear,
  initialGrade = 1,
  onClassCreated,
}) => {
  const [grade, setGrade] = useState<number>(initialGrade || 1);
  const [className, setClassName] = useState<string>('');
  const [roomNumber, setRoomNumber] = useState<string>('');
  const [capacity, setCapacity] = useState<number>(35);
  const [loading, setLoading] = useState<boolean>(false);

  // Auto suggest room and class name prefix when grade changes
  const handleGradeChange = (newGrade: number) => {
    setGrade(newGrade);
    if (!className || className.length <= 2) {
      setClassName(`${newGrade}D`);
      setRoomNumber(`Phòng ${newGrade}0D`);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmedName = className.trim().toUpperCase();

    if (!trimmedName) {
      toast.error('Vui lòng nhập tên lớp học (Ví dụ: 4D)!');
      return;
    }

    if (trimmedName.includes('<') || trimmedName.includes('>')) {
      toast.error('Tên lớp học không được chứa thẻ HTML hoặc ký tự đặc biệt!');
      return;
    }

    if (!new RegExp(`^${grade}[A-Z]$`).test(trimmedName)) {
      toast.error(`Tên lớp '${trimmedName}' không khớp với Khối ${grade}. Quy tắc đặt tên: Khối (${grade}) + 1 chữ cái phân lớp (VD: ${grade}A, ${grade}B).`);
      return;
    }

    if (roomNumber.includes('<') || roomNumber.includes('>')) {
      toast.error('Tên phòng học không được chứa thẻ HTML hoặc ký tự "<", ">"!');
      return;
    }

    if (roomNumber.trim().length > 50) {
      toast.error('Số phòng học không được vượt quá 50 ký tự!');
      return;
    }

    if (isNaN(capacity) || capacity < 25 || capacity > 40) {
      toast.error('Sĩ số mỗi lớp phải từ 25 đến 40 học sinh (Quy chuẩn thông thường: 30 - 35 em/lớp).');
      return;
    }

    if (!activeSchoolYear?.id) {
      toast.error('Không tìm thấy năm học hợp lệ.');
      return;
    }

    setLoading(true);
    try {
      const res = await classApi.createClass({
        schoolYearId: activeSchoolYear.id,
        className: trimmedName,
        grade,
        roomNumber: roomNumber.trim() || `Phòng ${trimmedName}`,
        capacity,
      });

      if (res.success && res.data) {
        toast.success(`Đã tạo lớp '${res.data.className}' thành công!`);
        onClassCreated(res.data, grade);
        onClose();
        setClassName('');
        setRoomNumber('');
      } else {
        toast.error(res.message || 'Không thể tạo lớp học.');
      }
    } catch (err: any) {
      const message =
        err?.response?.data?.errors?.[0] ||
        err?.response?.data?.message ||
        'Lỗi khi tạo lớp học mới.';
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Thêm lớp học đơn lẻ"
      badgeText="Tạo lớp mới"
      badgeColor="violet"
      icon={<PlusCircle size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-accent-soft)"
      maxWidth="md"
    >
      <form noValidate onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div className="banner banner--info" style={{ fontSize: 'var(--fs-sm)' }}>
          <Sparkles size={18} color="var(--color-accent)" style={{ flexShrink: 0, marginTop: 2 }} />
          <div>
            Tạo một lớp học mới trong Năm học{' '}
            <strong>{activeSchoolYear?.yearName ?? 'hiện tại'}</strong>. Lớp học mới sẽ xuất hiện ngay
            trên danh mục lớp của khối tương ứng.
          </div>
        </div>

        <div className="field">
          <label className="label">Khối lớp (*)</label>
          <select
            className="select"
            value={grade}
            onChange={(e) => handleGradeChange(Number(e.target.value))}
          >
            <option value={1}>Khối 1 (Tiểu học)</option>
            <option value={2}>Khối 2</option>
            <option value={3}>Khối 3</option>
            <option value={4}>Khối 4</option>
            <option value={5}>Khối 5</option>
          </select>
        </div>

        <div className="field">
          <label className="label">Tên lớp (*)</label>
          <input
            type="text"
            className="input"
            value={className}
            onChange={(e) => setClassName(e.target.value)}
            placeholder="Ví dụ: 4D, 1E..."
            required
          />
          <small style={{ color: 'var(--color-muted-fg)' }}>
            Quy tắc đặt tên: Khối ({grade}) + 1 chữ cái phân lớp (VD: {grade}A, {grade}B).
          </small>
        </div>

        <div className="field">
          <label className="label">Phòng học</label>
          <input
            type="text"
            className="input"
            value={roomNumber}
            onChange={(e) => setRoomNumber(e.target.value)}
            placeholder="Ví dụ: Phòng 40D"
          />
        </div>

        <div className="field">
          <label className="label">Sĩ số tối đa</label>
          <input
            type="number"
            className="input"
            value={capacity}
            onChange={(e) => setCapacity(Number(e.target.value))}
            min={25}
            max={40}
            required
          />
          <small style={{ color: 'var(--color-muted-fg)' }}>
            Quy chuẩn trường tiểu học: 30 - 35 học sinh / lớp (hệ thống hỗ trợ linh hoạt từ 25 - 40 học sinh).
          </small>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 10 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={loading}>
            Hủy
          </button>
          <button type="submit" className="btn btn--primary" disabled={loading}>
            {loading ? 'Đang tạo...' : 'Tạo lớp học'}
            <Check size={18} />
          </button>
        </div>
      </form>
    </GeometricModal>
  );
};

