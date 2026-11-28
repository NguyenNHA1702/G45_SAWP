import React, { useEffect, useState } from 'react';
import {
  Calendar,
  CheckCircle,
  Clock,
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
import type { StudentHistoryDto, StudentProfileDto } from '../../types/student.types';

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

  // GAP-02 (UC-12): Tabs & Student History state
  const [activeTab, setActiveTab] = useState<'profile' | 'history'>('profile');
  const [history, setHistory] = useState<StudentHistoryDto | null>(null);
  const [loadingHistory, setLoadingHistory] = useState(false);

  // SC-04: Inline edit state
  const [isEditing, setIsEditing] = useState(false);
  const [savingEdit, setSavingEdit] = useState(false);
  const [statusUpdating, setStatusUpdating] = useState(false);
  const [editForm, setEditForm] = useState({
    fullName: '',
    dateOfBirth: '',
    gender: 'Nam',
    residentialArea: '',
    isSpecialNeeds: false,
    specialNeedsNotes: '',
    status: 'Active',
  });

  useEffect(() => {
    setIsEditing(false);
    setActiveTab('profile');
    setHistory(null);
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
      return roles.includes('OfficeStaff');
    } catch {
      return false;
    }
  })();

  useEffect(() => {
    if (!open || activeTab !== 'history' || !activeStudent?.id || history) return;
    setLoadingHistory(true);
    studentApi
      .getStudentHistory(activeStudent.id)
      .then((res) => {
        if (res.success && res.data) {
          setHistory(res.data);
        } else {
          toast.error(res.message || 'Không thể tải lịch sử học sinh.');
        }
      })
      .catch(() => {
        toast.error('Không thể tải lịch sử học sinh.');
      })
      .finally(() => {
        setLoadingHistory(false);
      });
  }, [open, activeTab, activeStudent?.id, history]);

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
      status: activeStudent.status || 'Active',
    });
    setIsEditing(true);
  };

  const handleStatusChange = async (newStatus: string) => {
    if (!activeStudent || newStatus === activeStudent.status) return;
    const statusLabel = newStatus === 'Active' ? 'Đang học (Active)' : newStatus === 'Inactive' ? 'Ngừng hoạt động (Inactive)' : 'Lưu trữ / Chuyển trường (Archived)';
    if (newStatus !== 'Active') {
      const ok = window.confirm(
        `Xác nhận chuyển trạng thái học sinh ${activeStudent.fullName} sang "${statusLabel}"?\n\nHọc sinh ngừng hoạt động sẽ không được đăng ký thêm suất ăn mới và có thể tạm ẩn khỏi vận hành hàng ngày.`
      );
      if (!ok) return;
    }

    setStatusUpdating(true);
    try {
      const dob = activeStudent.dateOfBirth ? activeStudent.dateOfBirth.slice(0, 10) : '';
      const birthYear = dob ? new Date(dob).getFullYear() : new Date().getFullYear();
      const age = new Date().getFullYear() - birthYear;
      const res = await studentApi.updateStudent(activeStudent.id, {
        fullName: activeStudent.fullName,
        dateOfBirth: dob,
        gender: activeStudent.gender,
        residentialArea: activeStudent.residentialArea || activeStudent.address,
        isSpecialNeeds: activeStudent.isSpecialNeeds,
        specialNeedsNotes: activeStudent.specialNeedsNotes,
        status: newStatus,
        allowAgeException: age > 15,
      });
      if (res.success && res.data) {
        toast.success(`Đã chuyển trạng thái hồ sơ sang ${statusLabel}!`);
        setLoadedStudent(res.data);
        setHistory(null);
        onUpdated?.(res.data);
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể cập nhật trạng thái học sinh.');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.errors?.[0] || err?.response?.data?.message || 'Lỗi khi cập nhật trạng thái.');
    } finally {
      setStatusUpdating(false);
    }
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
    if (isNaN(birthDate.getTime()) || birthDate >= today || birthDate.getFullYear() < 1900) {
      toast.error('Ngày sinh không hợp lệ (không thể là ngày trong tương lai).');
      return;
    }
    const age = today.getFullYear() - birthDate.getFullYear();
    if (age < 5) {
      toast.error(`Độ tuổi học sinh không hợp lệ (${age} tuổi). Học sinh tiểu học phải từ 5 tuổi trở lên.`);
      return;
    }

    let allowAgeException = false;
    let ageExceptionReason: string | undefined;
    if (age > 15) {
      const ok = window.confirm(
        `Cảnh báo ngoại lệ độ tuổi: Học sinh này ${age} tuổi (vượt trần 5–15 tuổi).\n\nBạn có chắc chắn muốn xác nhận trường hợp ngoại lệ quá tuổi?`
      );
      if (!ok) return;
      allowAgeException = true;
      ageExceptionReason = `Xác nhận ngoại lệ học sinh quá tuổi (${age} tuổi) khi cập nhật hồ sơ.`;
    }

    if (editForm.status !== activeStudent.status && editForm.status !== 'Active') {
      const ok = window.confirm(
        `Xác nhận chuyển trạng thái học sinh sang "${editForm.status}"?`
      );
      if (!ok) return;
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
        status: editForm.status,
        allowAgeException,
        ageExceptionReason,
      });

      if (res.success && res.data) {
        toast.success('Đã cập nhật hồ sơ học sinh thành công!');
        setLoadedStudent(res.data);
        setHistory(null);
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

  const statusBadgeClass =
    activeStudent.status === 'Active'
      ? 'badge--mint'
      : activeStudent.status === 'Inactive'
      ? 'badge--amber'
      : 'badge--pink';

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title={`Hồ sơ: ${activeStudent.fullName}`}
      badgeText={activeStudent.studentCode}
      badgeColor="violet"
      icon={<User size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-accent-soft)"
      maxWidth="lg"
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
              <span className={`badge ${statusBadgeClass}`}>{activeStudent.status}</span>
              {canManageGuardians && activeTab === 'profile' && !isEditing && (
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

        {/* GAP-02 (UC-12): Tabs */}
        <div style={{ display: 'flex', gap: 8, borderBottom: '2px solid var(--color-border)', paddingBottom: 10 }}>
          <button
            type="button"
            className={`btn btn--sm ${activeTab === 'profile' ? 'btn--primary' : 'btn--secondary'}`}
            onClick={() => setActiveTab('profile')}
          >
            <User size={14} /> Hồ sơ chi tiết
          </button>
          <button
            type="button"
            className={`btn btn--sm ${activeTab === 'history' ? 'btn--primary' : 'btn--secondary'}`}
            onClick={() => {
              setIsEditing(false);
              setActiveTab('history');
            }}
          >
            <Clock size={14} /> Lịch sử học sinh (UC-12)
          </button>
        </div>

        {activeTab === 'history' ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            {loadingHistory ? (
              <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
                ⏳ Đang tổng hợp lịch sử học sinh...
              </div>
            ) : !history ? (
              <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-muted-fg)' }}>
                Không có dữ liệu lịch sử.
              </div>
            ) : (
              <>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, minmax(0, 1fr))', gap: 10 }}>
                  <div className="card" style={{ padding: 10 }}>
                    <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>Có mặt</div>
                    <div style={{ fontSize: 'var(--fs-lg)', fontWeight: 800, color: '#059669' }}>{history.attendanceSummary.presentCount}</div>
                  </div>
                  <div className="card" style={{ padding: 10 }}>
                    <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>Nghỉ có phép</div>
                    <div style={{ fontSize: 'var(--fs-lg)', fontWeight: 800 }}>{history.attendanceSummary.absentExcusedCount}</div>
                  </div>
                  <div className="card" style={{ padding: 10 }}>
                    <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>Nghỉ không phép</div>
                    <div style={{ fontSize: 'var(--fs-lg)', fontWeight: 800, color: '#DC2626' }}>{history.attendanceSummary.absentUnexcusedCount}</div>
                  </div>
                  <div className="card" style={{ padding: 10 }}>
                    <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>Suất ăn bán trú</div>
                    <div style={{ fontSize: 'var(--fs-lg)', fontWeight: 800, color: 'var(--color-accent)' }}>
                      {history.lunchSummary.activeRegistrationsCount} <small style={{ fontSize: 11, fontWeight: 500 }}>(Hủy: {history.lunchSummary.cancelledCount})</small>
                    </div>
                  </div>
                </div>

                <div className="card" style={{ padding: 12 }}>
                  <div style={{ fontWeight: 800, fontSize: 13, marginBottom: 8 }}>Quá trình xếp lớp</div>
                  {history.enrollmentHistory.length === 0 ? (
                    <div style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>Chưa có dữ liệu xếp lớp.</div>
                  ) : (
                    <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 6 }}>
                      {history.enrollmentHistory.map((item) => (
                        <li key={item.assignmentId} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 'var(--fs-sm)', padding: '6px 8px', background: 'var(--color-bg)', borderRadius: 6 }}>
                          <span>
                            <strong>Lớp {item.className}</strong> ({item.academicYear}) {item.homeroomTeacherName ? `• GVCN: ${item.homeroomTeacherName}` : ''}
                          </span>
                          <span className={`badge ${item.isActive ? 'badge--mint' : item.isDraft ? 'badge--amber' : 'badge--violet'}`}>
                            {item.isActive ? 'Đang học' : item.isDraft ? 'Bản nháp' : `Kết thúc (${item.endDate ? formatDDMMYYYY(item.endDate) : '-'})`}
                          </span>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>

                <div className="card" style={{ padding: 12 }}>
                  <div style={{ fontWeight: 800, fontSize: 13, marginBottom: 8 }}>Đơn từ & yêu cầu hành chính ({history.requests.length})</div>
                  {history.requests.length === 0 ? (
                    <div style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)' }}>Chưa có đơn từ nào.</div>
                  ) : (
                    <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 6 }}>
                      {history.requests.map((req) => (
                        <li key={req.requestId} style={{ fontSize: 'var(--fs-sm)', padding: '6px 8px', background: 'var(--color-bg)', borderRadius: 6 }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                            <span><strong>{req.requestNumber}</strong> • {req.requestType}</span>
                            <span className="badge badge--violet">{req.status}</span>
                          </div>
                          {req.summary && <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginTop: 2 }}>{req.summary}</div>}
                        </li>
                      ))}
                    </ul>
                  )}
                </div>

                {history.auditLogs.length > 0 && (
                  <div className="card" style={{ padding: 12 }}>
                    <div style={{ fontWeight: 800, fontSize: 13, marginBottom: 8 }}>Nhật ký thao tác hồ sơ ({history.auditLogs.length})</div>
                    <ul style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 6, maxHeight: 160, overflowY: 'auto' }}>
                      {history.auditLogs.map((log, idx) => (
                        <li key={idx} style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                          <span><strong>{log.action}</strong> {log.actorName ? `bởi ${log.actorName}` : ''}</span>
                          <span>{formatDDMMYYYY(log.timestamp)}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </>
            )}
          </div>
        ) : (
        <>
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
                  min="1920-01-01"
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

              <div className="field">
                <label className="label">Trạng thái hồ sơ</label>
                <select
                  className="select"
                  value={editForm.status}
                  onChange={(e) => setEditForm({ ...editForm, status: e.target.value })}
                >
                  <option value="Active">Đang học (Active)</option>
                  <option value="Inactive">Ngừng hoạt động (Inactive)</option>
                  <option value="Archived">Chuyển trường / Lưu trữ (Archived)</option>
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
        </>
        )}

        {/* Actions */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 8, flexWrap: 'wrap', gap: 8 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
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
                <CheckCircle size={14} /> Hồ sơ đang hoạt động
              </span>
            )}
            {canManageGuardians && (
              <select
                className="select"
                aria-label="Đổi trạng thái học sinh"
                value={activeStudent.status}
                disabled={statusUpdating}
                onChange={(e) => void handleStatusChange(e.target.value)}
                style={{ width: 'auto', minHeight: 34, fontSize: 'var(--fs-xs)' }}
              >
                <option value="Active">Trạng thái: Đang học (Active)</option>
                <option value="Inactive">Ngừng hoạt động (Inactive)</option>
                <option value="Archived">Chuyển trường / Lưu trữ (Archived)</option>
              </select>
            )}
          </div>

          <button type="button" className="btn btn--secondary" onClick={onClose}>
            Đóng
          </button>
        </div>
      </div>
    </GeometricModal>
  );
};
