import React, { useState } from 'react';
import { Calendar, CalendarPlus, Check, Sparkles } from 'lucide-react';
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

    // Validate tên năm học phải đúng định dạng YYYY-YYYY và năm kết thúc = năm bắt đầu + 1
    const yearMatch = trimmedName.match(/^(\d{4})-(\d{4})$/);
    if (!yearMatch) {
      toast.error('Tên năm học phải có định dạng YYYY-YYYY (ví dụ: 2026-2027)');
      return;
    }

    const expectedStartYear = parseInt(yearMatch[1], 10);
    const expectedEndYear = parseInt(yearMatch[2], 10);

    if (expectedEndYear !== expectedStartYear + 1) {
      toast.error(`Năm kết thúc (${expectedEndYear}) phải bằng năm bắt đầu (${expectedStartYear}) + 1. Ví dụ: ${expectedStartYear}-${expectedStartYear + 1}`);
      return;
    }

    const startYear = start.getFullYear();
    const endYear = end.getFullYear();

    if (startYear !== expectedStartYear || endYear !== expectedEndYear) {
      toast.error(
        `Năm của ngày bắt đầu (${startYear}) và ngày kết thúc (${endYear}) phải trùng khớp với tên năm học (${expectedStartYear}-${expectedEndYear})!`
      );
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
      });

      if (res.success && res.data) {
        toast.success(`Đã khởi tạo năm học '${res.data.yearName}' thành công!`);
        onYearCreated(res.data);
        onClose();
      } else {
        toast.error(res.message || 'Không thể tạo năm học mới.');
      }
    } catch (err: any) {
      const message =
        err?.response?.data?.errors?.[0] ||
        err?.response?.data?.message ||
        (err instanceof Error ? err.message : 'Lỗi khi khởi tạo năm học mới.');
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
      <form noValidate onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
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

          <div
            style={{
              gridColumn: '1 / -1',
              fontSize: '0.8rem',
              color: 'var(--color-fg)',
              backgroundColor: '#F8FAFC',
              border: '1.5px dashed #CBD5E1',
              padding: '8px 12px',
              borderRadius: 'var(--radius-sm)',
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              fontWeight: 600,
            }}
          >
            <Calendar size={15} color="var(--color-accent)" />
            <span>Định dạng ngày tháng: <strong>DD/MM/YYYY (Ngày/Tháng/Năm)</strong></span>
          </div>
        </div>

        {/* UC-13 step 2, BR-72: a new school year is always created as a Draft */}
        <div
          style={{
            backgroundColor: '#F8FAFC',
            border: '2px solid #E2E8F0',
            borderRadius: 14,
            padding: '12px 14px',
            fontSize: '0.8rem',
            color: 'var(--color-muted-fg)',
            fontWeight: 500,
          }}
        >
          Năm học mới được tạo ở trạng thái <strong>Bản nháp</strong>. Sau khi nhập dữ liệu xếp lớp và rà soát từng lớp,
          Giáo vụ kích hoạt năm học để thay thế năm học hiện tại.
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
