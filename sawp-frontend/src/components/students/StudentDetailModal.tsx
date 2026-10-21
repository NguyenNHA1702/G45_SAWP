import React, { useState } from 'react';
import {
  Calendar,
  CheckCircle,
  GraduationCap,
  MapPin,
  Phone,
  ShieldCheck,
  User,
  Users,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { studentApi } from '../../services/implementations/studentApi';
import type { StudentProfileDto } from '../../types/student.types';

interface StudentDetailModalProps {
  open: boolean;
  onClose: () => void;
  student: StudentProfileDto | null;
  onUpdated: (student: StudentProfileDto) => void;
}

export const StudentDetailModal: React.FC<StudentDetailModalProps> = ({
  open,
  onClose,
  student,
  onUpdated,
}) => {
  const [activating, setActivating] = useState(false);

  if (!student) return null;

  const handleActivate = async () => {
    setActivating(true);
    try {
      const res = await studentApi.activateStudent(student.id);
      if (res.success) {
        toast.success(`Đã kích hoạt hồ sơ học sinh ${student.fullName} (Active)!`);
        onUpdated({ ...student, status: 'Active' });
      } else {
        toast.error(res.message || 'Kích hoạt thất bại.');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'Lỗi khi kích hoạt học sinh.');
    } finally {
      setActivating(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title={`Hồ sơ: ${student.fullName}`}
      badgeText={student.studentCode}
      badgeColor="violet"
      icon={<User size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-accent-soft)"
      maxWidth="md"
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Header Profile summary */}
        <div
          className="card"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 16,
            background: 'var(--color-bg)',
            boxShadow: 'none',
          }}
        >
          <div
            style={{
              width: 56,
              height: 56,
              borderRadius: '50%',
              background: 'var(--color-accent)',
              color: '#ffffff',
              display: 'grid',
              placeItems: 'center',
              fontSize: 'var(--fs-lg)',
              fontFamily: 'var(--font-heading)',
              fontWeight: 800,
              border: 'var(--border)',
            }}
          >
            {student.fullName.charAt(0)}
          </div>
          <div style={{ flex: 1 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <h3 style={{ margin: 0 }}>{student.fullName}</h3>
              <span
                className={`badge ${
                  student.status === 'Active' ? 'badge--mint' : 'badge--amber'
                }`}
              >
                {student.status}
              </span>
            </div>
            <div style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)', marginTop: 2 }}>
              Mã định danh: <strong>{student.studentCode}</strong> • Giới tính: {student.gender}
            </div>
          </div>
        </div>

        {/* Details Grid */}
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
          <div className="card" style={{ padding: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700 }}>
              <Calendar size={14} /> Ngày sinh
            </div>
            <div style={{ fontWeight: 700, marginTop: 4 }}>
              {new Date(student.dateOfBirth).toLocaleDateString('vi-VN')}
            </div>
          </div>

          <div className="card" style={{ padding: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700 }}>
              <GraduationCap size={14} /> Lớp đang học
            </div>
            <div style={{ fontWeight: 700, marginTop: 4, color: student.currentClassName ? 'var(--color-accent)' : 'var(--color-muted-fg)' }}>
              {student.currentClassName ? `Lớp ${student.currentClassName}` : 'Chưa xếp lớp'}
            </div>
          </div>

          <div className="card" style={{ padding: 12, gridColumn: 'span 2' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700 }}>
              <Users size={14} /> Giáo viên chủ nhiệm
            </div>
            <div style={{ fontWeight: 700, marginTop: 4 }}>
              {student.homeroomTeacherName || 'Chưa phân công GVCN'}
            </div>
          </div>

          <div className="card" style={{ padding: 12, gridColumn: 'span 2' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700 }}>
              <Phone size={14} /> Người giám hộ chính
            </div>
            <div style={{ fontWeight: 700, marginTop: 4 }}>
              {student.primaryGuardianName || 'Chưa có thông tin'}
              {student.primaryGuardianPhone && (
                <span style={{ fontWeight: 400, color: 'var(--color-muted-fg)', marginLeft: 8 }}>
                  (SĐT: {student.primaryGuardianPhone})
                </span>
              )}
            </div>
          </div>

          <div className="card" style={{ padding: 12, gridColumn: 'span 2' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700 }}>
              <MapPin size={14} /> Địa chỉ thường trú
            </div>
            <div style={{ fontWeight: 500, marginTop: 4 }}>
              {student.address || 'Chưa cập nhật'}
            </div>
          </div>
        </div>

        {/* Actions */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 8 }}>
          {student.status !== 'Active' ? (
            <button
              type="button"
              className="btn btn--primary btn--sm"
              onClick={handleActivate}
              disabled={activating}
            >
              <ShieldCheck size={16} />
              {activating ? 'Đang kích hoạt...' : 'Kích hoạt hồ sơ (Active)'}
            </button>
          ) : (
            <span className="badge badge--mint">
              <CheckCircle size={14} /> Hồ sơ đã kích hoạt
            </span>
          )}

          <button type="button" className="btn btn--secondary" onClick={onClose}>
            Đóng
          </button>
        </div>
      </div>
    </GeometricModal>
  );
};
