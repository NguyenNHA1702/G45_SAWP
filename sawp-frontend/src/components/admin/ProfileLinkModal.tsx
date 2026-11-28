import { useCallback, useEffect, useState } from 'react';
import { Link2, Unlink, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { adminApi } from '../../services/implementations/adminApi';
import { asApiError } from '../../utils/apiError';
import type { AdminProfileLinkingOverviewDto, AdminUserDto } from '../../types/admin.types';

interface ProfileLinkModalProps {
  user: AdminUserDto | null;
  onClose: () => void;
  onSaved: () => void;
}

export function ProfileLinkModal({ user, onClose, onSaved }: ProfileLinkModalProps) {
  const [overview, setOverview] = useState<AdminProfileLinkingOverviewDto | null>(null);
  const [profileType, setProfileType] = useState<'Staff' | 'Guardian'>('Guardian');
  const [selectedProfileId, setSelectedProfileId] = useState<number | ''>('');
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  const loadLinks = useCallback(async (uId: number, q: string) => {
    setLoading(true);
    try {
      const res = await adminApi.getUserProfileLinks(uId, q || undefined);
      if (res.success && res.data) {
        setOverview(res.data);
        if (res.data.user?.linkedStaffProfileId) {
          setProfileType('Staff');
          setSelectedProfileId(res.data.user.linkedStaffProfileId);
        } else {
          setProfileType('Guardian');
          setSelectedProfileId(res.data.user?.linkedGuardianId ?? '');
        }
      }
    } catch {
      toast.error('Không thể tải thông tin liên kết hồ sơ.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (user) {
      setSearch('');
      loadLinks(user.id, '');
    } else {
      setOverview(null);
    }
  }, [user, loadLinks]);

  if (!user) return null;

  const options = profileType === 'Staff' ? (overview?.staffProfiles ?? []) : (overview?.guardians ?? []);

  const handleSave = async () => {
    try {
      setSaving(true);
      const res = await adminApi.updateUserProfileLink(
        user.id,
        profileType,
        selectedProfileId === '' ? null : Number(selectedProfileId)
      );
      if (res.success) {
        toast.success(res.message || 'Đã cập nhật liên kết hồ sơ thành công.');
        onSaved();
        onClose();
      } else {
        toast.error(res.errors?.[0] || res.message || 'Không thể cập nhật liên kết hồ sơ.');
      }
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.errors?.[0] || apiErr.message || 'Lỗi khi liên kết hồ sơ.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(15, 23, 42, 0.55)', display: 'grid', placeItems: 'center', zIndex: 1000, padding: 16 }}>
      <div className="card" style={{ width: '100%', maxWidth: 540, display: 'flex', flexDirection: 'column', gap: 14 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <h3 style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
            <Link2 size={18} /> Liên kết hồ sơ thủ công (UC-55)
          </h3>
          <button type="button" className="btn btn--secondary btn--sm" onClick={onClose}>
            <X size={15} />
          </button>
        </div>

        <div style={{ fontSize: 'var(--fs-sm)', background: 'var(--color-muted)', padding: '10px 12px', borderRadius: 10 }}>
          <div>
            <strong>Tài khoản:</strong> {user.fullName} (<code>{user.email}</code>)
          </div>
          {overview?.user?.linkedStaffLabel && (
            <div style={{ marginTop: 4 }}>Nhân sự đang gắn: <strong>{overview.user.linkedStaffLabel}</strong></div>
          )}
          {overview?.user?.linkedGuardianLabel && (
            <div style={{ marginTop: 4 }}>Phụ huynh đang gắn: <strong>{overview.user.linkedGuardianLabel}</strong></div>
          )}
        </div>

        <div style={{ display: 'flex', gap: 10 }}>
          <button
            type="button"
            className={profileType === 'Guardian' ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
            onClick={() => {
              setProfileType('Guardian');
              setSelectedProfileId(overview?.user?.linkedGuardianId ?? '');
            }}
          >
            Hồ sơ Phụ huynh
          </button>
          <button
            type="button"
            className={profileType === 'Staff' ? 'btn btn--primary btn--sm' : 'btn btn--secondary btn--sm'}
            onClick={() => {
              setProfileType('Staff');
              setSelectedProfileId(overview?.user?.linkedStaffProfileId ?? '');
            }}
          >
            Hồ sơ Nhân sự
          </button>
        </div>

        <div style={{ display: 'flex', gap: 8 }}>
          <input
            type="text"
            className="input"
            style={{ flex: 1 }}
            placeholder={profileType === 'Staff' ? 'Tìm mã NV, họ tên, phòng ban...' : 'Tìm tên phụ huynh, SĐT, email...'}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <button type="button" className="btn btn--secondary btn--sm" onClick={() => loadLinks(user.id, search)}>
            Tìm
          </button>
        </div>

        {loading ? (
          <div style={{ padding: 20, textAlign: 'center' }}>Đang tải danh sách hồ sơ...</div>
        ) : (
          <label style={{ fontSize: 'var(--fs-sm)', fontWeight: 700 }}>
            Chọn {profileType === 'Staff' ? 'Hồ sơ Nhân sự' : 'Hồ sơ Phụ huynh'} để liên kết
            <select
              className="input"
              style={{ width: '100%', marginTop: 6 }}
              value={selectedProfileId}
              onChange={(e) => setSelectedProfileId(e.target.value ? Number(e.target.value) : '')}
            >
              {profileType === 'Guardian' && <option value="">— Không liên kết (Gỡ liên kết) —</option>}
              {profileType === 'Staff' && <option value="">— Chọn hồ sơ nhân sự —</option>}
              {options.map((opt) => (
                <option key={opt.profileId} value={opt.profileId}>
                  {opt.fullName} ({opt.codeOrPhone})
                  {opt.linkedUserId && opt.linkedUserId !== user.id ? ` [Đang gắn: ${opt.linkedUsername}]` : ''}
                </option>
              ))}
            </select>
          </label>
        )}

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
          {profileType === 'Guardian' && overview?.user?.linkedGuardianId && (
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              disabled={saving}
              onClick={async () => {
                setSaving(true);
                try {
                  const res = await adminApi.updateUserProfileLink(user.id, 'Guardian', null);
                  if (res.success) {
                    toast.success(res.message || 'Đã gỡ liên kết phụ huynh.');
                    onSaved();
                    onClose();
                  }
                } finally {
                  setSaving(false);
                }
              }}
            >
              <Unlink size={14} /> Gỡ liên kết PH
            </button>
          )}
          <button type="button" className="btn btn--secondary btn--sm" onClick={onClose}>
            Đóng
          </button>
          <button type="button" className="btn btn--primary btn--sm" disabled={saving || loading} onClick={handleSave}>
            {saving ? 'Đang lưu...' : 'Lưu liên kết'}
          </button>
        </div>
      </div>
    </div>
  );
}
