import React, { useState } from 'react';
import { CheckCircle2, RotateCcw, X, Award } from 'lucide-react';
import toast from 'react-hot-toast';
import { placementPlanApi } from '../../services/implementations/placementPlanApi';
import type { PlacementPlanDetailDto } from '../../types/placement.types';

interface PlacementReviewModalProps {
  isOpen: boolean;
  onClose: () => void;
  plan: PlacementPlanDetailDto;
  onReviewed: (updatedPlan: PlacementPlanDetailDto) => void;
}

export const PlacementReviewModal: React.FC<PlacementReviewModalProps> = ({
  isOpen,
  onClose,
  plan,
  onReviewed,
}) => {
  const [isApproved, setIsApproved] = useState<boolean>(true);
  const [reviewNotes, setReviewNotes] = useState<string>('');
  const [submitting, setSubmitting] = useState<boolean>(false);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isApproved && !reviewNotes.trim()) {
      toast.error('Vui lòng nhập lý do và yêu cầu điều chỉnh khi trả lại phương án.');
      return;
    }

    setSubmitting(true);
    try {
      const res = await placementPlanApi.reviewPlan(plan.id, {
        isApproved,
        reviewNotes: reviewNotes.trim() || (isApproved ? 'Đồng ý phê duyệt phương án xếp lớp chính thức.' : ''),
      });

      if (res.success) {
        toast.success(
          isApproved
            ? 'Đã phê duyệt phương án thành công! Danh sách lớp chính thức đã được thiết lập cho GVCN.'
            : 'Đã trả lại phương án để Giáo vụ điều chỉnh.'
        );
        // Reload full plan detail
        const reloaded = await placementPlanApi.getPlanById(plan.id);
        if (reloaded.success && reloaded.data) {
          onReviewed(reloaded.data);
        }
        onClose();
      } else {
        toast.error(res.message || 'Lỗi khi xử lý phê duyệt phương án.');
      }
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Lỗi khi gọi API phê duyệt.';
      toast.error(message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 60,
        backgroundColor: 'rgba(30, 41, 59, 0.6)',
        backdropFilter: 'blur(4px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 16,
      }}
    >
      <div
        style={{
          backgroundColor: '#FFFFFF',
          border: '3px solid #1E293B',
          borderRadius: 24,
          boxShadow: '8px 8px 0 0 #1E293B',
          width: '100%',
          maxWidth: 540,
          padding: 24,
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div
              style={{
                width: 40,
                height: 40,
                borderRadius: 12,
                backgroundColor: isApproved ? '#DCFCE7' : '#FEE2E2',
                border: '2px solid #1E293B',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              {isApproved ? <Award size={22} color="#15803D" /> : <RotateCcw size={22} color="#B91C1C" />}
            </div>
            <div>
              <h3 style={{ fontSize: '1.2rem', fontWeight: 800, color: '#1E293B', margin: 0 }}>
                Ban Giám hiệu xét duyệt phương án
              </h3>
              <p style={{ fontSize: '0.85rem', color: '#64748B', margin: 0 }}>
                {plan.planName} (v{plan.version})
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            style={{
              background: '#F1F5F9',
              border: '2px solid #1E293B',
              borderRadius: 10,
              padding: 5,
              cursor: 'pointer',
              display: 'flex',
            }}
          >
            <X size={18} color="#1E293B" />
          </button>
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* Lựa chọn Quyết định */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            <button
              type="button"
              onClick={() => setIsApproved(true)}
              style={{
                padding: '12px 14px',
                borderRadius: 14,
                border: `2px solid ${isApproved ? '#15803D' : '#CBD5E1'}`,
                backgroundColor: isApproved ? '#DCFCE7' : '#F8FAFC',
                fontWeight: 800,
                color: isApproved ? '#15803D' : '#64748B',
                boxShadow: isApproved ? '3px 3px 0 #15803D' : 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 8,
              }}
            >
              <CheckCircle2 size={18} /> Phê duyệt chính thức
            </button>

            <button
              type="button"
              onClick={() => setIsApproved(false)}
              style={{
                padding: '12px 14px',
                borderRadius: 14,
                border: `2px solid ${!isApproved ? '#B91C1C' : '#CBD5E1'}`,
                backgroundColor: !isApproved ? '#FEE2E2' : '#F8FAFC',
                fontWeight: 800,
                color: !isApproved ? '#B91C1C' : '#64748B',
                boxShadow: !isApproved ? '3px 3px 0 #B91C1C' : 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 8,
              }}
            >
              <RotateCcw size={18} /> Trả lại để điều chỉnh
            </button>
          </div>

          {/* Lưu ý quan trọng */}
          {isApproved ? (
            <div
              style={{
                backgroundColor: '#F0FDF4',
                border: '2px dashed #16A34A',
                borderRadius: 14,
                padding: '12px 14px',
                fontSize: '0.85rem',
                color: '#15803D',
                fontWeight: 600,
              }}
            >
              ✓ Khi phê duyệt: Hệ thống sẽ chính thức xác lập danh sách học sinh vào các lớp trong năm học và công bố danh sách cho Giáo viên chủ nhiệm & Phụ huynh theo đúng quy định.
            </div>
          ) : (
            <div
              style={{
                backgroundColor: '#FEF2F2',
                border: '2px dashed #EF4444',
                borderRadius: 14,
                padding: '12px 14px',
                fontSize: '0.85rem',
                color: '#B91C1C',
                fontWeight: 600,
              }}
            >
              ⚠ Khi trả lại: Phương án sẽ chuyển về trạng thái 'Bị trả lại'. Giáo vụ sẽ nhận được thông báo kèm ý kiến chỉ đạo để tiến hành đổi chỗ hoặc lập phương án mới.
            </div>
          )}

          {/* Ý kiến chỉ đạo / Ghi chú */}
          <div>
            <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: 700, color: '#1E293B', marginBottom: 6 }}>
              Ý kiến chỉ đạo / Ghi chú của Ban Giám hiệu {!isApproved && <span style={{ color: '#EF4444' }}>*</span>}:
            </label>
            <textarea
              rows={3}
              required={!isApproved}
              placeholder={
                isApproved
                  ? 'Ghi chú phê duyệt (Ví dụ: Thống nhất phương án xếp lớp đầu năm theo đề xuất)'
                  : 'Nêu rõ lý do trả lại và yêu cầu điều chỉnh (Ví dụ: Cần cân bằng lại sĩ số lớp 1A và 1B)'
              }
              value={reviewNotes}
              onChange={(e) => setReviewNotes(e.target.value)}
              style={{
                width: '100%',
                padding: '10px 12px',
                borderRadius: 12,
                border: '2px solid #1E293B',
                fontSize: '0.9rem',
                fontFamily: 'inherit',
                outline: 'none',
                boxSizing: 'border-box',
              }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              style={{
                padding: '9px 16px',
                borderRadius: 12,
                border: '2px solid #1E293B',
                backgroundColor: '#F1F5F9',
                fontWeight: 700,
                color: '#475569',
                cursor: 'pointer',
              }}
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={submitting}
              style={{
                padding: '9px 22px',
                borderRadius: 12,
                border: '2px solid #1E293B',
                backgroundColor: isApproved ? '#22C55E' : '#EF4444',
                fontWeight: 800,
                color: '#FFFFFF',
                boxShadow: '3px 3px 0 0 #1E293B',
                cursor: submitting ? 'not-allowed' : 'pointer',
              }}
            >
              {submitting ? 'Đang lưu...' : isApproved ? 'Xác nhận phê duyệt' : 'Xác nhận trả lại'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
