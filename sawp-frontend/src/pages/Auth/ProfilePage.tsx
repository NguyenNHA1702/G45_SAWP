import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import {
  User,
  Mail,
  Phone,
  MapPin,
  Briefcase,
  ShieldCheck,
  KeyRound,
  GraduationCap,
  CheckCircle2,
  Calendar,
  Sparkles,
  School,
  HeartHandshake,
  AlertCircle,
  RefreshCw,
  IdCard,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { profileApi } from '../../services/implementations/profileApi';
import { ChangePasswordModal } from '../../components/auth/ChangePasswordModal';
import { USER_KEY } from '../../services/apiClient';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import { asApiError } from '../../utils/apiError';
import type { UserProfile } from '../../types/profile.types';

export default function ProfilePage() {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [passwordModalOpen, setPasswordModalOpen] = useState(false);

  // Editable form state
  const [fullName, setFullName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [email, setEmail] = useState('');
  const [address, setAddress] = useState('');
  const [occupation, setOccupation] = useState('');

  const loadProfile = async () => {
    setLoading(true);
    try {
      const data = await profileApi.getProfile();
      setProfile(data);
      setFullName(data.fullName || '');
      setPhoneNumber(data.phoneNumber || '');
      setEmail(data.email || '');
      if (data.guardianProfile) {
        setAddress(data.guardianProfile.address || '');
        setOccupation(data.guardianProfile.occupation || '');
      }
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.message || 'Không thể tải thông tin hồ sơ.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadProfile();
  }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!fullName.trim()) {
      toast.error(SYSTEM_MESSAGES.MSG01.content);
      return;
    }

    setSaving(true);
    try {
      const updated = await profileApi.updateProfile({
        fullName: fullName.trim(),
        phoneNumber: phoneNumber.trim() || undefined,
        email: email.trim() || undefined,
        address: profile?.guardianProfile ? address.trim() || undefined : undefined,
        occupation: profile?.guardianProfile ? occupation.trim() || undefined : undefined,
      });

      setProfile(updated);
      setFullName(updated.fullName);
      setPhoneNumber(updated.phoneNumber || '');
      setEmail(updated.email || '');
      if (updated.guardianProfile) {
        setAddress(updated.guardianProfile.address || '');
        setOccupation(updated.guardianProfile.occupation || '');
      }

      // Sync local storage so header display updates immediately
      try {
        const storedUserJson = localStorage.getItem(USER_KEY);
        if (storedUserJson) {
          const storedUser = JSON.parse(storedUserJson);
          storedUser.fullName = updated.fullName;
          storedUser.email = updated.email;
          localStorage.setItem(USER_KEY, JSON.stringify(storedUser));
          window.dispatchEvent(new Event('storage'));
        }
      } catch {
        // silent
      }

      toast.success(SYSTEM_MESSAGES.MSG47.content);
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.message || 'Cập nhật hồ sơ thất bại.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="container" style={{ padding: '48px 16px', textAlign: 'center' }}>
        <div style={{ display: 'inline-flex', alignItems: 'center', gap: 10, fontWeight: 600 }}>
          <RefreshCw className="animate-spin" size={20} color="var(--color-primary)" />
          <span>Đang tải thông tin hồ sơ cá nhân...</span>
        </div>
      </div>
    );
  }

  if (!profile) {
    return (
      <div className="container" style={{ padding: '48px 16px', textAlign: 'center' }}>
        <div className="banner banner--danger" style={{ maxWidth: 500, margin: '0 auto' }}>
          <AlertCircle size={20} />
          <span>Không thể tải hồ sơ. Vui lòng thử lại sau.</span>
        </div>
        <button
          type="button"
          className="btn btn--secondary btn--sm"
          style={{ marginTop: 16 }}
          onClick={loadProfile}
        >
          <RefreshCw size={14} /> Thử lại
        </button>
      </div>
    );
  }

  // Get initial letters for avatar
  const avatarText = profile.fullName
    ? profile.fullName
        .split(' ')
        .map((w) => w[0])
        .filter(Boolean)
        .slice(-2)
        .join('')
        .toUpperCase()
    : profile.username.substring(0, 2).toUpperCase();

  const formattedJoinDate = profile.createdAt
    ? new Date(profile.createdAt).toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      })
    : 'N/A';

  return (
    <div className="container pop-in">
      {/* Memphis decorative elements */}
      <span className="decor decor--circle" style={{ top: -20, right: 30 }} aria-hidden="true" />
      <span className="decor decor--triangle" style={{ bottom: 30, left: 10 }} aria-hidden="true" />

      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <span className="badge badge--violet" style={{ marginBottom: 8, display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <Sparkles size={14} /> Tài khoản hệ thống SAWP
            </span>
            <h1 style={{ margin: '4px 0 8px' }}>
              Hồ sơ <span className="squiggle">cá nhân</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0 }}>
              Quản lý thông tin định danh, liên hệ cá nhân và vai trò công tác của bạn tại trường.
            </p>
          </div>
        </div>
      </header>

      {/* Main 2-column Grid */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
          gap: 24,
          alignItems: 'start',
        }}
      >
        {/* Left Column: Identity Sticker Card */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          <div className="card" style={{ textAlign: 'center', padding: '32px 24px' }}>
            {/* Big Avatar Sticker */}
            <div
              style={{
                width: 84,
                height: 84,
                margin: '0 auto 16px',
                borderRadius: '50%',
                background: 'var(--color-accent-soft)',
                border: '3px solid var(--color-border)',
                boxShadow: 'var(--shadow-pop)',
                display: 'grid',
                placeItems: 'center',
                fontSize: 'var(--fs-xl)',
                fontWeight: 800,
                color: 'var(--color-accent)',
                fontFamily: 'var(--font-heading)',
              }}
            >
              {avatarText}
            </div>

            <h2 style={{ fontSize: 'var(--fs-lg)', margin: '0 0 4px' }}>{profile.fullName}</h2>
            <div style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)', marginBottom: 12 }}>
              @{profile.username}
            </div>

            {/* Role Badges */}
            <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'center', gap: 6, marginBottom: 20 }}>
              {profile.roles.map((r) => (
                <span
                  key={r}
                  className={`badge ${
                    r === 'Admin'
                      ? 'badge--pink'
                      : r === 'Teacher'
                      ? 'badge--mint'
                      : r === 'Parent'
                      ? 'badge--amber'
                      : 'badge--violet'
                  }`}
                >
                  {r === 'Admin'
                    ? 'Quản trị viên'
                    : r === 'SchoolBoard'
                    ? 'Ban Giám Hiệu'
                    : r === 'OfficeStaff'
                    ? 'Cán bộ Giáo vụ'
                    : r === 'Teacher'
                    ? 'Giáo viên'
                    : r === 'ServiceStaff'
                    ? 'Nhân viên Bếp / Dịch vụ'
                    : r === 'Parent'
                    ? 'Phụ huynh'
                    : r}
                </span>
              ))}
            </div>

            {/* Account Metadata List */}
            <div
              style={{
                borderTop: '2px dashed var(--color-border)',
                paddingTop: 16,
                marginTop: 8,
                textAlign: 'left',
                display: 'flex',
                flexDirection: 'column',
                gap: 12,
                fontSize: 'var(--fs-sm)',
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ color: 'var(--color-muted-fg)' }}>Trạng thái tài khoản</span>
                <span
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 6,
                    fontWeight: 600,
                    color: profile.isActive ? 'var(--status-present)' : 'var(--color-secondary)',
                  }}
                >
                  <span
                    style={{
                      width: 8,
                      height: 8,
                      borderRadius: '50%',
                      background: profile.isActive ? 'var(--status-present)' : 'var(--color-secondary)',
                    }}
                  />
                  {profile.isActive ? 'Đang hoạt động' : 'Tạm khóa'}
                </span>
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ color: 'var(--color-muted-fg)' }}>Ngày tham gia</span>
                <span style={{ fontWeight: 600 }}>{formattedJoinDate}</span>
              </div>
            </div>

            {/* Quick action: Change password */}
            <div style={{ marginTop: 24 }}>
              <button
                type="button"
                className="btn btn--secondary"
                style={{ width: '100%', justifyContent: 'center' }}
                onClick={() => setPasswordModalOpen(true)}
              >
                <KeyRound size={16} />
                <span>Đổi mật khẩu tài khoản</span>
              </button>
            </div>
          </div>
        </div>

        {/* Right Column: Profile Form & Role Details */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24, minWidth: 0 }}>
          {/* Card 1: Editable Contact Info */}
          <div className="card">
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 16 }}>
              <div
                style={{
                  width: 38,
                  height: 38,
                  borderRadius: '50%',
                  background: 'var(--color-tertiary-soft)',
                  border: 'var(--border)',
                  display: 'grid',
                  placeItems: 'center',
                }}
              >
                <User size={18} color="var(--color-tertiary)" />
              </div>
              <div>
                <h3 style={{ margin: 0 }}>Thông tin cá nhân & Liên hệ</h3>
                <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Cập nhật họ tên hiển thị và các kênh liên lạc của bạn
                </p>
              </div>
            </div>

            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              {/* Họ và tên */}
              <div className="field">
                <label className="label" htmlFor="profile-fullname">
                  Họ và tên <span style={{ color: 'var(--color-secondary)' }}>*</span>
                </label>
                <div>
                  <input
                    id="profile-fullname"
                    type="text"
                    className="input"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    placeholder="Nhập họ và tên đầy đủ"
                    required
                  />
                </div>
              </div>

              {/* Grid 2 cột: Điện thoại & Email */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 16 }}>
                <div className="field">
                  <label className="label" htmlFor="profile-phone">
                    <Phone size={14} style={{ display: 'inline', verticalAlign: '-2px', marginRight: 4 }} />
                    Số điện thoại
                  </label>
                  <input
                    id="profile-phone"
                    type="tel"
                    className="input"
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                    placeholder="09xxxxxxxx"
                  />
                </div>

                <div className="field">
                  <label className="label" htmlFor="profile-email">
                    <Mail size={14} style={{ display: 'inline', verticalAlign: '-2px', marginRight: 4 }} />
                    Địa chỉ Email
                  </label>
                  <input
                    id="profile-email"
                    type="email"
                    className="input"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="user@school.edu.vn"
                  />
                </div>
              </div>

              {/* Nếu là Phụ huynh: thêm Địa chỉ và Nghề nghiệp */}
              {profile.guardianProfile && (
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 16 }}>
                  <div className="field">
                    <label className="label" htmlFor="profile-address">
                      <MapPin size={14} style={{ display: 'inline', verticalAlign: '-2px', marginRight: 4 }} />
                      Địa chỉ liên hệ
                    </label>
                    <input
                      id="profile-address"
                      type="text"
                      className="input"
                      value={address}
                      onChange={(e) => setAddress(e.target.value)}
                      placeholder="Số nhà, đường, quận/huyện..."
                    />
                  </div>

                  <div className="field">
                    <label className="label" htmlFor="profile-occupation">
                      <Briefcase size={14} style={{ display: 'inline', verticalAlign: '-2px', marginRight: 4 }} />
                      Nghề nghiệp
                    </label>
                    <input
                      id="profile-occupation"
                      type="text"
                      className="input"
                      value={occupation}
                      onChange={(e) => setOccupation(e.target.value)}
                      placeholder="Nghề nghiệp / Đơn vị công tác"
                    />
                  </div>
                </div>
              )}

              {/* Save Button */}
              <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 8 }}>
                <button
                  type="submit"
                  className="btn btn--primary"
                  disabled={saving}
                >
                  {saving ? (
                    <>
                      <RefreshCw className="animate-spin" size={16} />
                      <span>Đang lưu thay đổi...</span>
                    </>
                  ) : (
                    <>
                      <CheckCircle2 size={16} />
                      <span>Lưu thay đổi</span>
                    </>
                  )}
                </button>
              </div>
            </form>
          </div>

          {/* Card 2: Role Details (Staff or Guardian) */}
          {profile.staffProfile && (
            <div className="card">
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16, flexWrap: 'wrap', gap: 8 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <div
                    style={{
                      width: 38,
                      height: 38,
                      borderRadius: '50%',
                      background: 'var(--color-accent-soft)',
                      border: 'var(--border)',
                      display: 'grid',
                      placeItems: 'center',
                    }}
                  >
                    <School size={18} color="var(--color-accent)" />
                  </div>
                  <div>
                    <h3 style={{ margin: 0 }}>Hồ sơ công tác cán bộ</h3>
                    <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                      Thông tin chức danh, tổ bộ môn do Nhà trường quản lý
                    </p>
                  </div>
                </div>
                <span className="badge badge--neutral">
                  <ShieldCheck size={12} style={{ marginRight: 4 }} /> Do nhà trường quản lý
                </span>
              </div>

              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                  gap: 16,
                }}
              >
                <div style={{ padding: 14, background: 'var(--color-bg)', border: 'var(--border)', borderRadius: 'var(--radius-md)' }}>
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginBottom: 4 }}>
                    Mã số cán bộ (Staff Code)
                  </div>
                  <div style={{ fontWeight: 700, fontSize: 'var(--fs-base)', fontFamily: 'var(--font-heading)' }}>
                    {profile.staffProfile.staffCode || 'Chưa cập nhật'}
                  </div>
                </div>

                <div style={{ padding: 14, background: 'var(--color-bg)', border: 'var(--border)', borderRadius: 'var(--radius-md)' }}>
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginBottom: 4 }}>
                    Phòng ban / Tổ chuyên môn
                  </div>
                  <div style={{ fontWeight: 700, fontSize: 'var(--fs-base)' }}>
                    {profile.staffProfile.department || 'Ban Giám Hiệu'}
                  </div>
                </div>

                <div style={{ padding: 14, background: 'var(--color-bg)', border: 'var(--border)', borderRadius: 'var(--radius-md)' }}>
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginBottom: 4 }}>
                    Vị trí / Chức vụ
                  </div>
                  <div style={{ fontWeight: 700, fontSize: 'var(--fs-base)' }}>
                    {profile.staffProfile.position || 'Cán bộ'}
                  </div>
                </div>

                <div style={{ padding: 14, background: 'var(--color-bg)', border: 'var(--border)', borderRadius: 'var(--radius-md)' }}>
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', marginBottom: 4 }}>
                    Phân công Chủ nhiệm
                  </div>
                  <div style={{ fontWeight: 700, fontSize: 'var(--fs-base)', display: 'flex', alignItems: 'center', gap: 6 }}>
                    <GraduationCap size={16} color="var(--color-primary)" />
                    {profile.staffProfile.isHomeroomTeacher
                      ? `Lớp ${profile.staffProfile.homeroomClassName || 'Đang phân công'}`
                      : 'Không phụ trách GVCN'}
                  </div>
                </div>
              </div>
            </div>
          )}

          {profile.guardianProfile && (
            <div className="card">
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16, flexWrap: 'wrap', gap: 8 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <div
                    style={{
                      width: 38,
                      height: 38,
                      borderRadius: '50%',
                      background: 'var(--status-present-soft)',
                      border: 'var(--border)',
                      display: 'grid',
                      placeItems: 'center',
                    }}
                  >
                    <HeartHandshake size={18} color="var(--status-present)" />
                  </div>
                  <div>
                    <h3 style={{ margin: 0 }}>Hồ sơ phụ huynh & Giám hộ</h3>
                    <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                      Định danh công dân và danh sách các con đang theo học
                    </p>
                  </div>
                </div>
                <span className="badge badge--mint">
                  <ShieldCheck size={12} style={{ marginRight: 4 }} /> Đã xác thực CCCD
                </span>
              </div>

              {/* Identity card info */}
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 12,
                  padding: 14,
                  background: 'var(--color-bg)',
                  border: 'var(--border)',
                  borderRadius: 'var(--radius-md)',
                  marginBottom: 16,
                }}
              >
                <IdCard size={20} color="var(--color-muted-fg)" />
                <div>
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>Số CMND / Căn cước công dân</div>
                  <div style={{ fontWeight: 700, fontFamily: 'monospace', fontSize: 'var(--fs-base)' }}>
                    {profile.guardianProfile.identityCardNumber || 'Chưa định danh'}
                  </div>
                </div>
              </div>

              {/* Children Table */}
              <div style={{ marginTop: 12 }}>
                <h4 style={{ fontSize: 'var(--fs-sm)', marginBottom: 8, display: 'flex', alignItems: 'center', gap: 6 }}>
                  <GraduationCap size={16} /> Danh sách học sinh phụ trách ({profile.guardianProfile.children.length})
                </h4>

                {profile.guardianProfile.children.length === 0 ? (
                  <div className="banner banner--info" style={{ fontSize: 'var(--fs-sm)' }}>
                    Chưa có học sinh nào được liên kết với tài khoản này. Vui lòng gửi yêu cầu liên kết hồ sơ con tại Cổng Phụ huynh.
                  </div>
                ) : (
                  <div className="data-table-wrapper">
                    <table className="data-table">
                      <thead>
                        <tr>
                          <th>Mã HS</th>
                          <th>Họ và tên con</th>
                          <th>Lớp học</th>
                          <th>Ngày sinh</th>
                          <th>Mối quan hệ</th>
                          <th style={{ textAlign: 'center' }}>Liên hệ chính</th>
                        </tr>
                      </thead>
                      <tbody>
                        {profile.guardianProfile.children.map((child) => (
                          <tr key={child.studentId}>
                            <td style={{ fontWeight: 700, fontFamily: 'monospace' }}>{child.studentCode}</td>
                            <td style={{ fontWeight: 600 }}>{child.fullName}</td>
                            <td>
                              {child.className ? (
                                <span className="badge badge--mint">Lớp {child.className}</span>
                              ) : (
                                <span style={{ color: 'var(--color-muted-fg)' }}>Chưa xếp lớp</span>
                              )}
                            </td>
                            <td>
                              <Calendar size={13} style={{ display: 'inline', verticalAlign: '-2px', marginRight: 4 }} />
                              {child.dateOfBirth
                                ? new Date(child.dateOfBirth).toLocaleDateString('vi-VN')
                                : 'N/A'}
                            </td>
                            <td>{child.relationship || 'Phụ huynh'}</td>
                            <td style={{ textAlign: 'center' }}>
                              {child.isPrimaryContact ? (
                                <span className="badge badge--amber" style={{ fontSize: 'var(--fs-xs)' }}>
                                  ⭐ Người liên hệ chính
                                </span>
                              ) : (
                                <span style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-xs)' }}>
                                  Phụ
                                </span>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Change Password Modal */}
      <ChangePasswordModal
        open={passwordModalOpen}
        onClose={() => setPasswordModalOpen(false)}
      />
    </div>
  );
}
