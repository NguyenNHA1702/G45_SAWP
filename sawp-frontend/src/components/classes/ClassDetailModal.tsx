import React, { useEffect, useState } from 'react';
import {
  CheckCircle2,
  GraduationCap,
  Megaphone,
  Trash2,
  UserCheck,
  UserPlus,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { classApi } from '../../services/implementations/classApi';
import type { ClassRosterDto, ClassSummaryDto } from '../../types/class.types';

interface ClassDetailModalProps {
  open: boolean;
  onClose: () => void;
  targetClass: ClassSummaryDto | null;
  onOpenAssignTeacher: (c: ClassSummaryDto) => void;
  onOpenAssignStudents: (c: ClassSummaryDto) => void;
  onRosterUpdated: () => void;
}

export const ClassDetailModal: React.FC<ClassDetailModalProps> = ({
  open,
  onClose,
  targetClass,
  onOpenAssignTeacher,
  onOpenAssignStudents,
  onRosterUpdated,
}) => {
  const [roster, setRoster] = useState<ClassRosterDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [publishing, setPublishing] = useState(false);

  useEffect(() => {
    if (open && targetClass) {
      loadRoster();
    }
  }, [open, targetClass]);

  const loadRoster = async () => {
    if (!targetClass) return;
    setLoading(true);
    try {
      const res = await classApi.getClassRoster(targetClass.id);
      if (res.success && res.data) {
        setRoster(res.data);
      }
    } catch {
      toast.error('Lỗi khi tải danh sách học sinh của lớp.');
    } finally {
      setLoading(false);
    }
  };

  const handlePublishPlacement = async () => {
    if (!targetClass) return;
    setPublishing(true);
    try {
      const res = await classApi.publishPlacement(targetClass.id);
      if (res.success) {
        toast.success(`Đã công bố danh sách xếp lớp ${targetClass.className} tới Phụ huynh & GV!`);
        loadRoster();
        onRosterUpdated();
      } else {
        toast.error(res.message || 'Công bố thất bại.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi công bố xếp lớp.');
    } finally {
      setPublishing(false);
    }
  };

  const handleRemoveStudent = async (studentId: number, studentName: string) => {
    if (!targetClass) return;
    if (!window.confirm(`Bạn có chắc muốn gỡ học sinh ${studentName} khỏi lớp ${targetClass.className}?`)) {
      return;
    }

    try {
      const res = await classApi.removeStudent(targetClass.id, studentId);
      if (res.success) {
        toast.success(`Đã gỡ học sinh khỏi lớp!`);
        loadRoster();
        onRosterUpdated();
      } else {
        toast.error(res.message || 'Không thể gỡ học sinh.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi gỡ học sinh.');
    }
  };

  if (!targetClass) return null;

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title={`Chi tiết Lớp ${targetClass.className}`}
      badgeText={`Khối ${targetClass.grade} • Năm học ${targetClass.schoolYearName}`}
      badgeColor="violet"
      icon={<GraduationCap size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-accent-soft)"
      maxWidth="lg"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {/* Top Summary Bar */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: '1fr 1fr',
            gap: 12,
            background: 'var(--color-bg)',
            padding: 16,
            borderRadius: 'var(--radius-md)',
            border: 'var(--border)',
          }}
        >
          {/* GVCN */}
          <div>
            <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
              Giáo viên chủ nhiệm
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 4 }}>
              <UserCheck size={18} color="var(--color-quaternary)" />
              <strong style={{ fontSize: 'var(--fs-base)' }}>
                {targetClass.homeroomTeacherName || 'Chưa phân công'}
              </strong>
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                style={{ padding: '2px 8px', fontSize: 'var(--fs-xs)', marginLeft: 'auto' }}
                onClick={() => onOpenAssignTeacher(targetClass)}
              >
                {targetClass.homeroomTeacherName ? 'Đổi GVCN' : '+ Phân công'}
              </button>
            </div>
          </div>

          {/* Sĩ số & Trạng thái công bố */}
          <div>
            <div style={{ fontSize: 'var(--fs-xs)', textTransform: 'uppercase', color: 'var(--color-muted-fg)', fontWeight: 700 }}>
              Sĩ số hiện tại
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 4 }}>
              <span className="badge badge--mint">
                {targetClass.enrolledStudentsCount} / {targetClass.capacity} học sinh
              </span>
              {roster?.isPlacementPublished ? (
                <span className="badge badge--violet">
                  <CheckCircle2 size={12} /> Đã công bố
                </span>
              ) : (
                <span className="badge badge--amber">Chưa công bố</span>
              )}
            </div>
          </div>
        </div>

        {/* Action Row */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
          <div style={{ display: 'flex', gap: 8 }}>
            <button
              type="button"
              className="btn btn--primary btn--sm"
              onClick={() => onOpenAssignStudents(targetClass)}
              disabled={targetClass.availableCapacity <= 0}
            >
              <UserPlus size={16} /> Xếp thêm học sinh
            </button>
            {!roster?.isPlacementPublished && (
              <button
                type="button"
                className="btn btn--secondary btn--sm"
                onClick={handlePublishPlacement}
                disabled={publishing || (roster?.students.length ?? 0) === 0}
              >
                <Megaphone size={16} color="var(--color-secondary)" />
                {publishing ? 'Đang công bố...' : 'Công bố kết quả xếp lớp'}
              </button>
            )}
          </div>
          <span style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>
            Danh sách gồm <strong>{roster?.students.length ?? 0}</strong> học sinh
          </span>
        </div>

        {/* Student Roster Table */}
        {loading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>Đang tải danh sách học sinh...</div>
        ) : !roster || roster.students.length === 0 ? (
          <div className="card empty-state" style={{ padding: 28, textAlign: 'center' }}>
            <Users size={36} color="var(--color-muted-fg)" style={{ margin: '0 auto 8px' }} />
            <p style={{ margin: 0, color: 'var(--color-muted-fg)' }}>
              Lớp này hiện chưa có học sinh nào. Bấm nút <strong>"Xếp thêm học sinh"</strong> ở trên để thêm.
            </p>
          </div>
        ) : (
          <div className="data-table-wrapper" style={{ maxHeight: 320 }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>STT</th>
                  <th>Mã HS</th>
                  <th>Họ và tên</th>
                  <th>Ngày sinh</th>
                  <th>Giới tính</th>
                  <th>Phụ huynh</th>
                  <th>SĐT Phụ huynh</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {roster.students.map((s, idx) => (
                  <tr key={s.studentId}>
                    <td>{idx + 1}</td>
                    <td style={{ fontWeight: 700 }}>{s.studentCode}</td>
                    <td>{s.fullName}</td>
                    <td>{new Date(s.dateOfBirth).toLocaleDateString('vi-VN')}</td>
                    <td>{s.gender}</td>
                    <td>{s.primaryGuardianName || '-'}</td>
                    <td>{s.primaryGuardianPhone || '-'}</td>
                    <td>
                      <button
                        type="button"
                        className="btn btn--secondary btn--sm"
                        style={{ padding: '4px 8px', color: 'var(--color-secondary)' }}
                        onClick={() => handleRemoveStudent(s.studentId, s.fullName)}
                        title="Gỡ khỏi lớp"
                      >
                        <Trash2 size={14} /> Gỡ
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 10 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose}>
            Đóng
          </button>
        </div>
      </div>
    </GeometricModal>
  );
};
