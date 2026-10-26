import React, { useState } from 'react';
import {
  AlertCircle,
  AlertTriangle,
  ArrowLeft,
  ArrowLeftRight,
  Award,
  CheckCircle2,
  Clock,
  Info,
  MapPin,
  RotateCcw,
  Send,
  Sparkles,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { placementPlanApi } from '../../services/implementations/placementPlanApi';
import { MoveOrSwapModal } from './MoveOrSwapModal';
import { PlacementReviewModal } from './PlacementReviewModal';
import type {
  PlacementPlanClassPreviewDto,
  PlacementPlanDetailDto,
  PlacementStudentItemDto,
} from '../../types/placement.types';

interface DraftPlacementPreviewProps {
  plan: PlacementPlanDetailDto;
  userRoles: string[];
  onPlanUpdated: (plan: PlacementPlanDetailDto) => void;
  onBackToHistory: () => void;
}

export const DraftPlacementPreview: React.FC<DraftPlacementPreviewProps> = ({
  plan,
  userRoles,
  onPlanUpdated,
  onBackToHistory,
}) => {
  const [submittingToBoard, setSubmittingToBoard] = useState(false);
  const [selectedStudentForAction, setSelectedStudentForAction] = useState<{
    student: PlacementStudentItemDto;
    classInfo: PlacementPlanClassPreviewDto;
  } | null>(null);
  const [reviewModalOpen, setReviewModalOpen] = useState(false);

  const isOfficeStaff = userRoles.includes('OfficeStaff') || userRoles.includes('Admin');
  const isSchoolBoard = userRoles.includes('SchoolBoard') || userRoles.includes('Admin');

  const handleSubmitToBoard = async () => {
    setSubmittingToBoard(true);
    try {
      const res = await placementPlanApi.submitPlan(plan.id);
      if (res.success) {
        toast.success('Đã trình phương án lên Ban Giám hiệu thành công!');
        const reloaded = await placementPlanApi.getPlanById(plan.id);
        if (reloaded.success && reloaded.data) {
          onPlanUpdated(reloaded.data);
        }
      } else {
        toast.error(res.message || 'Lỗi khi gửi phương án.');
      }
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Lỗi khi kết nối hệ thống.';
      toast.error(message);
    } finally {
      setSubmittingToBoard(false);
    }
  };

  const getStatusBadge = () => {
    switch (plan.status) {
      case 'Draft':
        return {
          label: 'Bản nháp (Đang điều chỉnh)',
          bg: '#FEF08A',
          color: '#854D0E',
          border: '#CA8A04',
          icon: Clock,
        };
      case 'Submitted':
        return {
          label: 'Đã trình Ban Giám hiệu duyệt',
          bg: '#BAE6FD',
          color: '#0369A1',
          border: '#0284C7',
          icon: Send,
        };
      case 'Approved':
        return {
          label: 'Đã phê duyệt chính thức',
          bg: '#DCFCE7',
          color: '#15803D',
          border: '#16A34A',
          icon: CheckCircle2,
        };
      case 'Rejected':
        return {
          label: 'Bị trả lại để điều chỉnh',
          bg: '#FEE2E2',
          color: '#B91C1C',
          border: '#EF4444',
          icon: RotateCcw,
        };
      default:
        return {
          label: plan.status,
          bg: '#F1F5F9',
          color: '#475569',
          border: '#94A3B8',
          icon: Info,
        };
    }
  };

  const statusConfig = getStatusBadge();
  const StatusIcon = statusConfig.icon;

  const totalBoys = plan.classes.reduce((acc, c) => acc + c.maleCount, 0);
  const totalGirls = plan.classes.reduce((acc, c) => acc + c.femaleCount, 0);
  const totalSpecial = plan.classes.reduce((acc, c) => acc + c.specialNeedsCount, 0);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
      {/* Top action & status banner */}
      <div
        style={{
          backgroundColor: '#FFFFFF',
          border: '3px solid #1E293B',
          borderRadius: 24,
          padding: 24,
          boxShadow: '6px 6px 0 0 #1E293B',
          display: 'flex',
          flexWrap: 'wrap',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 16,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
          <button
            onClick={onBackToHistory}
            style={{
              padding: '8px 12px',
              borderRadius: 12,
              border: '2px solid #1E293B',
              backgroundColor: '#F8FAFC',
              fontWeight: 800,
              color: '#1E293B',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            <ArrowLeft size={16} /> Lịch sử phương án
          </button>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <h2 style={{ fontSize: '1.4rem', fontWeight: 900, color: '#1E293B', margin: 0 }}>
                {plan.planName}
              </h2>
              <span
                style={{
                  fontSize: '0.8rem',
                  fontWeight: 800,
                  padding: '3px 10px',
                  borderRadius: 20,
                  border: `2px solid ${statusConfig.border}`,
                  backgroundColor: statusConfig.bg,
                  color: statusConfig.color,
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                }}
              >
                <StatusIcon size={14} /> {statusConfig.label}
              </span>
              <span
                style={{
                  fontSize: '0.8rem',
                  fontWeight: 700,
                  padding: '3px 10px',
                  borderRadius: 20,
                  backgroundColor: '#F1F5F9',
                  border: '2px solid #CBD5E1',
                  color: '#475569',
                }}
              >
                Phiên bản v{plan.version}
              </span>
            </div>
            <p style={{ fontSize: '0.85rem', color: '#64748B', margin: '4px 0 0 0' }}>
              Năm học: <strong>{plan.schoolYearName}</strong> • Khối: <strong>{plan.grade}</strong> • Người lập:{' '}
              <strong>{plan.createdByName}</strong> • Ngày tạo: {new Date(plan.createdAt).toLocaleString('vi-VN')}
            </p>
          </div>
        </div>

        {/* Workflow actions */}
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
          {isOfficeStaff && (plan.status === 'Draft' || plan.status === 'Rejected') && (
            <button
              onClick={handleSubmitToBoard}
              disabled={submittingToBoard}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 8,
                padding: '10px 20px',
                borderRadius: 14,
                border: '2px solid #1E293B',
                backgroundColor: '#BAE6FD',
                fontWeight: 800,
                color: '#0369A1',
                boxShadow: '4px 4px 0 0 #1E293B',
                cursor: submittingToBoard ? 'not-allowed' : 'pointer',
              }}
            >
              <Send size={18} />
              {submittingToBoard ? 'Đang gửi...' : 'Gửi Ban Giám hiệu phê duyệt'}
            </button>
          )}

          {isSchoolBoard && plan.status === 'Submitted' && (
            <button
              onClick={() => setReviewModalOpen(true)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 8,
                padding: '10px 22px',
                borderRadius: 14,
                border: '2px solid #1E293B',
                backgroundColor: '#DCFCE7',
                fontWeight: 800,
                color: '#15803D',
                boxShadow: '4px 4px 0 0 #1E293B',
                cursor: 'pointer',
              }}
            >
              <Award size={18} />
              Xét duyệt phương án (Hiệu trưởng)
            </button>
          )}

          {plan.status === 'Approved' && (
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 8,
                padding: '8px 16px',
                borderRadius: 14,
                backgroundColor: '#DCFCE7',
                border: '2px solid #16A34A',
                color: '#15803D',
                fontWeight: 800,
                fontSize: '0.9rem',
              }}
            >
              <CheckCircle2 size={18} />
              Đã thiết lập danh sách chính thức cho GVCN
            </div>
          )}
        </div>
      </div>

      {/* Review Notes banner if rejected or approved with notes */}
      {plan.reviewNotes && (
        <div
          style={{
            backgroundColor: plan.status === 'Approved' ? '#F0FDF4' : '#FEF2F2',
            border: `2px solid ${plan.status === 'Approved' ? '#16A34A' : '#EF4444'}`,
            borderRadius: 16,
            padding: '14px 18px',
            display: 'flex',
            alignItems: 'flex-start',
            gap: 12,
          }}
        >
          {plan.status === 'Approved' ? (
            <CheckCircle2 size={20} color="#16A34A" style={{ marginTop: 2, flexShrink: 0 }} />
          ) : (
            <AlertCircle size={20} color="#EF4444" style={{ marginTop: 2, flexShrink: 0 }} />
          )}
          <div>
            <div
              style={{
                fontSize: '0.875rem',
                fontWeight: 800,
                color: plan.status === 'Approved' ? '#15803D' : '#B91C1C',
              }}
            >
              Ý kiến chỉ đạo từ Ban Giám hiệu ({plan.reviewedByName} -{' '}
              {plan.reviewedAt ? new Date(plan.reviewedAt).toLocaleString('vi-VN') : ''}):
            </div>
            <div style={{ fontSize: '0.9rem', color: '#1E293B', marginTop: 4, fontWeight: 600 }}>
              "{plan.reviewNotes}"
            </div>
          </div>
        </div>
      )}

      {/* Summary KPI Badges */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 16 }}>
        <div
          style={{
            backgroundColor: '#FFFFFF',
            border: '2px solid #1E293B',
            borderRadius: 18,
            padding: 16,
            boxShadow: '4px 4px 0 #1E293B',
          }}
        >
          <div style={{ fontSize: '0.8rem', fontWeight: 700, color: '#64748B' }}>TỔNG SỐ HỌC SINH</div>
          <div style={{ fontSize: '1.75rem', fontWeight: 900, color: '#1E293B', marginTop: 4 }}>
            {plan.totalStudents} <span style={{ fontSize: '0.9rem', color: '#64748B' }}>em</span>
          </div>
          <div style={{ fontSize: '0.8rem', color: '#0369A1', fontWeight: 700, marginTop: 4 }}>
            {totalBoys} Nam • {totalGirls} Nữ
          </div>
        </div>

        <div
          style={{
            backgroundColor: '#FFFFFF',
            border: '2px solid #1E293B',
            borderRadius: 18,
            padding: 16,
            boxShadow: '4px 4px 0 #1E293B',
          }}
        >
          <div style={{ fontSize: '0.8rem', fontWeight: 700, color: '#64748B' }}>SỐ LỚP TIẾP NHẬN</div>
          <div style={{ fontSize: '1.75rem', fontWeight: 900, color: '#1E293B', marginTop: 4 }}>
            {plan.classes.length} <span style={{ fontSize: '0.9rem', color: '#64748B' }}>lớp</span>
          </div>
          <div style={{ fontSize: '0.8rem', color: '#475569', fontWeight: 600, marginTop: 4 }}>
            Chỉ tiêu: ~{plan.targetCapacity} hs/lớp
          </div>
        </div>

        <div
          style={{
            backgroundColor: '#FFFFFF',
            border: '2px solid #1E293B',
            borderRadius: 18,
            padding: 16,
            boxShadow: '4px 4px 0 #1E293B',
          }}
        >
          <div style={{ fontSize: '0.8rem', fontWeight: 700, color: '#64748B' }}>HS KHUYẾT TẬT HÒA NHẬP</div>
          <div style={{ fontSize: '1.75rem', fontWeight: 900, color: '#D97706', marginTop: 4 }}>
            {totalSpecial} <span style={{ fontSize: '0.9rem', color: '#64748B' }}>em</span>
          </div>
          <div style={{ fontSize: '0.8rem', color: '#B45309', fontWeight: 600, marginTop: 4 }}>
            Giới hạn tối đa: ≤ 2 hs/lớp
          </div>
        </div>

        <div
          style={{
            backgroundColor: plan.criteriaWarnings.some((w) => w.severity === 'Violation')
              ? '#FEF2F2'
              : '#F0FDF4',
            border: `2px solid ${
              plan.criteriaWarnings.some((w) => w.severity === 'Violation') ? '#EF4444' : '#22C55E'
            }`,
            borderRadius: 18,
            padding: 16,
            boxShadow: '4px 4px 0 #1E293B',
          }}
        >
          <div style={{ fontSize: '0.8rem', fontWeight: 700, color: '#64748B' }}>ĐÁNH GIÁ TIÊU CHÍ</div>
          <div
            style={{
              fontSize: '1.25rem',
              fontWeight: 900,
              color: plan.criteriaWarnings.some((w) => w.severity === 'Violation') ? '#B91C1C' : '#15803D',
              marginTop: 4,
            }}
          >
            {plan.criteriaWarnings.some((w) => w.severity === 'Violation')
              ? `${plan.violationCount} Tiêu chí vi phạm`
              : 'Đạt mọi tiêu chí'}
          </div>
          <div style={{ fontSize: '0.8rem', color: '#475569', fontWeight: 600, marginTop: 4 }}>
            {plan.criteriaWarnings.filter((w) => w.severity === 'Warning').length} Cảnh báo cần lưu ý
          </div>
        </div>
      </div>

      {/* Step 3: Hiển thị các tiêu chí không thỏa / Cảnh báo xếp lớp */}
      <div
        style={{
          backgroundColor: '#FFFFFF',
          border: '2px solid #1E293B',
          borderRadius: 20,
          padding: 18,
          boxShadow: '4px 4px 0 #1E293B',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
          <Sparkles size={20} color="#0284C7" />
          <h3 style={{ fontSize: '1.1rem', fontWeight: 800, color: '#1E293B', margin: 0 }}>
            Kiểm tra ràng buộc tiêu chí thuật toán (Criteria Validation Report)
          </h3>
        </div>

        {plan.criteriaWarnings.length === 0 ? (
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 10,
              padding: '12px 16px',
              borderRadius: 14,
              backgroundColor: '#DCFCE7',
              border: '2px solid #22C55E',
              color: '#15803D',
              fontWeight: 700,
              fontSize: '0.9rem',
            }}
          >
            <CheckCircle2 size={20} />
            Phương án xếp lớp hoàn toàn cân đối: Tỷ lệ Nam/Nữ đồng đều, mỗi lớp không quá 2 học sinh khuyết tật hòa nhập, không có học sinh trùng họ tên và đã tối ưu gom cụm địa bàn cư trú!
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {plan.criteriaWarnings.map((warning, index) => {
              const isViolation = warning.severity === 'Violation';
              const isWarning = warning.severity === 'Warning';
              return (
                <div
                  key={index}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 10,
                    padding: '10px 14px',
                    borderRadius: 12,
                    backgroundColor: isViolation ? '#FEF2F2' : isWarning ? '#FFFBEB' : '#F0F9FF',
                    border: `2px solid ${isViolation ? '#EF4444' : isWarning ? '#F59E0B' : '#0284C7'}`,
                    color: isViolation ? '#B91C1C' : isWarning ? '#B45309' : '#0369A1',
                    fontSize: '0.875rem',
                    fontWeight: 700,
                  }}
                >
                  {isViolation ? (
                    <AlertCircle size={18} style={{ flexShrink: 0 }} />
                  ) : isWarning ? (
                    <AlertTriangle size={18} style={{ flexShrink: 0 }} />
                  ) : (
                    <Info size={18} style={{ flexShrink: 0 }} />
                  )}
                  <span>{warning.message}</span>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Draft Placement Preview: Bảng Xem trước kết quả từng lớp */}
      <div>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 }}>
          <h3 style={{ fontSize: '1.2rem', fontWeight: 900, color: '#1E293B', margin: 0 }}>
            Bảng Xem trước kết quả phân lớp (Draft Placement Preview)
          </h3>
          {isOfficeStaff && plan.status !== 'Approved' && (
            <div style={{ fontSize: '0.85rem', color: '#64748B', fontWeight: 600 }}>
              💡 Giáo vụ có thể đổi chỗ hoặc chuyển lớp thủ công cho từng học sinh đặc thù
            </div>
          )}
        </div>

        {/* Classes Side-by-Side Grid */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(360px, 1fr))', gap: 20 }}>
          {plan.classes.map((cls) => {
            const hasOverSpecial = cls.specialNeedsCount > 2;
            const duplicateNamesInThisClass = cls.students
              .filter((s, idx, arr) => arr.some((other, oIdx) => oIdx !== idx && other.fullName.trim().toLowerCase() === s.fullName.trim().toLowerCase()))
              .map((s) => s.fullName.trim().toLowerCase());

            return (
              <div
                key={cls.classId}
                style={{
                  backgroundColor: '#FFFFFF',
                  border: `3px solid ${hasOverSpecial ? '#EF4444' : '#1E293B'}`,
                  borderRadius: 22,
                  boxShadow: '6px 6px 0 0 #1E293B',
                  overflow: 'hidden',
                  display: 'flex',
                  flexDirection: 'column',
                }}
              >
                {/* Header card */}
                <div
                  style={{
                    backgroundColor: '#FFFDF5',
                    borderBottom: '2px solid #1E293B',
                    padding: '16px 18px',
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <span
                        style={{
                          fontSize: '1.3rem',
                          fontWeight: 900,
                          color: '#1E293B',
                          backgroundColor: '#FEF08A',
                          border: '2px solid #1E293B',
                          borderRadius: 12,
                          padding: '4px 12px',
                        }}
                      >
                        Lớp {cls.className}
                      </span>
                      <span style={{ fontSize: '0.875rem', fontWeight: 700, color: '#475569' }}>
                        Khối {cls.grade}
                      </span>
                    </div>

                    <div style={{ textAlign: 'right' }}>
                      <span
                        style={{
                          fontSize: '1.05rem',
                          fontWeight: 900,
                          color: cls.totalStudents > cls.capacity ? '#DC2626' : '#0F172A',
                        }}
                      >
                        {cls.totalStudents}
                      </span>
                      <span style={{ fontSize: '0.85rem', color: '#64748B' }}>/{cls.capacity} hs</span>
                    </div>
                  </div>

                  {/* Metrics Badges */}
                  <div style={{ display: 'flex', gap: 8, marginTop: 12, flexWrap: 'wrap' }}>
                    <span
                      style={{
                        fontSize: '0.75rem',
                        fontWeight: 800,
                        padding: '3px 8px',
                        borderRadius: 8,
                        backgroundColor: '#E0F2FE',
                        color: '#0369A1',
                        border: '1px solid #BAE6FD',
                      }}
                    >
                      {cls.maleCount} Nam / {cls.femaleCount} Nữ
                    </span>

                    <span
                      style={{
                        fontSize: '0.75rem',
                        fontWeight: 800,
                        padding: '3px 8px',
                        borderRadius: 8,
                        backgroundColor: hasOverSpecial ? '#FEE2E2' : '#FEF3C7',
                        color: hasOverSpecial ? '#B91C1C' : '#B45309',
                        border: `1px solid ${hasOverSpecial ? '#FCA5A5' : '#FDE68A'}`,
                      }}
                    >
                      ★ {cls.specialNeedsCount} HS hòa nhập {hasOverSpecial ? '(Vượt quá 2!)' : ''}
                    </span>
                  </div>

                  {/* Phân bổ địa bàn cư trú */}
                  <div style={{ marginTop: 10, display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                    <MapPin size={14} color="#64748B" />
                    <span style={{ fontSize: '0.75rem', fontWeight: 700, color: '#64748B' }}>Địa bàn:</span>
                    {Object.entries(cls.residentialAreaCounts).map(([area, count]) => (
                      <span
                        key={area}
                        style={{
                          fontSize: '0.72rem',
                          fontWeight: 600,
                          backgroundColor: '#F1F5F9',
                          padding: '2px 6px',
                          borderRadius: 6,
                          color: '#334155',
                          border: '1px solid #CBD5E1',
                        }}
                      >
                        {area}: {count}
                      </span>
                    ))}
                  </div>
                </div>

                {/* Danh sách học sinh trong lớp */}
                <div style={{ flex: 1, padding: 12, display: 'flex', flexDirection: 'column', gap: 8, maxHeight: 420, overflowY: 'auto' }}>
                  {cls.students.length === 0 ? (
                    <div style={{ textAlign: 'center', padding: 24, color: '#94A3B8', fontSize: '0.85rem' }}>
                      Chưa có học sinh nào được xếp vào lớp này
                    </div>
                  ) : (
                    cls.students.map((student, idx) => {
                      const isDup = duplicateNamesInThisClass.includes(student.fullName.trim().toLowerCase());
                      return (
                        <div
                          key={student.studentId}
                          style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            padding: '8px 12px',
                            borderRadius: 12,
                            border: `2px solid ${isDup ? '#EF4444' : student.isSpecialNeeds ? '#F59E0B' : '#E2E8F0'}`,
                            backgroundColor: isDup ? '#FEF2F2' : student.isSpecialNeeds ? '#FFFBEB' : '#FFFFFF',
                            transition: 'all 0.15s ease',
                          }}
                        >
                          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                            <span
                              style={{
                                fontSize: '0.75rem',
                                fontWeight: 800,
                                color: '#64748B',
                                width: 22,
                                textAlign: 'right',
                              }}
                            >
                              {idx + 1}.
                            </span>
                            <div>
                              <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                                <span style={{ fontSize: '0.9rem', fontWeight: 800, color: '#0F172A' }}>
                                  {student.fullName}
                                </span>
                                <span
                                  style={{
                                    fontSize: '0.7rem',
                                    fontWeight: 700,
                                    padding: '1px 6px',
                                    borderRadius: 6,
                                    backgroundColor: student.gender === 'Nam' ? '#E0F2FE' : '#FCE7F3',
                                    color: student.gender === 'Nam' ? '#0369A1' : '#BE185D',
                                  }}
                                >
                                  {student.gender}
                                </span>
                                {isDup && (
                                  <span
                                    style={{
                                      fontSize: '0.65rem',
                                      fontWeight: 800,
                                      padding: '1px 6px',
                                      borderRadius: 6,
                                      backgroundColor: '#EF4444',
                                      color: '#FFFFFF',
                                    }}
                                  >
                                    Trùng tên!
                                  </span>
                                )}
                              </div>
                              <div style={{ fontSize: '0.75rem', color: '#64748B', display: 'flex', gap: 8, marginTop: 2 }}>
                                <span>{student.studentCode}</span>
                                {student.residentialArea && <span>• {student.residentialArea}</span>}
                                {student.isSpecialNeeds && (
                                  <span style={{ color: '#D97706', fontWeight: 700 }}>
                                    • ★ {student.specialNeedsNotes || 'Hòa nhập'}
                                  </span>
                                )}
                                {student.isManualOverride && (
                                  <span style={{ color: '#0284C7', fontWeight: 700 }}>• (Đã đổi)</span>
                                )}
                              </div>
                            </div>
                          </div>

                          {/* Nút đổi chỗ / chuyển lớp */}
                          {isOfficeStaff && plan.status !== 'Approved' && (
                            <button
                              onClick={() => setSelectedStudentForAction({ student, classInfo: cls })}
                              title="Chuyển lớp hoặc đổi chỗ học sinh này"
                              style={{
                                padding: '5px 8px',
                                borderRadius: 8,
                                border: '1.5px solid #1E293B',
                                backgroundColor: '#F8FAFC',
                                color: '#1E293B',
                                fontWeight: 700,
                                fontSize: '0.75rem',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: 4,
                              }}
                            >
                              <ArrowLeftRight size={13} /> Đổi chỗ
                            </button>
                          )}
                        </div>
                      );
                    })
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Modal Chuyển lớp / Đổi chỗ */}
      {selectedStudentForAction && (
        <MoveOrSwapModal
          isOpen={true}
          onClose={() => setSelectedStudentForAction(null)}
          planId={plan.id}
          student={selectedStudentForAction.student}
          currentClass={selectedStudentForAction.classInfo}
          allClasses={plan.classes}
          onUpdated={(updatedPlan) => {
            onPlanUpdated(updatedPlan);
            setSelectedStudentForAction(null);
          }}
        />
      )}

      {/* Modal Phê duyệt / Trả lại của Ban Giám hiệu */}
      {reviewModalOpen && (
        <PlacementReviewModal
          isOpen={true}
          onClose={() => setReviewModalOpen(false)}
          plan={plan}
          onReviewed={(updatedPlan) => {
            onPlanUpdated(updatedPlan);
            setReviewModalOpen(false);
          }}
        />
      )}
    </div>
  );
};
