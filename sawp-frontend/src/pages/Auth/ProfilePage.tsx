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
    <div className="container pop-in" style={{ paddingBottom: 60 }}>
      {/* Page Header */}
      <header className="page-header" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8 }}>
              <span className="badge badge--violet" style={{ border: '2px solid var(--color-fg)', padding: '5px 14px', borderRadius: 'var(--radius-full)', fontWeight: 800 }}>
                <Sparkles size={14} /> Tài khoản hệ thống SAWP
              </span>
            </div>
            <h1 style={{ margin: '4px 0 8px' }}>
              Hồ Sơ <span className="squiggle">Cá Nhân</span>
            </h1>
            <p style={{ color: 'var(--color-muted-fg)', margin: 0, fontSize: 'var(--fs-base)' }}>
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
          <div
            className="card"
            style={{
              textAlign: 'center',
              padding: '32px 24px',
              background: '#FFFDF5',
              border: '2px solid var(--color-fg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
              borderRadius: 'var(--radius-lg)',
            }}
          >
            {/* Big Avatar Sticker */}
            <div
              style={{
                width: 88,
                height: 88,
                margin: '0 auto 16px',
                borderRadius: '50%',
                background: '#EDE9FE',
                border: '2.5px solid var(--color-fg)',
                boxShadow: '3px 3px 0 0 var(--color-fg)',
                display: 'grid',
                placeItems: 'center',
                fontSize: 'var(--fs-2xl)',
                fontWeight: 900,
                color: 'var(--color-accent)',
                fontFamily: 'var(--font-heading)',
              }}
            >
              {avatarText}
            </div>

            <h2 style={{ fontSize: 'var(--fs-xl)', margin: '0 0 4px', fontFamily: 'var(--font-heading)', fontWeight: 800 }}>
              {profile.fullName}
            </h2>
            <div style={{ color: 'var(--color-muted-fg)', fontSize: 'var(--fs-sm)', marginBottom: 14, fontWeight: 600 }}>
              @{profile.username}
            </div>

            {/* Role Badges */}
            <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'center', gap: 8, marginBottom: 20 }}>
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
                  style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800, padding: '4px 12px' }}
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
                borderTop: '2px dashed var(--color-fg)',
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
                <span style={{ color: 'var(--color-muted-fg)', fontWeight: 600 }}>Trạng thái tài khoản</span>
                <span
                  className="badge badge--mint"
                  style={{ border: '1px solid var(--color-fg)', fontWeight: 700 }}
                >
                  ● {profile.isActive ? 'Đang hoạt động' : 'Tạm khóa'}
                </span>
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ color: 'var(--color-muted-fg)', fontWeight: 600 }}>Ngày tham gia</span>
                <span style={{ fontWeight: 800, color: 'var(--color-fg)' }}>{formattedJoinDate}</span>
              </div>
            </div>

            {/* Quick action: Change password */}
            <div style={{ marginTop: 24 }}>
              <button
                type="button"
                className="btn btn--secondary"
                style={{
                  width: '100%',
                  justifyContent: 'center',
                  borderRadius: 'var(--radius-full)',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  fontWeight: 700,
                  padding: '10px 18px',
                }}
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
          <div
            className="card"
            style={{
              background: '#FFFFFF',
              border: '2px solid var(--color-fg)',
              boxShadow: '4px 4px 0 0 var(--color-fg)',
              borderRadius: 'var(--radius-lg)',
              padding: '24px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 20 }}>
              <div
                style={{
                  width: 44,
                  height: 44,
                  borderRadius: '50%',
                  background: '#FEF3C7',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  display: 'grid',
                  placeItems: 'center',
                  flexShrink: 0,
                }}
              >
                <User size={22} color="#B45309" strokeWidth={2.5} />
              </div>
              <div>
                <h3 style={{ margin: 0, fontFamily: 'var(--font-heading)', fontSize: 'var(--fs-md)' }}>
                  Thông tin cá nhân & Liên hệ
                </h3>
                <p style={{ margin: '2px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                  Cập nhật họ tên hiển thị và các kênh liên lạc của bạn
                </p>
              </div>
            </div>

            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              {/* Họ và tên */}
              <div className="field">
                <label className="label" htmlFor="profile-fullname" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
                  HỌ VÀ TÊN <span style={{ color: 'var(--color-secondary)' }}>*</span>
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
                    style={{
                      borderRadius: 'var(--radius-full)',
                      border: '2px solid var(--color-fg)',
                      padding: '9px 18px',
                      fontWeight: 700,
                    }}
                  />
                </div>
              </div>

              {/* Grid 2 cột: Điện thoại & Email */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 16 }}>
                <div className="field">
                  <label className="label" htmlFor="profile-phone" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
                    <Phone size={13} style={{ display: 'inline', verticalAlign: '-2px', marginRight: 4 }} />
                    SỐ ĐIỆN THOẠI
                  </label>
                  <input
                    id="profile-phone"
                    type="tel"
                    className="input"
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                    placeholder="09xxxxxxxx"
                    style={{
                      borderRadius: 'var(--radius-full)',
                      border: '2px solid var(--color-fg)',
                      padding: '9px 18px',
                      fontWeight: 700,
                    }}
                  />
                </div>

                <div className="field">
                  <label className="label" htmlFor="profile-email" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
                    <Mail size={13} style={{ display: 'inline', verticalAlign: '-2px', marginRight: 4 }} />
                    ĐỊA CHỈ EMAIL
                  </label>
                  <input
                    id="profile-email"
                    type="email"
                    className="input"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="user@school.edu.vn"
                    style={{
                      borderRadius: 'var(--radius-full)',
                      border: '2px solid var(--color-fg)',
                      padding: '9px 18px',
                      fontWeight: 700,
                    }}
                  />
                </div>
              </div>

              {/* Nếu là Phụ huynh: thêm Địa chỉ và Nghề nghiệp */}
              {profile.guardianProfile && (
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 16 }}>
                  <div className="field">
                    <label className="label" htmlFor="profile-address" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
                      <MapPin size={13} style={{ display: 'inline', verticalAlign: '-2px', marginRight: 4 }} />
                      ĐỊA CHỈ LIÊN HỆ
                    </label>
                    <input
                      id="profile-address"
                      type="text"
                      className="input"
                      value={address}
                      onChange={(e) => setAddress(e.target.value)}
                      placeholder="Số nhà, đường, quận/huyện..."
                      style={{
                        borderRadius: 'var(--radius-full)',
                        border: '2px solid var(--color-fg)',
                        padding: '9px 18px',
                        fontWeight: 600,
                      }}
                    />
                  </div>

                  <div className="field">
                    <label className="label" htmlFor="profile-occupation" style={{ fontSize: 'var(--fs-xs)', fontWeight: 800, letterSpacing: '0.04em' }}>
                      <Briefcase size={13} style={{ display: 'inline', verticalAlign: '-2px', marginRight: 4 }} />
                      NGHỀ NGHIỆP
                    </label>
                    <input
                      id="profile-occupation"
                      type="text"
                      className="input"
                      value={occupation}
                      onChange={(e) => setOccupation(e.target.value)}
                      placeholder="Nghề nghiệp / Đơn vị công tác"
                      style={{
                        borderRadius: 'var(--radius-full)',
                        border: '2px solid var(--color-fg)',
                        padding: '9px 18px',
                        fontWeight: 600,
                      }}
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
                  style={{
                    borderRadius: 'var(--radius-full)',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '4px 4px 0 0 var(--color-fg)',
                    fontWeight: 800,
                    padding: '10px 24px',
                    background: 'var(--color-accent)',
                    color: '#FFFFFF',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 8,
                  }}
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

          {/* Card 2: Role Details (Staff Profile) */}
          {profile.staffProfile && (
            <div
              className="card"
              style={{
                background: '#FFFFFF',
                border: '2px solid var(--color-fg)',
                boxShadow: '4px 4px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-lg)',
                padding: '24px',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 18, flexWrap: 'wrap', gap: 12 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                  <div
                    style={{
                      width: 44,
                      height: 44,
                      borderRadius: '50%',
                      background: '#EDE9FE',
                      border: '2px solid var(--color-fg)',
                      boxShadow: '2px 2px 0 0 var(--color-fg)',
                      display: 'grid',
                      placeItems: 'center',
                      flexShrink: 0,
                    }}
                  >
                    <School size={22} color="var(--color-accent)" strokeWidth={2.5} />
                  </div>
                  <div>
                    <h3 style={{ margin: 0, fontFamily: 'var(--font-heading)', fontSize: 'var(--fs-md)' }}>
                      Hồ sơ công tác cán bộ
                    </h3>
                    <p style={{ margin: '2px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                      Thông tin chức danh, tổ bộ môn do Nhà trường quản lý
                    </p>
                  </div>
                </div>
                <span className="badge badge--violet" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700, padding: '4px 12px' }}>
                  <ShieldCheck size={13} style={{ marginRight: 4 }} /> Do nhà trường quản lý
                </span>
              </div>

              <div
                style={{
                  display: 'grid',
                  gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                  gap: 16,
                }}
              >
                <div
                  style={{
                    padding: '14px 18px',
                    background: '#FFFDF5',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '2px 2px 0 0 var(--color-fg)',
                    borderRadius: 'var(--radius-md)',
                  }}
                >
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 700, textTransform: 'uppercase', marginBottom: 4 }}>
                    Mã số cán bộ (Staff Code)
                  </div>
                  <div style={{ fontWeight: 800, fontSize: 'var(--fs-base)', fontFamily: 'var(--font-heading)', color: 'var(--color-accent)' }}>
                    {profile.staffProfile.staffCode || 'Chưa cập nhật'}
                  </div>
                </div>

                <div
                  style={{
                    padding: '14px 18px',
                    background: '#ECFDF5',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '2px 2px 0 0 var(--color-fg)',
                    borderRadius: 'var(--radius-md)',
                  }}
                >
                  <div style={{ fontSize: 'var(--fs-xs)', color: '#047857', fontWeight: 700, textTransform: 'uppercase', marginBottom: 4 }}>
                    Phòng ban / Tổ chuyên môn
                  </div>
                  <div style={{ fontWeight: 800, fontSize: 'var(--fs-base)', color: '#065F46' }}>
                    {profile.staffProfile.department || 'Ban Giám Hiệu'}
                  </div>
                </div>

                <div
                  style={{
                    padding: '14px 18px',
                    background: '#FEF3C7',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '2px 2px 0 0 var(--color-fg)',
                    borderRadius: 'var(--radius-md)',
                  }}
                >
                  <div style={{ fontSize: 'var(--fs-xs)', color: '#B45309', fontWeight: 700, textTransform: 'uppercase', marginBottom: 4 }}>
                    Vị trí / Chức vụ
                  </div>
                  <div style={{ fontWeight: 800, fontSize: 'var(--fs-base)', color: '#78350F' }}>
                    {profile.staffProfile.position || 'Cán bộ'}
                  </div>
                </div>

                <div
                  style={{
                    padding: '14px 18px',
                    background: '#EDE9FE',
                    border: '2px solid var(--color-fg)',
                    boxShadow: '2px 2px 0 0 var(--color-fg)',
                    borderRadius: 'var(--radius-md)',
                  }}
                >
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-accent)', fontWeight: 700, textTransform: 'uppercase', marginBottom: 4 }}>
                    Phân công Chủ nhiệm
                  </div>
                  <div style={{ fontWeight: 800, fontSize: 'var(--fs-base)', color: 'var(--color-fg)', display: 'flex', alignItems: 'center', gap: 6 }}>
                    <GraduationCap size={16} color="var(--color-accent)" />
                    {profile.staffProfile.isHomeroomTeacher
                      ? `Lớp ${profile.staffProfile.homeroomClassName || 'Đang phân công'}`
                      : 'Không phụ trách GVCN'}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Card 3: Guardian Profile (if Parent) */}
          {profile.guardianProfile && (
            <div
              className="card"
              style={{
                background: '#FFFFFF',
                border: '2px solid var(--color-fg)',
                boxShadow: '4px 4px 0 0 var(--color-fg)',
                borderRadius: 'var(--radius-lg)',
                padding: '24px',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 18, flexWrap: 'wrap', gap: 12 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                  <div
                    style={{
                      width: 44,
                      height: 44,
                      borderRadius: '50%',
                      background: '#D1FAE5',
                      border: '2px solid var(--color-fg)',
                      boxShadow: '2px 2px 0 0 var(--color-fg)',
                      display: 'grid',
                      placeItems: 'center',
                      flexShrink: 0,
                    }}
                  >
                    <HeartHandshake size={22} color="#059669" strokeWidth={2.5} />
                  </div>
                  <div>
                    <h3 style={{ margin: 0, fontFamily: 'var(--font-heading)', fontSize: 'var(--fs-md)' }}>
                      Hồ sơ phụ huynh & Giám hộ
                    </h3>
                    <p style={{ margin: '2px 0 0', fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)' }}>
                      Định danh công dân và danh sách các con đang theo học
                    </p>
                  </div>
                </div>
                <span className="badge badge--mint" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 700, padding: '4px 12px' }}>
                  <ShieldCheck size={13} style={{ marginRight: 4 }} /> Đã xác thực CCCD
                </span>
              </div>

              {/* Identity card info */}
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 14,
                  padding: '14px 18px',
                  background: '#FFFDF5',
                  border: '2px solid var(--color-fg)',
                  boxShadow: '2px 2px 0 0 var(--color-fg)',
                  borderRadius: 'var(--radius-md)',
                  marginBottom: 20,
                }}
              >
                <IdCard size={24} color="var(--color-accent)" />
                <div>
                  <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--color-muted-fg)', fontWeight: 700, textTransform: 'uppercase' }}>
                    Số CMND / Căn cước công dân
                  </div>
                  <div style={{ fontWeight: 800, fontFamily: 'monospace', fontSize: 'var(--fs-base)', color: 'var(--color-fg)' }}>
                    {profile.guardianProfile.identityCardNumber || 'Chưa định danh'}
                  </div>
                </div>
              </div>

              {/* Children Table */}
              <div style={{ marginTop: 12 }}>
                <h4 style={{ fontSize: 'var(--fs-sm)', marginBottom: 12, display: 'flex', alignItems: 'center', gap: 6, fontWeight: 800 }}>
                  <GraduationCap size={16} color="var(--color-accent)" /> Danh sách học sinh phụ trách ({profile.guardianProfile.children.length})
                </h4>

                {profile.guardianProfile.children.length === 0 ? (
                  <div className="banner banner--info" style={{ fontSize: 'var(--fs-sm)', border: '2px solid var(--color-fg)', borderRadius: 'var(--radius-md)' }}>
                    Chưa có học sinh nào được liên kết với tài khoản này. Vui lòng gửi yêu cầu liên kết hồ sơ con tại Cổng Phụ huynh.
                  </div>
                ) : (
                  <div style={{ overflowX: 'auto', border: '2px solid var(--color-fg)', borderRadius: 'var(--radius-md)' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 'var(--fs-sm)' }}>
                      <thead>
                        <tr style={{ borderBottom: '2px solid var(--color-fg)', background: '#F8FAFC' }}>
                          <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 800 }}>Mã HS</th>
                          <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 800 }}>Họ và tên con</th>
                          <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 800 }}>Lớp học</th>
                          <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 800 }}>Ngày sinh</th>
                          <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 800 }}>Mối quan hệ</th>
                          <th style={{ padding: '10px 12px', textAlign: 'center', fontWeight: 800 }}>Liên hệ chính</th>
                        </tr>
                      </thead>
                      <tbody>
                        {profile.guardianProfile.children.map((child) => (
                          <tr
                            key={child.studentId}
                            style={{ borderBottom: '1px solid var(--color-border)', transition: 'background 0.15s' }}
                            onMouseEnter={(e) => (e.currentTarget.style.background = '#FFFDF5')}
                            onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
                          >
                            <td style={{ padding: '10px 12px' }}>
                              <span className="badge badge--violet" style={{ border: '1.5px solid var(--color-fg)', fontWeight: 800 }}>
                                {child.studentCode}
                              </span>
                            </td>
                            <td style={{ padding: '10px 12px', fontWeight: 700 }}>{child.fullName}</td>
                            <td style={{ padding: '10px 12px' }}>
                              {child.className ? (
                                <span className="badge badge--mint" style={{ border: '1px solid var(--color-fg)', fontWeight: 700 }}>
                                  Lớp {child.className}
                                </span>
                              ) : (
                                <span style={{ color: 'var(--color-muted-fg)' }}>Chưa xếp lớp</span>
                              )}
                            </td>
                            <td style={{ padding: '10px 12px', color: 'var(--color-muted-fg)' }}>
                              <Calendar size={13} style={{ display: 'inline', verticalAlign: '-2px', marginRight: 4 }} />
                              {child.dateOfBirth
                                ? new Date(child.dateOfBirth).toLocaleDateString('vi-VN')
                                : 'N/A'}
                            </td>
                            <td style={{ padding: '10px 12px', fontWeight: 600 }}>{child.relationship || 'Phụ huynh'}</td>
                            <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                              {child.isPrimaryContact ? (
                                <span className="badge badge--amber" style={{ fontSize: 'var(--fs-xs)', border: '1px solid var(--color-fg)', fontWeight: 700 }}>
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
