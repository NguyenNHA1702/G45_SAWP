import React, { useState } from 'react';
import { Check, Layers, Sparkles } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { classApi } from '../../services/implementations/classApi';
import type { ClassSummaryDto, SchoolYearDto } from '../../types/class.types';

interface BatchInitClassesModalProps {
  open: boolean;
  onClose: () => void;
  activeSchoolYear: SchoolYearDto | null;
  onClassesCreated: (newClasses: ClassSummaryDto[], grade: number) => void;
}

export const BatchInitClassesModal: React.FC<BatchInitClassesModalProps> = ({
  open,
  onClose,
  activeSchoolYear,
  onClassesCreated,
}) => {
  const [targetGrade, setTargetGrade] = useState(1);
  const [sectionsStr, setSectionsStr] = useState('A, B, C');
  const [capacity, setCapacity] = useState(35);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const sectionNames = sectionsStr
      .split(',')
      .map((s) => s.trim().toUpperCase())
      .filter(Boolean);

    if (sectionNames.length === 0) {
      toast.error('Vui lòng nhập ít nhất 1 tên lớp (VD: A, B, C)!');
      return;
    }

    if (isNaN(capacity) || capacity < 15 || capacity > 50) {
      toast.error('Sĩ số mỗi lớp phải từ 15 đến 50 học sinh (Quy chuẩn thông thường: 30 - 35 em/lớp).');
      return;
    }

    setLoading(true);
    try {
      const res = await classApi.initializeBatch({
        schoolYearId: activeSchoolYear?.id,
        targetGrade,
        grades: [targetGrade],
        sectionNames,
        sectionSuffixes: sectionNames,
        capacityPerClass: capacity,
        defaultCapacity: capacity,
      });

      if (res.success && res.data) {
        toast.success(res.message || `Đã khởi tạo danh sách lớp thành công!`);
        onClassesCreated(res.data, targetGrade);
        onClose();
      } else {
        toast.error(res.message || 'Khởi tạo lớp hàng loạt thất bại.');
      }
    } catch (err: any) {
      const message =
        err?.response?.data?.errors?.[0] ||
        err?.response?.data?.message ||
        'Lỗi khi khởi tạo lớp học.';
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Khởi tạo danh sách lớp hàng loạt"
      badgeText="Khởi tạo lớp hàng loạt"
      badgeColor="violet"
      icon={<Layers size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-accent-soft)"
      maxWidth="md"
    >
      <form noValidate onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div className="banner banner--info" style={{ fontSize: 'var(--fs-sm)' }}>
          <Sparkles size={18} color="var(--color-accent)" style={{ flexShrink: 0, marginTop: 2 }} />
          <div>
            Khởi tạo nhanh các lớp mục tiêu cho Năm học{' '}
            <strong>{activeSchoolYear?.yearName ?? 'hiện tại'}</strong>. Hệ thống sẽ tạo lớp theo
            cú pháp: <strong>Khối + Tên phân lớp</strong> (VD: 1A, 1B, 1C).
          </div>
        </div>

        <div className="field">
          <label className="label">Khối lớp mục tiêu</label>
          <select
            className="select"
            value={targetGrade}
            onChange={(e) => setTargetGrade(Number(e.target.value))}
          >
            <option value={1}>Khối 1 (Đầu cấp tiểu học)</option>
            <option value={2}>Khối 2</option>
            <option value={3}>Khối 3</option>
            <option value={4}>Khối 4</option>
            <option value={5}>Khối 5</option>
          </select>
        </div>

        <div className="field">
          <label className="label">Danh sách phân lớp (phân tách bằng dấu phẩy)</label>
          <input
            type="text"
            className="input"
            value={sectionsStr}
            onChange={(e) => setSectionsStr(e.target.value)}
            placeholder="A, B, C, D"
            required
          />
          <small style={{ color: 'var(--color-muted-fg)' }}>
            Ví dụ: Nhập <code>A, B, C</code> sẽ tạo ra các lớp <strong>1A, 1B, 1C</strong>.
          </small>
        </div>

        <div className="field">
          <label className="label">Sĩ số tối đa mỗi lớp</label>
          <input
            type="number"
            className="input"
            value={capacity}
            onChange={(e) => setCapacity(Number(e.target.value))}
            required
          />
          <small style={{ color: 'var(--color-muted-fg)' }}>
            Quy chuẩn trường tiểu học: 30 - 35 học sinh / lớp (hệ thống cho phép thiết lập linh hoạt từ 15 - 50 học sinh).
          </small>
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 10 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={loading}>
            Hủy
          </button>
          <button type="submit" className="btn btn--primary" disabled={loading}>
            {loading ? 'Đang tạo...' : 'Khởi tạo danh sách lớp'}
            <Check size={18} />
          </button>
        </div>
      </form>
    </GeometricModal>
  );
};
