import React, { useState } from 'react';
import { CalendarPlus, Check, Sparkles } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { classApi } from '../../services/implementations/classApi';
import type { SchoolYearDto } from '../../types/class.types';

interface CreateSchoolYearModalProps {
  open: boolean;
  onClose: () => void;
  onYearCreated: (newYear: SchoolYearDto) => void;
}

export const CreateSchoolYearModal: React.FC<CreateSchoolYearModalProps> = ({
  open,
  onClose,
  onYearCreated,
}) => {
  const currentYear = new Date().getFullYear();
  const nextYear = currentYear + 1;

  const [yearName, setYearName] = useState(`${nextYear}-${nextYear + 1}`);
  const [startDate, setStartDate] = useState(`${nextYear}-09-01`);
  const [endDate, setEndDate] = useState(`${nextYear + 1}-05-31`);
  const [isActive, setIsActive] = useState(true);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const trimmedName = yearName.trim();
    if (!trimmedName) {
      toast.error('Vui lòng nhập tên năm học (Ví dụ: 2027-2028)!');
      return;
    }

    if (!startDate || !endDate) {
      toast.error('Vui lòng chọn đầy đủ ngày bắt đầu và kết thúc năm học.');
      return;
    }

    const start = new Date(startDate);
    const end = new Date(endDate);

    if (start >= end) {
      toast.error('Ngày bắt đầu phải trước ngày kết thúc năm học.');
      return;
    }

    const diffDays = (end.getTime() - start.getTime()) / (1000 * 3600 * 24);
    if (diffDays < 60) {
      toast.error('Thời lượng năm học quá ngắn (tối thiểu 60 ngày). Vui lòng kiểm tra lại ngày.');
      return;
    }

    if (diffDays > 450) {
      toast.error('Thời lượng năm học không được vượt quá 15 tháng (tối đa 450 ngày). Vui lòng kiểm tra lại năm bắt đầu/kết thúc.');
      return;
    }

    setLoading(true);
    try {
      const res = await classApi.createSchoolYear({
        yearName: trimmedName,
        startDate,
        endDate,
        isActive,
      });

      if (res.success && res.data) {
        toast.success(`Đã khởi tạo năm học '${res.data.yearName}' thành công!`);
        onYearCreated(res.data);
        onClose();
      } else {
        toast.error(res.message || 'Không thể tạo năm học mới.');
      }
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Lỗi khi khởi tạo năm học mới.';
      toast.error(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Khởi tạo Năm học mới"
      badgeText="Năm học mới"
      badgeColor="amber"
      icon={<CalendarPlus size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="#FEF08A"
      maxWidth="md"
    >
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div className="banner banner--info" style={{ fontSize: 'var(--fs-sm)' }}>
          <Sparkles size={18} color="var(--color-tertiary)" style={{ flexShrink: 0, marginTop: 2 }} />
          <div>
            Khởi tạo chu kỳ năm học mới cho nhà trường theo khung kế hoạch của Sở GD&ĐT.
            Sau khi tạo, Giáo vụ có thể bắt đầu <strong>khởi tạo danh sách lớp mục tiêu</strong> cho năm học này.
          </div>
        </div>

        {/* Tên năm học */}
        <div className="field">
          <label className="label">
            Tên năm học <span style={{ color: 'var(--color-quaternary-fg)' }}>*</span>
          </label>
          <input
            type="text"
            className="input"
            required
            value={yearName}
            onChange={(e) => setYearName(e.target.value)}
            placeholder="Ví dụ: 2027-2028"
          />
          <div style={{ fontSize: '0.75rem', color: 'var(--color-muted-fg)', marginTop: 4 }}>
            Định dạng thông lệ: <strong>YYYY-YYYY</strong> (Ví dụ: 2027-2028)
          </div>
        </div>

        {/* Ngày bắt đầu & Kết thúc */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
          <div className="field">
            <label className="label">
              Ngày bắt đầu năm học <span style={{ color: 'var(--color-quaternary-fg)' }}>*</span>
            </label>
            <input
              type="date"
              className="input"
              required
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
            />
            <div style={{ fontSize: '0.75rem', color: 'var(--color-muted-fg)', marginTop: 4 }}>
              Thường là ngày tựu trường (Tháng 8 hoặc 01/09)
            </div>
          </div>

          <div className="field">
            <label className="label">
              Ngày kết thúc năm học <span style={{ color: 'var(--color-quaternary-fg)' }}>*</span>
            </label>
            <input
              type="date"
              className="input"
              required
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
            />
            <div style={{ fontSize: '0.75rem', color: 'var(--color-muted-fg)', marginTop: 4 }}>
              Thường là ngày bế giảng (Cuối tháng 5 năm sau)
            </div>
          </div>
        </div>

        {/* Checkbox đặt làm năm học hiện tại */}
        <div
          style={{
            backgroundColor: '#F8FAFC',
            border: '2px solid #E2E8F0',
            borderRadius: 14,
            padding: '12px 14px',
          }}
        >
          <label
            style={{
              display: 'flex',
              alignItems: 'flex-start',
              gap: 10,
              cursor: 'pointer',
              fontSize: 'var(--fs-sm)',
              fontWeight: 600,
              color: 'var(--color-ink)',
            }}
          >
            <input
              type="checkbox"
              checked={isActive}
              onChange={(e) => setIsActive(e.target.checked)}
              style={{ width: 18, height: 18, marginTop: 2, accentColor: 'var(--color-tertiary)' }}
            />
            <div>
              <div style={{ fontWeight: 700 }}>Đặt làm Năm học hiện tại (Active)</div>
              <div style={{ fontSize: '0.75rem', color: 'var(--color-muted-fg)', fontWeight: 500 }}>
                Hệ thống quy định chỉ có duy nhất 1 năm học được kích hoạt tại một thời điểm.
                Nếu chọn, năm học cũ sẽ tự động chuyển về trạng thái lưu trữ.
              </div>
            </div>
          </label>
        </div>

        {/* Actions */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 10 }}>
          <button
            type="button"
            className="btn btn--secondary"
            onClick={onClose}
            disabled={loading}
          >
            Hủy
          </button>
          <button
            type="submit"
            className="btn btn--primary"
            disabled={loading}
          >
            <Check size={16} />
            {loading ? 'Đang tạo...' : 'Khởi tạo năm học'}
          </button>
        </div>
      </form>
    </GeometricModal>
  );
};
