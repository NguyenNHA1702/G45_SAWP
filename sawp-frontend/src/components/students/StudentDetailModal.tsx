import React, { useEffect, useState } from 'react';
import {
  Calendar,
  CheckCircle,
  GraduationCap,
  MapPin,
  Pencil,
  Phone,
  Save,
  ShieldCheck,
  User,
  Users,
  X,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { studentApi } from '../../services/implementations/studentApi';
import { getAuthUser } from '../../services/apiClient';
import { StudentGuardiansSection } from './StudentGuardiansSection';
import { formatDDMMYYYY } from '../../utils/date';
import type { StudentProfileDto } from '../../types/student.types';

export interface StudentDetailModalProps {
  open: boolean;
  onClose: () => void;
  student?: StudentProfileDto | null;
  studentId?: number | null;
  onUpdated?: (student: StudentProfileDto) => void;
}

export const StudentDetailModal: React.FC<StudentDetailModalProps> = ({
  open,
  onClose,
  student,
  studentId,
  onUpdated,
}) => {
  const [activating, setActivating] = useState(false);
  const [loadedStudent, setLoadedStudent] = useState<StudentProfileDto | null>(null);
  const [loading, setLoading] = useState(false);

  // SC-04: Inline edit state
  const [isEditing, setIsEditing] = useState(false);
  const [savingEdit, setSavingEdit] = useState(false);
  const [editForm, setEditForm] = useState({
    fullName: '',
    dateOfBirth: '',
    gender: 'Nam',
    residentialArea: '',
    isSpecialNeeds: false,
    specialNeedsNotes: '',
  });

  useEffect(() => {
    setIsEditing(false);
    // A list row may not carry the guardians, so the full profile is always loaded when the modal opens
    const id = studentId ?? student?.id;
    if (student) {
      setLoadedStudent(student);
    }
    if (id && open) {
      setLoading(true);
      studentApi
        .getStudentProfile(id)
        .then((res) => {
          if (res.success && res.data) {
            setLoadedStudent(res.data);
          }
        })
        .catch(() => {
          toast.error('Không thể tải thông tin hồ sơ học sinh.');
        })
        .finally(() => {
          setLoading(false);
        });
    } else if (!student) {
      setLoadedStudent(null);
    }
  }, [student, studentId, open]);

  // The loaded (and, after a change, updated) profile wins over the list row passed in
  const activeStudent = loadedStudent ?? student;
  const canManageGuardians = (() => {
    try {
      const roles: string[] = JSON.parse(getAuthUser() ?? '{}')?.roles ?? [];
      return roles.includes('OfficeStaff') || roles.includes('Admin');
    } catch {
      return false;
    }
  })();

  if (!open) return null;

  if (!activeStudent) {
    return (
      <GeometricModal open={open} onClose={onClose} title="Hồ sơ học sinh" maxWidth="md">
        <div style={{ padding: 32, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
          {loading ? '⏳ Đang tải thông tin chi tiết học sinh...' : 'Không tìm thấy hồ sơ học sinh.'}
        </div>
      </GeometricModal>
    );
  }

  const startEdit = () => {
    setEditForm({
      fullName: activeStudent.fullName || '',
      dateOfBirth: activeStudent.dateOfBirth ? activeStudent.dateOfBirth.slice(0, 10) : '',
      gender: activeStudent.gender === 'Nữ' || activeStudent.gender === 'Female' ? 'Nữ' : 'Nam',
      residentialArea: activeStudent.residentialArea || activeStudent.address || '',
      isSpecialNeeds: !!activeStudent.isSpecialNeeds,
      specialNeedsNotes: activeStudent.specialNeedsNotes || '',
    });
    setIsEditing(true);
  };

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    const cleanedName = editForm.fullName.trim().replace(/\s+/g, ' ');
    if (cleanedName.length < 2 || cleanedName.length > 100) {
      toast.error('Họ và tên học sinh phải có độ dài từ 2 đến 100 ký tự.');
      return;
    }
    if (/[<>]/.test(cleanedName)) {
      toast.error('Họ và tên học sinh không được chứa thẻ HTML.');
      return;
    }
    if (!/^[\p{L}\s'\-.]+$/u.test(cleanedName) || !/\p{L}/u.test(cleanedName)) {
      toast.error("Họ và tên học sinh chỉ được chứa chữ cái, khoảng trắng và dấu (' - .), không được chứa chữ số hoặc ký tự đặc biệt.");
      return;
    }
    if (!editForm.dateOfBirth) {
      toast.error('Vui lòng chọn ngày sinh của học sinh.');
      return;
    }
    const birthDate = new Date(editForm.dateOfBirth);
    const today = new Date();
    if (isNaN(birthDate.getTime()) || birthDate >= today) {
      toast.error('Ngày sinh không hợp lệ (không thể là ngày trong tương lai).');
      return;
    }
    const age = today.getFullYear() - birthDate.getFullYear();
    if (age < 5 || age > 15) {
      toast.error(`Độ tuổi học sinh không hợp lệ (${age} tuổi). Độ tuổi tiểu học hợp lệ từ 5 đến 15 tuổi.`);
      return;
    }

    setSavingEdit(true);
    try {
      const res = await studentApi.updateStudent(activeStudent.id, {
        fullName: cleanedName,
        dateOfBirth: editForm.dateOfBirth,
        gender: editForm.gender,
        residentialArea: editForm.residentialArea.trim() || undefined,
        isSpecialNeeds: editForm.isSpecialNeeds,
        specialNeedsNotes: editForm.isSpecialNeeds ? editForm.specialNeedsNotes.trim() || undefined : undefined,
        status: activeStudent.status,
      });

      if (res.success && res.data) {
        toast.success('Đã cập nhật hồ sơ học sinh thành công!');
        setLoadedStudent(res.data);
        setIsEditing(false);
        onUpdated?.(res.data);
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể cập nhật hồ sơ học sinh.');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.errors?.[0] || err?.response?.data?.message || 'Lỗi khi cập nhật hồ sơ học sinh.');
    } finally {
      setSavingEdit(false);
    }
  };

  const handleActivate = async () => {
    setActivating(true);
    try {
      const res = await studentApi.activateStudent(activeStudent.id);
      if (res.success) {
        toast.success(`Đã kích hoạt hồ sơ học sinh ${activeStudent.fullName} (Active)!`);
        if (onUpdated) {
          onUpdated({ ...activeStudent, status: 'Active' });
        }
        setLoadedStudent((prev) => (prev ? { ...prev, status: 'Active' } : null));
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
      title={`Hồ sơ: ${activeStudent.fullName}`}
      badgeText={activeStudent.studentCode}
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
            {activeStudent.fullName.charAt(0)}
          </div>
          <div style={{ flex: 1 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
              <h3 style={{ margin: 0 }}>{activeStudent.fullName}</h3>
              {canManageGuardians && !isEditing && (
                <button
                  type="button"
                  className="btn btn--secondary btn--sm"
                  onClick={startEdit}
                  style={{ marginLeft: 'auto', padding: '5px 12px', display: 'inline-flex', alignItems: 'center', gap: 5 }}
                >
                  <Pencil size={14} /> Chỉnh sửa
                </button>
              )}
            </div>
            <div style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)', marginTop: 2 }}>
              Mã định danh: <strong>{activeStudent.studentCode}</strong> • Giới tính: {activeStudent.gender}
            </div>
          </div>
        </div>

        {/* SC-04: Inline Edit Form or Read-only Details Grid */}
        {isEditing ? (
          <form onSubmit={handleSaveEdit} className="card" style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 12, background: '#FFFDF5' }}>
            <div style={{ fontWeight: 800, fontSize: 13, color: 'var(--color-accent)', textTransform: 'uppercase' }}>
              Chỉnh sửa thông tin học sinh
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
              <div className="field" style={{ gridColumn: 'span 2' }}>
                <label className="label">Họ và tên (*)</label>
                <input
                  type="text"
                  className="input"
                  required
                  value={editForm.fullName}
                  onChange={(e) => setEditForm({ ...editForm, fullName: e.target.value })}
                />
              </div>

              <div className="field">
                <label className="label">Ngày sinh (*)</label>
                <input
                  type="date"
                  className="input"
                  required
                  min={`${new Date().getFullYear() - 15}-01-01`}
                  max={`${new Date().getFullYear() - 5}-12-31`}
                  value={editForm.dateOfBirth}
                  onChange={(e) => setEditForm({ ...editForm, dateOfBirth: e.target.value })}
                />
              </div>

              <div className="field">
                <label className="label">Giới tính (*)</label>
                <select
                  className="select"
                  value={editForm.gender}
                  onChange={(e) => setEditForm({ ...editForm, gender: e.target.value })}
                >
                  <option value="Nam">Nam</option>
                  <option value="Nữ">Nữ</option>
                </select>
              </div>

              <div className="field" style={{ gridColumn: 'span 2' }}>
                <label className="label">Địa chỉ thường trú</label>
                <input
                  type="text"
                  className="input"
                  placeholder="Số nhà, đường, phường/xã, quận/huyện..."
                  value={editForm.residentialArea}
                  onChange={(e) => setEditForm({ ...editForm, residentialArea: e.target.value })}
                />
              </div>

              <div className="field" style={{ gridColumn: 'span 2' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontSize: 13, fontWeight: 700 }}>
                  <input
                    type="checkbox"
                    checked={editForm.isSpecialNeeds}
                    onChange={(e) => setEditForm({ ...editForm, isSpecialNeeds: e.target.checked })}
                  />
                  Học sinh cần hỗ trợ đặc biệt
                </label>
                {editForm.isSpecialNeeds && (
                  <input
                    type="text"
                    className="input"
                    style={{ marginTop: 6 }}
                    placeholder="Ghi chú hỗ trợ đặc biệt..."
                    value={editForm.specialNeedsNotes}
                    onChange={(e) => setEditForm({ ...editForm, specialNeedsNotes: e.target.value })}
                  />
                )}
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 4 }}>
              <button type="button" className="btn btn--secondary btn--sm" onClick={() => setIsEditing(false)} disabled={savingEdit}>
                <X size={14} /> Hủy
              </button>
              <button type="submit" className="btn btn--primary btn--sm" disabled={savingEdit}>
                <Save size={14} /> {savingEdit ? 'Đang lưu...' : 'Lưu thay đổi'}
              </button>
            </div>
          </form>
        ) : (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
          <div className="card" style={{ padding: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700 }}>
              <Calendar size={14} /> Ngày sinh
            </div>
            <div style={{ fontWeight: 700, marginTop: 4 }}>
              {formatDDMMYYYY(activeStudent.dateOfBirth)}
            </div>
          </div>

          <div className="card" style={{ padding: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700 }}>
              <GraduationCap size={14} /> Lớp đang học
            </div>
            <div style={{ fontWeight: 700, marginTop: 4, color: activeStudent.currentClassName ? 'var(--color-accent)' : 'var(--color-muted-fg)' }}>
              {activeStudent.currentClassName ? `Lớp ${activeStudent.currentClassName}` : 'Chưa xếp lớp'}
            </div>
          </div>

          <div className="card" style={{ padding: 12, gridColumn: 'span 2' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700 }}>
              <Users size={14} /> Giáo viên chủ nhiệm
            </div>
            <div style={{ fontWeight: 700, marginTop: 4 }}>
              {activeStudent.homeroomTeacherName || 'Chưa phân công GVCN'}
            </div>
          </div>

          <div className="card" style={{ padding: 12, gridColumn: 'span 2' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700 }}>
              <Phone size={14} /> Người giám hộ chính
            </div>
            <div style={{ fontWeight: 700, marginTop: 4 }}>
              {activeStudent.primaryGuardianName || 'Chưa có thông tin'}
              {activeStudent.primaryGuardianPhone && (
                <span style={{ fontWeight: 400, color: 'var(--color-muted-fg)', marginLeft: 8 }}>
                  (SĐT: {activeStudent.primaryGuardianPhone})
                </span>
              )}
            </div>
          </div>

          <div className="card" style={{ padding: 12, gridColumn: 'span 2' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)', textTransform: 'uppercase', fontWeight: 700 }}>
              <MapPin size={14} /> Địa chỉ thường trú
            </div>
            <div style={{ fontWeight: 500, marginTop: 4 }}>
              {activeStudent.address || 'Chưa cập nhật'}
            </div>
          </div>
        </div>
        )}

        <StudentGuardiansSection
          student={activeStudent}
          canManage={canManageGuardians}
          onChanged={(updated) => {
            setLoadedStudent(updated);
            onUpdated?.(updated);
          }}
        />

        {/* Actions */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 8 }}>
          {activeStudent.status !== 'Active' ? (
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
