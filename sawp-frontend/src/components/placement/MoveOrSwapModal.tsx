import React, { useState } from 'react';
import { ArrowLeftRight, MoveRight, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { placementPlanApi } from '../../services/implementations/placementPlanApi';
import type {
  PlacementPlanClassPreviewDto,
  PlacementPlanDetailDto,
  PlacementStudentItemDto,
} from '../../types/placement.types';

interface MoveOrSwapModalProps {
  isOpen: boolean;
  onClose: () => void;
  planId: number;
  student: PlacementStudentItemDto | null;
  currentClass: PlacementPlanClassPreviewDto | null;
  allClasses: PlacementPlanClassPreviewDto[];
  onUpdated: (updatedPlan: PlacementPlanDetailDto) => void;
}

export const MoveOrSwapModal: React.FC<MoveOrSwapModalProps> = ({
  isOpen,
  onClose,
  planId,
  student,
  currentClass,
  allClasses,
  onUpdated,
}) => {
  const [mode, setMode] = useState<'move' | 'swap'>('move');
  const [targetClassId, setTargetClassId] = useState<number>(0);
  const [swapWithStudentId, setSwapWithStudentId] = useState<number>(0);
  const [notes, setNotes] = useState<string>('');
  const [loading, setLoading] = useState<boolean>(false);

  if (!isOpen || !student || !currentClass) return null;

  const otherClasses = allClasses.filter((c) => c.classId !== currentClass.classId);
  const selectedTargetClass = allClasses.find((c) => c.classId === targetClassId) || otherClasses[0];

  const handleAction = async () => {
    const finalTargetClassId = targetClassId || otherClasses[0]?.classId;
    if (!finalTargetClassId) {
      toast.error('Vui lòng chọn lớp chuyển đến.');
      return;
    }

    setLoading(true);
    try {
      if (mode === 'move') {
        const res = await placementPlanApi.moveStudent(planId, {
          studentId: student.studentId,
          fromClassId: currentClass.classId,
          toClassId: finalTargetClassId,
          notes: notes || `Chuyển từ lớp ${currentClass.className} sang ${selectedTargetClass?.className}`,
        });
        if (res.success && res.data) {
          toast.success(`Đã chuyển em ${student.fullName} sang lớp ${selectedTargetClass?.className}!`);
          onUpdated(res.data);
          onClose();
        } else {
          toast.error(res.message || 'Lỗi khi chuyển lớp.');
        }
      } else {
        if (!swapWithStudentId) {
          toast.error('Vui lòng chọn học sinh ở lớp đối ứng để đổi chỗ.');
          return;
        }
        const partnerStudent = selectedTargetClass?.students.find((s) => s.studentId === swapWithStudentId);
        const res = await placementPlanApi.swapStudents(planId, {
          student1Id: student.studentId,
          class1Id: currentClass.classId,
          student2Id: swapWithStudentId,
          class2Id: finalTargetClassId,
          notes: notes || `Đổi chỗ giữa ${student.fullName} và ${partnerStudent?.fullName}`,
        });
        if (res.success && res.data) {
          toast.success(`Đã đổi chỗ thành công giữa 2 học sinh!`);
          onUpdated(res.data);
          onClose();
        } else {
          toast.error(res.message || 'Lỗi khi đổi chỗ.');
        }
      }
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Lỗi khi thực hiện đổi chỗ.';
      toast.error(message);
    } finally {
      setLoading(false);
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
          maxWidth: 520,
          padding: 24,
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div
              style={{
                width: 38,
                height: 38,
                borderRadius: 12,
                backgroundColor: '#BAE6FD',
                border: '2px solid #1E293B',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              {mode === 'move' ? <MoveRight size={20} color="#0369A1" /> : <ArrowLeftRight size={20} color="#0369A1" />}
            </div>
            <div>
              <h3 style={{ fontSize: '1.15rem', fontWeight: 800, color: '#1E293B', margin: 0 }}>
                {mode === 'move' ? 'Chuyển lớp học sinh' : 'Đổi chỗ hai học sinh'}
              </h3>
              <p style={{ fontSize: '0.85rem', color: '#64748B', margin: 0 }}>
                Điều chỉnh thủ công cho các trường hợp đặc biệt
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

        {/* Học sinh hiện tại */}
        <div
          style={{
            backgroundColor: '#F8FAFC',
            border: '2px solid #CBD5E1',
            borderRadius: 14,
            padding: '12px 16px',
            marginBottom: 16,
          }}
        >
          <div style={{ fontSize: '0.75rem', fontWeight: 800, color: '#64748B', textTransform: 'uppercase' }}>
            Học sinh được chọn
          </div>
          <div style={{ fontSize: '1.05rem', fontWeight: 800, color: '#0F172A', marginTop: 2 }}>
            {student.fullName} ({student.studentCode})
          </div>
          <div style={{ fontSize: '0.85rem', color: '#475569', display: 'flex', gap: 12, marginTop: 4, flexWrap: 'wrap' }}>
            <span>Lớp hiện tại: <strong>{currentClass.className}</strong></span>
            <span>Giới tính: <strong>{student.gender}</strong></span>
            {student.residentialArea && <span>Địa bàn: <strong>{student.residentialArea}</strong></span>}
            {student.isSpecialNeeds && (
              <span style={{ color: '#D97706', fontWeight: 700 }}>
                ★ {student.specialNeedsNotes || 'Cần hỗ trợ hòa nhập'}
              </span>
            )}
          </div>
        </div>

        {/* Chuyển chế độ: Move vs Swap */}
        <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
          <button
            type="button"
            onClick={() => setMode('move')}
            style={{
              flex: 1,
              padding: '8px 12px',
              borderRadius: 12,
              border: '2px solid #1E293B',
              backgroundColor: mode === 'move' ? '#FEF08A' : '#F1F5F9',
              fontWeight: 800,
              color: '#1E293B',
              cursor: 'pointer',
              fontSize: '0.85rem',
            }}
          >
            Chuyển lớp trực tiếp
          </button>
          <button
            type="button"
            onClick={() => setMode('swap')}
            style={{
              flex: 1,
              padding: '8px 12px',
              borderRadius: 12,
              border: '2px solid #1E293B',
              backgroundColor: mode === 'swap' ? '#FEF08A' : '#F1F5F9',
              fontWeight: 800,
              color: '#1E293B',
              cursor: 'pointer',
              fontSize: '0.85rem',
            }}
          >
            Đổi chỗ 2 học sinh
          </button>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
          {/* Lớp chuyển đến */}
          <div>
            <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: 700, color: '#1E293B', marginBottom: 6 }}>
              Chọn lớp tiếp nhận:
            </label>
            <select
              value={targetClassId || otherClasses[0]?.classId}
              onChange={(e) => {
                setTargetClassId(Number(e.target.value));
                setSwapWithStudentId(0);
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
              {otherClasses.map((c) => (
                <option key={c.classId} value={c.classId}>
                  Lớp {c.className} (Hiện có: {c.totalStudents} hs, {c.maleCount} Nam / {c.femaleCount} Nữ, {c.specialNeedsCount} Hòa nhập)
                </option>
              ))}
            </select>
          </div>

          {/* Nếu là Swap: Chọn học sinh ở lớp đối ứng */}
          {mode === 'swap' && (
            <div>
              <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: 700, color: '#1E293B', marginBottom: 6 }}>
                Chọn học sinh lớp {selectedTargetClass?.className} muốn đổi chỗ:
              </label>
              <select
                value={swapWithStudentId}
                onChange={(e) => setSwapWithStudentId(Number(e.target.value))}
                style={{
                  width: '100%',
                  padding: '10px 12px',
                  borderRadius: 12,
                  border: '2px solid #1E293B',
                  fontWeight: 600,
                  fontSize: '0.9rem',
                }}
              >
                <option value={0}>-- Chọn học sinh để hoán đổi --</option>
                {selectedTargetClass?.students.map((s) => (
                  <option key={s.studentId} value={s.studentId}>
                    {s.fullName} ({s.gender}) {s.isSpecialNeeds ? '★ [Hòa nhập]' : ''} - {s.residentialArea || 'Chưa có địa bàn'}
                  </option>
                ))}
              </select>
            </div>
          )}

          {/* Ghi chú lý do */}
          <div>
            <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: 700, color: '#1E293B', marginBottom: 6 }}>
              Lý do điều chỉnh (tùy chọn):
            </label>
            <input
              type="text"
              placeholder="Ví dụ: Đổi chỗ theo nguyện vọng hỗ trợ hòa nhập hoặc điều kiện cư trú"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              style={{
                width: '100%',
                padding: '9px 12px',
                borderRadius: 12,
                border: '2px solid #1E293B',
                fontSize: '0.875rem',
                boxSizing: 'border-box',
              }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 8 }}>
            <button
              type="button"
              onClick={onClose}
              disabled={loading}
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
              type="button"
              onClick={handleAction}
              disabled={loading || (mode === 'swap' && !swapWithStudentId)}
              style={{
                padding: '9px 20px',
                borderRadius: 12,
                border: '2px solid #1E293B',
                backgroundColor: '#BAE6FD',
                fontWeight: 800,
                color: '#0369A1',
                boxShadow: '3px 3px 0 0 #1E293B',
                cursor: loading || (mode === 'swap' && !swapWithStudentId) ? 'not-allowed' : 'pointer',
                opacity: loading || (mode === 'swap' && !swapWithStudentId) ? 0.6 : 1,
              }}
            >
              {loading ? 'Đang cập nhật...' : mode === 'move' ? 'Xác nhận chuyển lớp' : 'Xác nhận đổi chỗ'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
