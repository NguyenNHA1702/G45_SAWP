import React, { useEffect, useState } from 'react';
import { Check, GraduationCap, UserCheck } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { classApi } from '../../services/implementations/classApi';
import type { ClassSummaryDto, TeacherOptionDto } from '../../types/class.types';

interface AssignTeacherModalProps {
  open: boolean;
  onClose: () => void;
  targetClass: ClassSummaryDto | null;
  onAssigned: () => void;
}

export const AssignTeacherModal: React.FC<AssignTeacherModalProps> = ({
  open,
  onClose,
  targetClass,
  onAssigned,
}) => {
  const [teachers, setTeachers] = useState<TeacherOptionDto[]>([]);
  const [selectedUserId, setSelectedUserId] = useState<number | undefined>(undefined);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open && targetClass) {
      loadTeachers();
      setSelectedUserId(targetClass.homeroomTeacherUserId);
    }
  }, [open, targetClass]);

  const loadTeachers = async () => {
    setLoading(true);
    try {
      const res = await classApi.getEligibleTeachers(targetClass?.schoolYearId);
      if (res.success && res.data) {
        setTeachers(res.data);
      }
    } catch {
      toast.error('Lỗi khi tải danh sách giáo viên.');
    } finally {
      setLoading(false);
    }
  };

  const handleAssign = async () => {
    if (!targetClass || !selectedUserId) {
      toast.error('Vui lòng chọn một giáo viên!');
      return;
    }

    setSubmitting(true);
    try {
      const res = await classApi.assignHomeroomTeacher(targetClass.id, selectedUserId);
      if (res.success) {
        toast.success(`Phân công GVCN cho lớp ${targetClass.className} thành công!`);
        onAssigned();
        onClose();
      } else {
        toast.error(res.message || 'Phân công thất bại.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi phân công giáo viên chủ nhiệm.');
    } finally {
      setSubmitting(false);
    }
  };

  if (!targetClass) return null;

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title={`Phân công GVCN: Lớp ${targetClass.className}`}
      badgeText="Quy tắc nghiệp vụ BR-16"
      badgeColor="mint"
      icon={<GraduationCap size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-quaternary)"
      maxWidth="md"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
          Theo quy tắc BR-16: Mỗi lớp học chỉ có 01 Giáo viên chủ nhiệm chính thức, và một giáo viên
          chỉ làm chủ nhiệm tối đa 01 lớp trong cùng năm học.
        </p>

        {loading ? (
          <div style={{ textAlign: 'center', padding: 20 }}>Đang tải danh sách giáo viên đủ điều kiện...</div>
        ) : (
          <div className="field">
            <label className="label">Chọn Giáo viên chủ nhiệm</label>
            <select
              className="select"
              value={selectedUserId || ''}
              onChange={(e) => setSelectedUserId(e.target.value ? Number(e.target.value) : undefined)}
            >
              <option value="">-- Chọn giáo viên --</option>
              {teachers.map((t) => (
                <option key={t.userId} value={t.userId}>
                  {t.fullName} ({t.email})
                  {t.currentAssignedClass
                    ? ` - Đang CN lớp: ${t.currentAssignedClass}`
                    : ' - Chưa chủ nhiệm lớp nào'}
                </option>
              ))}
            </select>
          </div>
        )}

        {/* Selected teacher preview card */}
        {selectedUserId && (
          <div
            className="card"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 12,
              background: 'var(--color-bg)',
              boxShadow: 'none',
              padding: 12,
            }}
          >
            <UserCheck size={28} color="var(--color-quaternary)" />
            <div>
              <div style={{ fontWeight: 700 }}>
                {teachers.find((t) => t.userId === selectedUserId)?.fullName}
              </div>
              <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                {teachers.find((t) => t.userId === selectedUserId)?.email} • SĐT:{' '}
                {teachers.find((t) => t.userId === selectedUserId)?.phone || 'Chưa cập nhật'}
              </div>
            </div>
          </div>
        )}

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 10 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={submitting}>
            Hủy
          </button>
          <button
            type="button"
            className="btn btn--primary"
            onClick={handleAssign}
            disabled={!selectedUserId || submitting}
          >
            {submitting ? 'Đang lưu...' : 'Lưu phân công'}
            <Check size={18} />
          </button>
        </div>
      </div>
    </GeometricModal>
  );
};
