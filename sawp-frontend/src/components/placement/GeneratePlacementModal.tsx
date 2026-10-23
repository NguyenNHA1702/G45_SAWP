import React, { useState } from 'react';
import {
  Sparkles,
  X,
  ShieldCheck,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { placementPlanApi } from '../../services/implementations/placementPlanApi';
import type { SchoolYearDto, ClassSummaryDto } from '../../types/class.types';
import type { PlacementPlanDetailDto } from '../../types/placement.types';

interface GeneratePlacementModalProps {
  isOpen: boolean;
  onClose: () => void;
  schoolYears: SchoolYearDto[];
  activeSchoolYearId?: number;
  availableClasses: ClassSummaryDto[];
  onGenerated: (plan: PlacementPlanDetailDto) => void;
}

export const GeneratePlacementModal: React.FC<GeneratePlacementModalProps> = ({
  isOpen,
  onClose,
  schoolYears,
  activeSchoolYearId,
  availableClasses,
  onGenerated,
}) => {
  const [schoolYearId, setSchoolYearId] = useState<number>(activeSchoolYearId || (schoolYears[0]?.id ?? 1));
  const [grade, setGrade] = useState<number>(1);
  const [planName, setPlanName] = useState<string>('Phương án xếp lớp Khối 1 - Đầu năm học');
  const [targetCapacity, setTargetCapacity] = useState<number>(35);
  const [maxSpecialNeeds, setMaxSpecialNeeds] = useState<number>(2);
  const [avoidDuplicateNames, setAvoidDuplicateNames] = useState<boolean>(true);
  const [prioritizeArea, setPrioritizeArea] = useState<boolean>(true);
  const [balanceGender, setBalanceGender] = useState<boolean>(true);
  const [submitting, setSubmitting] = useState(false);

  // Seamless auto-create classes if no classes exist for this grade
  const [autoCreateClasses, setAutoCreateClasses] = useState<boolean>(true);
  const [autoSectionsStr, setAutoSectionsStr] = useState<string>('A, B, C');

  if (!isOpen) return null;

  const targetClassesForGrade = availableClasses.filter(
    (c) => c.schoolYearId === schoolYearId && c.grade === grade
  );

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!schoolYearId) {
      toast.error('Vui lòng chọn năm học.');
      return;
    }

    const parsedSuffixes = autoSectionsStr
      .split(',')
      .map((s) => s.trim().toUpperCase())
      .filter(Boolean);

    setSubmitting(true);
    try {
      const res = await placementPlanApi.generateDraftPlan({
        schoolYearId,
        grade,
        planName,
        targetCapacity,
        maxSpecialNeedsPerClass: maxSpecialNeeds,
        avoidDuplicateNames,
        prioritizeResidentialArea: prioritizeArea,
        balanceGender,
        targetClassIds: targetClassesForGrade.map((c) => c.id),
        autoCreateClassesIfMissing: targetClassesForGrade.length === 0 ? autoCreateClasses : false,
        autoCreateClassCount: parsedSuffixes.length || 3,
        autoCreateSectionSuffixes: parsedSuffixes.length ? parsedSuffixes : ['A', 'B', 'C'],
      });

      if (res.success && res.data) {
        toast.success(`Đã sinh phương án xếp lớp '${res.data.planName}' thành công!`);
        onGenerated(res.data);
        onClose();
      } else {
        toast.error(res.message || 'Không thể sinh phương án xếp lớp.');
      }
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Lỗi khi gọi thuật toán xếp lớp.';
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
        zIndex: 50,
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
          maxWidth: 620,
          maxHeight: '90vh',
          overflowY: 'auto',
          padding: 28,
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <div
              style={{
                width: 44,
                height: 44,
                borderRadius: 14,
                backgroundColor: '#FEF08A',
                border: '2px solid #1E293B',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                boxShadow: '2px 2px 0 #1E293B',
              }}
            >
              <Sparkles size={24} color="#1E293B" />
            </div>
            <div>
              <h2 style={{ fontSize: '1.25rem', fontWeight: 800, color: '#1E293B', margin: 0 }}>
                Lập phương án xếp lớp đầu năm
              </h2>
              <p style={{ fontSize: '0.875rem', color: '#64748B', margin: 0 }}>
                Thuật toán cân bằng giới tính, hòa nhập & địa bàn cư trú
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            style={{
              background: '#F1F5F9',
              border: '2px solid #1E293B',
              borderRadius: 12,
              padding: 6,
              cursor: 'pointer',
              display: 'flex',
            }}
          >
            <X size={20} color="#1E293B" />
          </button>
        </div>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          {/* Tên phương án */}
          <div>
            <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 700, color: '#1E293B', marginBottom: 6 }}>
              Tên phương án xếp lớp:
            </label>
            <input
              type="text"
              required
              value={planName}
              onChange={(e) => setPlanName(e.target.value)}
              style={{
                width: '100%',
                padding: '10px 14px',
                borderRadius: 12,
                border: '2px solid #1E293B',
                fontSize: '0.95rem',
                fontWeight: 600,
                outline: 'none',
                boxSizing: 'border-box',
              }}
            />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            {/* Năm học */}
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 700, color: '#1E293B', marginBottom: 6 }}>
                Năm học áp dụng:
              </label>
              <select
                value={schoolYearId}
                onChange={(e) => setSchoolYearId(Number(e.target.value))}
                style={{
                  width: '100%',
                  padding: '10px 12px',
                  borderRadius: 12,
                  border: '2px solid #1E293B',
                  fontWeight: 600,
                  fontSize: '0.9rem',
                }}
              >
                {schoolYears.map((sy) => (
                  <option key={sy.id} value={sy.id}>
                    {sy.yearName} {sy.isActive ? '(Hiện tại)' : ''}
                  </option>
                ))}
              </select>
            </div>

            {/* Khối lớp */}
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 700, color: '#1E293B', marginBottom: 6 }}>
                Khối lớp:
              </label>
              <select
                value={grade}
                onChange={(e) => {
                  const g = Number(e.target.value);
                  setGrade(g);
                  setPlanName(`Phương án xếp lớp Khối ${g} - Đầu năm học`);
                }}
                style={{
                  width: '100%',
                  padding: '10px 12px',
                  borderRadius: 12,
                  border: '2px solid #1E293B',
                  fontWeight: 600,
                  fontSize: '0.9rem',
                }}
              >
                {[1, 2, 3, 4, 5].map((g) => (
                  <option key={g} value={g}>
                    Khối {g}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Sĩ số dự kiến & Lớp tiếp nhận */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 700, color: '#1E293B', marginBottom: 6 }}>
                Sĩ số dự kiến mỗi lớp:
              </label>
              <input
                type="number"
                min={15}
                max={50}
                value={targetCapacity}
                onChange={(e) => setTargetCapacity(Number(e.target.value))}
                style={{
                  width: '100%',
                  padding: '10px 14px',
                  borderRadius: 12,
                  border: '2px solid #1E293B',
                  fontSize: '0.95rem',
                  fontWeight: 600,
                  boxSizing: 'border-box',
                }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 700, color: '#1E293B', marginBottom: 6 }}>
                Số lớp mở trong khối:
              </label>
              <div
                style={{
                  padding: '10px 14px',
                  borderRadius: 12,
                  border: '2px solid #1E293B',
                  backgroundColor: '#F8FAFC',
                  fontWeight: 700,
                  color: '#0284C7',
                  fontSize: '0.95rem',
                }}
              >
                {targetClassesForGrade.length > 0
                  ? `${targetClassesForGrade.length} lớp (${targetClassesForGrade.map((c) => c.className).join(', ')})`
                  : 'Chưa có lớp nào mở trong khối này'}
              </div>
            </div>
          </div>

          {/* Tiêu chí xếp lớp thông minh */}
          <div
            style={{
              backgroundColor: '#FFFDF5',
              border: '2px dashed #F59E0B',
              borderRadius: 16,
              padding: 16,
            }}
          >
            <div style={{ fontSize: '0.9rem', fontWeight: 800, color: '#B45309', marginBottom: 10, display: 'flex', alignItems: 'center', gap: 6 }}>
              <ShieldCheck size={18} /> Tiêu chí thuật toán tối ưu xếp lớp (SAWP Smart Placement):
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', fontSize: '0.875rem', fontWeight: 600, color: '#1E293B' }}>
                <input
                  type="checkbox"
                  checked={balanceGender}
                  onChange={(e) => setBalanceGender(e.target.checked)}
                  style={{ width: 18, height: 18, accentColor: '#0284C7' }}
                />
                <span>
                  <strong style={{ color: '#0F172A' }}>Cân bằng sĩ số và tỷ lệ Nam/Nữ</strong> giữa các lớp trong khối
                </span>
              </label>

              <label style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', fontSize: '0.875rem', fontWeight: 600, color: '#1E293B' }}>
                <input
                  type="checkbox"
                  checked={maxSpecialNeeds === 2}
                  onChange={(e) => setMaxSpecialNeeds(e.target.checked ? 2 : 5)}
                  style={{ width: 18, height: 18, accentColor: '#0284C7' }}
                />
                <span>
                  <strong style={{ color: '#0F172A' }}>Giới hạn tối đa 2 học sinh khuyết tật hòa nhập / lớp</strong> (Ưu tiên phân bổ đều)
                </span>
              </label>

              <label style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', fontSize: '0.875rem', fontWeight: 600, color: '#1E293B' }}>
                <input
                  type="checkbox"
                  checked={avoidDuplicateNames}
                  onChange={(e) => setAvoidDuplicateNames(e.target.checked)}
                  style={{ width: 18, height: 18, accentColor: '#0284C7' }}
                />
                <span>
                  <strong style={{ color: '#0F172A' }}>Tránh học sinh trùng họ tên hoàn toàn</strong> trong cùng một lớp
                </span>
              </label>

              <label style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', fontSize: '0.875rem', fontWeight: 600, color: '#1E293B' }}>
                <input
                  type="checkbox"
                  checked={prioritizeArea}
                  onChange={(e) => setPrioritizeArea(e.target.checked)}
                  style={{ width: 18, height: 18, accentColor: '#0284C7' }}
                />
                <span>
                  <strong style={{ color: '#0F172A' }}>Ưu tiên phân bổ theo địa bàn cư trú</strong> (nếu không làm hỏng tiêu chí chính)
                </span>
              </label>
            </div>
          </div>

          {targetClassesForGrade.length === 0 && (
            <div
              style={{
                backgroundColor: '#EFF6FF',
                border: '2px solid #3B82F6',
                borderRadius: 16,
                padding: 16,
                display: 'flex',
                flexDirection: 'column',
                gap: 10,
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: '#1D4ED8', fontWeight: 800, fontSize: '0.9rem' }}>
                <Sparkles size={18} />
                Chưa có lớp nào mở trong Khối {grade} — Tự động khởi tạo nhanh:
              </div>
              <label style={{ display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', fontSize: '0.875rem', fontWeight: 600, color: '#1E293B' }}>
                <input
                  type="checkbox"
                  checked={autoCreateClasses}
                  onChange={(e) => setAutoCreateClasses(e.target.checked)}
                  style={{ width: 18, height: 18, accentColor: '#2563EB' }}
                />
                <span>
                  <strong>Tự động khởi tạo các lớp tiếp nhận</strong> (Ví dụ: {grade}A, {grade}B, {grade}C) khi bấm sinh phương án
                </span>
              </label>

              {autoCreateClasses && (
                <div>
                  <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
                    Danh sách phân lớp dự kiến (phân tách bằng dấu phẩy):
                  </label>
                  <input
                    type="text"
                    value={autoSectionsStr}
                    onChange={(e) => setAutoSectionsStr(e.target.value)}
                    placeholder="A, B, C"
                    style={{
                      width: '100%',
                      padding: '8px 12px',
                      borderRadius: 10,
                      border: '2px solid #1E293B',
                      fontSize: '0.875rem',
                      fontWeight: 600,
                      backgroundColor: '#FFFFFF',
                      boxSizing: 'border-box',
                    }}
                  />
                </div>
              )}
            </div>
          )}

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 10 }}>
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              style={{
                padding: '10px 18px',
                borderRadius: 14,
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
              disabled={submitting || (targetClassesForGrade.length === 0 && !autoCreateClasses)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 8,
                padding: '10px 22px',
                borderRadius: 14,
                border: '2px solid #1E293B',
                backgroundColor: '#FEF08A',
                fontWeight: 800,
                color: '#1E293B',
                boxShadow: '4px 4px 0 0 #1E293B',
                cursor: submitting || (targetClassesForGrade.length === 0 && !autoCreateClasses) ? 'not-allowed' : 'pointer',
                opacity: submitting || (targetClassesForGrade.length === 0 && !autoCreateClasses) ? 0.6 : 1,
              }}
            >
              <Sparkles size={18} />
              {submitting ? 'Đang chạy thuật toán...' : 'Sinh phương án nháp'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
