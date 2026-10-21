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
  onClassesCreated: (newClasses: ClassSummaryDto[]) => void;
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

    setLoading(true);
    try {
      const res = await classApi.initializeBatch({
        schoolYearId: activeSchoolYear?.id,
        targetGrade,
        sectionNames,
        capacityPerClass: capacity,
      });

      if (res.success && res.data) {
        toast.success(`Đã khởi tạo ${res.data.length} lớp học thành công!`);
        onClassesCreated(res.data);
        onClose();
      } else {
        toast.error(res.message || 'Khởi tạo lớp hàng loạt thất bại.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi khởi tạo lớp học.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Khởi tạo danh sách lớp hàng loạt"
      badgeText="Quy trình xếp lớp (Workflow 1)"
      badgeColor="violet"
      icon={<Layers size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-accent-soft)"
      maxWidth="md"
    >
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
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
          <label className="label">Sĩ số tối đa mỗi lớp (Capacity)</label>
          <input
            type="number"
            className="input"
            min={15}
            max={50}
            value={capacity}
            onChange={(e) => setCapacity(Number(e.target.value))}
            required
          />
          <small style={{ color: 'var(--color-muted-fg)' }}>
            Theo quy chuẩn trường tiểu học: 30 - 35 học sinh / lớp.
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
