import { useState } from 'react';
import type { FormEvent } from 'react';
import toast from 'react-hot-toast';
import { KeyRound, Lock } from 'lucide-react';
import { authApi } from '../../services/implementations/authApi';
import { asApiError } from '../../utils/apiError';
import { SYSTEM_MESSAGES } from '../../constants/messages';
import { GeometricModal } from '../common/GeometricModal';

interface Props {
  open: boolean;
  onClose: () => void;
}

export function ChangePasswordModal({ open, onClose }: Props) {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const handleResetAndClose = () => {
    setCurrentPassword('');
    setNewPassword('');
    setConfirmPassword('');
    onClose();
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    if (!currentPassword || !newPassword || !confirmPassword) {
      toast.error(SYSTEM_MESSAGES.MSG01.content);
      return;
    }

    if (newPassword !== confirmPassword) {
      toast.error(SYSTEM_MESSAGES.MSG04.content);
      return;
    }

    setLoading(true);
    try {
      const res = await authApi.changePassword({ currentPassword, newPassword });
      toast.success(res.message || SYSTEM_MESSAGES.MSG46.content);
      handleResetAndClose();
    } catch (err) {
      const apiErr = asApiError(err);
      toast.error(apiErr.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <GeometricModal
      open={open}
      onClose={handleResetAndClose}
      title="Đổi mật khẩu tài khoản"
      badgeText="Bảo mật tài khoản"
      badgeColor="violet"
      icon={<KeyRound size={24} color="var(--color-accent)" />}
      iconBg="var(--color-accent-soft)"
      maxWidth="md"
    >
      <p style={{ fontSize: 'var(--fs-sm)', color: 'var(--color-muted-fg)', margin: '0 0 20px', lineHeight: 1.5 }}>
        Mật khẩu mới phải có tối thiểu 8 ký tự, bao gồm cả chữ hoa, chữ thường, chữ số và ký tự đặc biệt.
      </p>

      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <div className="field" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <label className="label" htmlFor="cur-pass">
            Mật khẩu hiện tại <span style={{ color: 'var(--color-secondary)' }}>*</span>
          </label>
          <input
            id="cur-pass"
            type="password"
            className="input"
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            disabled={loading}
            placeholder="Nhập mật khẩu bạn đang sử dụng"
            style={{ width: '100%', boxSizing: 'border-box' }}
            required
          />
        </div>

        <div className="field" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <label className="label" htmlFor="new-pass">
            Mật khẩu mới <span style={{ color: 'var(--color-secondary)' }}>*</span>
          </label>
          <input
            id="new-pass"
            type="password"
            className="input"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            disabled={loading}
            placeholder="Tối thiểu 8 ký tự, gồm chữ hoa, thường, số, ký tự đặc biệt"
            style={{ width: '100%', boxSizing: 'border-box' }}
            required
          />
        </div>

        <div className="field" style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
          <label className="label" htmlFor="cfm-pass">
            Xác nhận mật khẩu mới <span style={{ color: 'var(--color-secondary)' }}>*</span>
          </label>
          <input
            id="cfm-pass"
            type="password"
            className="input"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            disabled={loading}
            placeholder="Nhập lại chính xác mật khẩu mới"
            style={{ width: '100%', boxSizing: 'border-box' }}
            required
          />
        </div>

        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 12 }}>
          <button
            type="button"
            className="btn btn--secondary"
            onClick={handleResetAndClose}
            disabled={loading}
          >
            Hủy bỏ
          </button>
          <button
            type="submit"
            className="btn btn--primary"
            disabled={loading}
          >
            {loading ? (
              <span>Đang lưu...</span>
            ) : (
              <>
                <Lock size={16} />
                <span>Cập nhật mật khẩu</span>
              </>
            )}
          </button>
        </div>
      </form>
    </GeometricModal>
  );
}
