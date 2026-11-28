import { useEffect, useState } from 'react';
import { AlertCircle, Check, UserPlus } from 'lucide-react';
import toast from 'react-hot-toast';
import { GeometricModal } from '../common/GeometricModal';
import { studentApi } from '../../services/implementations/studentApi';
import type { CreateStudentDto, StudentProfileDto } from '../../types/student.types';
import type { ClassSummaryDto } from '../../types/class.types';

/** What the form edits; sent to the API with the backend's field names (toCreateStudentPayload). */
interface StudentFormState {
  studentCode: string;
  fullName: string;
  dateOfBirth: string;
  gender: string;
  address?: string;
  classId?: number;
  parentFullName?: string;
  parentPhone?: string;
  parentEmail?: string;
  parentIdentityCard?: string;
  parentRelationship?: string;
}

const RELATIONSHIP_VALUE: Record<string, number> = { 'Bố': 1, 'Mẹ': 2, 'Người giám hộ': 3 };

/**
 * Maps the form to POST /students. The form used to send classId / address / parent* fields that the backend does not
 * read, so the class, the address and the guardian were silently dropped.
 */
const toCreateStudentPayload = (f: StudentFormState): CreateStudentDto => {
  const guardianName = f.parentFullName?.trim();
  return {
    studentCode: f.studentCode.trim().toUpperCase(),
    fullName: f.fullName.trim(),
    dateOfBirth: f.dateOfBirth,
    gender: f.gender,
    residentialArea: f.address?.trim() || undefined,
    initialClassId: f.classId,
    initialGuardian: guardianName
      ? {
          fullName: guardianName,
          phoneNumber: f.parentPhone?.trim() || undefined,
          email: f.parentEmail?.trim() || undefined,
          identityCardNumber: f.parentIdentityCard?.trim() || undefined,
          relationship: RELATIONSHIP_VALUE[f.parentRelationship ?? ''] ?? 3,
          isPrimaryContact: true,
        }
      : undefined,
  };
};

interface CreateStudentModalProps {
  open: boolean;
  onClose: () => void;
  classes: ClassSummaryDto[];
  onCreated: (student: StudentProfileDto) => void;
  onDuplicateDetected: (pending: CreateStudentDto, existing: StudentProfileDto) => void;
}

const createInitialFormState = (): StudentFormState => ({
  studentCode: '',
  fullName: '',
  dateOfBirth: '2020-01-01',
  gender: 'Nam',
  address: '',
  classId: undefined,
  parentFullName: '',
  parentPhone: '',
  parentEmail: '',
  parentIdentityCard: '',
  parentRelationship: 'Bố',
});

const PERSON_NAME_REGEX = /^[\p{L}\s'\-.]+$/u;
const HAS_LETTER_REGEX = /\p{L}/u;

export const CreateStudentModal: React.FC<CreateStudentModalProps> = ({
  open,
  onClose,
  classes,
  onCreated,
  onDuplicateDetected,
}) => {
  const [formData, setFormData] = useState<StudentFormState>(createInitialFormState);

  const [loading, setLoading] = useState(false);
  const [dupChecking, setDupChecking] = useState(false);
  const [dupWarning, setDupWarning] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setFormData(createInitialFormState());
      setDupWarning(null);
    }
  }, [open]);

  const checkCodeDuplicate = async (code: string) => {
    if (!code.trim()) return;
    setDupChecking(true);
    setDupWarning(null);
    try {
      const res = await studentApi.checkDuplicate(code.trim().toUpperCase());
      if (res.data?.isDuplicate) {
        setDupWarning(res.data.message || 'Mã học sinh đã tồn tại!');
      }
    } catch {
      // ignore
    } finally {
      setDupChecking(false);
    }
  };

  const currentYear = new Date().getFullYear();
  const maxBirthDate = `${currentYear - 5}-12-31`;
  const minBirthDate = '1920-01-01';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.studentCode.trim() || !formData.fullName.trim()) {
      toast.error('Vui lòng điền đầy đủ Mã học sinh và Họ tên!');
      return;
    }

    const code = formData.studentCode.trim().toUpperCase();
    if (!/^[A-Z0-9_-]{3,30}$/.test(code)) {
      toast.error('Mã học sinh chỉ được chứa chữ cái, chữ số, dấu gạch ngang (-) và gạch dưới (_) từ 3 đến 30 ký tự!');
      return;
    }

    const name = formData.fullName.trim().replace(/\s+/g, ' ');
    if (name.length < 2 || name.length > 100) {
      toast.error('Họ và tên học sinh phải có độ dài từ 2 đến 100 ký tự.');
      return;
    }
    if (name.includes('<') || name.includes('>')) {
      toast.error('Họ và tên học sinh không được chứa thẻ HTML!');
      return;
    }
    if (!PERSON_NAME_REGEX.test(name) || !HAS_LETTER_REGEX.test(name)) {
      toast.error("Họ và tên học sinh chỉ được chứa chữ cái, khoảng trắng và dấu (' - .), không được chứa chữ số hoặc ký tự đặc biệt!");
      return;
    }

    if (formData.classId) {
      const targetCls = classes.find((c) => c.id === formData.classId);
      const enrolled = targetCls ? (targetCls.enrolledCount ?? targetCls.enrolledStudentsCount ?? 0) : 0;
      if (targetCls && targetCls.capacity > 0 && enrolled >= targetCls.capacity) {
        toast.error(`Lớp ${targetCls.className} đã đủ sĩ số (${enrolled}/${targetCls.capacity}). Vui lòng chọn lớp khác hoặc chọn "Chưa xếp lớp"!`);
        return;
      }
    }

    if (!formData.dateOfBirth) {
      toast.error('Vui lòng chọn ngày sinh của học sinh!');
      return;
    }

    const birthDate = new Date(formData.dateOfBirth);
    const today = new Date();
    if (isNaN(birthDate.getTime()) || birthDate >= today || birthDate.getFullYear() < 1900) {
      toast.error('Ngày sinh không hợp lệ (không thể là ngày trong tương lai)!');
      return;
    }

    const age = today.getFullYear() - birthDate.getFullYear();
    if (age < 5) {
      toast.error(`Độ tuổi của học sinh không hợp lệ (${age} tuổi). Học sinh tiểu học phải từ 5 tuổi trở lên.`);
      return;
    }

    let allowAgeException = false;
    let ageExceptionReason: string | undefined;
    if (age > 15) {
      const confirmed = window.confirm(
        `Cảnh báo ngoại lệ độ tuổi: Học sinh này ${age} tuổi (vượt quá trần tiểu học 5–15 tuổi).\n\nBạn có chắc chắn muốn xác nhận trường hợp ngoại lệ quá tuổi và lưu hồ sơ này (sẽ ghi nhật ký hệ thống)?`
      );
      if (!confirmed) {
        return;
      }
      allowAgeException = true;
      ageExceptionReason = `Xác nhận ngoại lệ học sinh quá tuổi (${age} tuổi) khi tạo hồ sơ.`;
    }

    const phone = formData.parentPhone?.trim();
    const email = formData.parentEmail?.trim();
    const cccd = formData.parentIdentityCard?.trim();
    const parentName = formData.parentFullName?.trim();

    if ((phone || email || cccd) && !parentName) {
      toast.error('Vui lòng nhập Họ tên phụ huynh khi cung cấp thông tin liên hệ (SĐT / Email / CCCD)!');
      return;
    }

    if (parentName) {
      const cleanedParentName = parentName.replace(/\s+/g, ' ');
      if (cleanedParentName.includes('<') || cleanedParentName.includes('>')) {
        toast.error('Họ tên phụ huynh không được chứa thẻ HTML!');
        return;
      }
      if (!PERSON_NAME_REGEX.test(cleanedParentName) || !HAS_LETTER_REGEX.test(cleanedParentName)) {
        toast.error("Họ tên phụ huynh chỉ được chứa chữ cái, khoảng trắng và dấu (' - .), không được chứa chữ số hoặc ký tự đặc biệt!");
        return;
      }
    }

    if (phone) {
      if (!/^0\d{9}$/.test(phone)) {
        toast.error('Số điện thoại liên hệ phải gồm đúng 10 chữ số (bắt đầu bằng số 0, ví dụ: 0912345678)!');
        return;
      }
    }

    if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      toast.error('Email phụ huynh không đúng định dạng (ví dụ: phuhuynh@gmail.com)!');
      return;
    }

    if (cccd && !/^\d{9}(\d{3})?$/.test(cccd)) {
      toast.error('Số CCCD/CMND phụ huynh phải gồm 9 hoặc 12 chữ số!');
      return;
    }

    setLoading(true);
    const payload: CreateStudentDto = {
      ...toCreateStudentPayload(formData),
      allowAgeException,
      ageExceptionReason,
    };
    try {
      const res = await studentApi.createStudent(payload);

      if (res.success && res.data) {
        toast.success(res.message || 'Tạo hồ sơ học sinh thành công!');
        onCreated(res.data);
        onClose();
      } else {
        toast.error(res.message || 'Tạo hồ sơ thất bại.');
      }
    } catch (err: any) {
      // 409 Conflict - MSG12 duplicate detection
      if (err.response?.status === 409) {
        toast.error('Mã học sinh đã tồn tại trong hệ thống. Đang chuyển sang màn hình giải quyết trùng lặp.');
        // fetch existing info
        const dupRes = await studentApi.checkDuplicate(formData.studentCode.trim().toUpperCase());
        if (dupRes.data?.existingStudent) {
          onDuplicateDetected(payload, dupRes.data.existingStudent);
          onClose();
        }
      } else {
        // e.g. MSG01 with the reason (invalid email, conflicting identity card / email of guardian records)
        toast.error(err.response?.data?.errors?.[0] || err.response?.data?.message || 'Lỗi khi tạo hồ sơ học sinh.');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={onClose}
      title="Thêm hồ sơ học sinh mới"
      badgeText="Tạo mới hồ sơ"
      badgeColor="mint"
      icon={<UserPlus size={24} color="#1E293B" strokeWidth={2.5} />}
      iconBg="var(--color-quaternary)"
      maxWidth="lg"
    >
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Section 1: Thông tin học sinh */}
        <div>
          <h4 style={{ fontSize: 'var(--fs-sm)', textTransform: 'uppercase', color: 'var(--color-accent)', marginBottom: 12 }}>
            1. Thông tin học sinh
          </h4>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            <div className="field">
              <label className="label">Mã học sinh (*)</label>
              <input
                type="text"
                className="input"
                placeholder="VD: HS2026001"
                required
                value={formData.studentCode}
                onChange={(e) => {
                  setFormData({ ...formData, studentCode: e.target.value.toUpperCase() });
                  setDupWarning(null);
                }}
                onBlur={(e) => checkCodeDuplicate(e.target.value)}
              />
              {dupChecking && <small style={{ color: 'var(--color-muted-fg)' }}>Đang kiểm tra trùng lặp...</small>}
              {dupWarning && (
                <div style={{ color: 'var(--color-secondary)', fontSize: 'var(--fs-xs)', display: 'flex', alignItems: 'center', gap: 4, marginTop: 4 }}>
                  <AlertCircle size={14} /> {dupWarning}
                </div>
              )}
            </div>

            <div className="field">
              <label className="label">Họ và tên (*)</label>
              <input
                type="text"
                className="input"
                placeholder="Nguyễn Văn An"
                required
                value={formData.fullName}
                onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
              />
            </div>

            <div className="field">
              <label className="label">Ngày sinh (*)</label>
              <input
                type="date"
                className="input"
                required
                max={maxBirthDate}
                min={minBirthDate}
                value={formData.dateOfBirth}
                onChange={(e) => setFormData({ ...formData, dateOfBirth: e.target.value })}
              />
              <small style={{ color: 'var(--color-muted-fg)', marginTop: 4, display: 'block' }}>
                Độ tuổi tiểu học chuẩn: từ 5 đến 15 tuổi. Trường hợp quá 15 tuổi sẽ yêu cầu xác nhận ngoại lệ khi lưu.
              </small>
            </div>

            <div className="field">
              <label className="label">Giới tính</label>
              <select
                className="select"
                value={formData.gender}
                onChange={(e) => setFormData({ ...formData, gender: e.target.value })}
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
                value={formData.address || ''}
                onChange={(e) => setFormData({ ...formData, address: e.target.value })}
              />
            </div>

            <div className="field" style={{ gridColumn: 'span 2' }}>
              <label className="label">Xếp lớp dự kiến (Tùy chọn)</label>
              <select
                className="select"
                value={formData.classId || ''}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    classId: e.target.value ? Number(e.target.value) : undefined,
                  })
                }
              >
                <option value="">-- Chưa xếp lớp (Xếp lớp sau) --</option>
                {classes.map((c) => {
                  const enrolled = c.enrolledCount ?? c.enrolledStudentsCount ?? 0;
                  const isFull = c.capacity > 0 && enrolled >= c.capacity;
                  return (
                    <option key={c.id} value={c.id} disabled={isFull}>
                      Lớp {c.className} (Khối {c.grade}) - {isFull ? `Đã đầy (${enrolled}/${c.capacity})` : `Hiện có ${enrolled}/${c.capacity} học sinh`}
                    </option>
                  );
                })}
              </select>
            </div>
          </div>
        </div>

        {/* Section 2: Thông tin phụ huynh */}
        <div style={{ borderTop: '2px dashed var(--color-border)', paddingTop: 14 }}>
          <h4 style={{ fontSize: 'var(--fs-sm)', textTransform: 'uppercase', color: 'var(--color-secondary)', marginBottom: 12 }}>
            2. Thông tin phụ huynh / Người giám hộ
          </h4>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
            <div className="field">
              <label className="label">Họ tên phụ huynh</label>
              <input
                type="text"
                className="input"
                placeholder="Nguyễn Văn Nam"
                value={formData.parentFullName || ''}
                onChange={(e) => setFormData({ ...formData, parentFullName: e.target.value })}
              />
            </div>

            <div className="field">
              <label className="label">Số điện thoại liên hệ</label>
              <input
                type="tel"
                className="input"
                placeholder="0912345678"
                maxLength={10}
                value={formData.parentPhone || ''}
                onChange={(e) => setFormData({ ...formData, parentPhone: e.target.value })}
              />
              <small style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>
                Bao gồm đúng 10 chữ số, bắt đầu bằng số 0 (ví dụ: 0912345678).
              </small>
            </div>

            <div className="field">
              <label className="label">Email phụ huynh</label>
              <input
                type="email"
                className="input"
                placeholder="ten@example.com"
                value={formData.parentEmail || ''}
                onChange={(e) => setFormData({ ...formData, parentEmail: e.target.value })}
              />
              <small style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>
                Không bắt buộc. Cần có email để sau này cấp tài khoản phụ huynh.
              </small>
            </div>

            <div className="field">
              <label className="label">Số CCCD / CMND phụ huynh</label>
              <input
                type="text"
                className="input"
                placeholder="001200123456"
                value={formData.parentIdentityCard || ''}
                onChange={(e) => setFormData({ ...formData, parentIdentityCard: e.target.value })}
              />
            </div>

            <div className="field">
              <label className="label">Mối quan hệ</label>
              <select
                className="select"
                value={formData.parentRelationship || 'Bố'}
                onChange={(e) => setFormData({ ...formData, parentRelationship: e.target.value })}
              >
                <option value="Bố">Bố</option>
                <option value="Mẹ">Mẹ</option>
                <option value="Người giám hộ">Người giám hộ</option>
              </select>
            </div>
          </div>
        </div>

        {/* Action Buttons */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 10 }}>
          <button type="button" className="btn btn--secondary" onClick={onClose} disabled={loading}>
            Hủy
          </button>
          <button type="submit" className="btn btn--primary" disabled={loading}>
            {loading ? 'Đang tạo...' : 'Lưu hồ sơ học sinh'}
            <Check size={18} />
          </button>
        </div>
      </form>
    </GeometricModal>
  );
};
